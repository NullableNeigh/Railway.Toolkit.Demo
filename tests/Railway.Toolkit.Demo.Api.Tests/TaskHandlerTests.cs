using Microsoft.Extensions.Logging.Abstractions;
using Railway.Toolkit;
using Railway.Toolkit.Demo.Api.Domain;
using Railway.Toolkit.Demo.Api.Features.Tasks;
using Railway.Toolkit.Demo.Api.Features.Tasks.BatchCreate;
using Railway.Toolkit.Demo.Api.Features.Tasks.Complete;
using Railway.Toolkit.Demo.Api.Features.Tasks.Create;
using Railway.Toolkit.Demo.Api.Features.Tasks.Edit;
using Railway.Toolkit.Demo.Api.Features.Tasks.Lookup;
using Railway.Toolkit.Demo.Api.Infrastructure;

namespace Railway.Toolkit.Demo.Api.Tests;

public sealed class TaskHandlerTests
{
    private readonly TaskStore _store = new();
    private readonly TaskValidation _validation = new(new FixedClock());
    private CreateTaskHandler Create => new(_store, _validation);
    private CompleteTaskHandler Complete => new(_store, NullLogger<CompleteTaskHandler>.Instance);

    [Fact]
    public void Create_preserves_fields_and_assigns_open_status_and_unique_ids()
    {
        TaskRequest request = new("Learn Railway", "Try a pipeline", new DateOnly(2026, 11, 3));
        TodoTask first = Ok(Create.Handle(request));
        TodoTask second = Ok(Create.Handle(request));
        Assert.Equal(request.Title, first.Title);
        Assert.Equal(request.Description, first.Description);
        Assert.Equal(request.DueDate, first.DueDate);
        Assert.Equal(TodoStatus.Open, first.Status);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first, Ok(_store.Find(first.Id)));
    }

    [Fact]
    public void Validation_collects_independent_field_errors_without_storing()
    {
        Error error = Fail(Create.Handle(new(" ", new string('x', 1001), new DateOnly(2026, 11, 2))));
        Assert.Equal("Validation.Failed", error.Code);
        Assert.Equal(new[] { "Description", "DueDate", "Title" }, error.Details!.Keys.Order().ToArray());
        Assert.Empty(_store.GetAll());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Missing_title_is_a_validation_failure(string? title) =>
        Assert.Contains("Title", Fail(Create.Handle(new(title))).Details!.Keys);

    [Fact]
    public void Length_limits_are_inclusive()
    {
        Ok(Create.Handle(new(new string('x', 100), new string('x', 1000))));
        Assert.Contains("Title", Fail(Create.Handle(new(new string('x', 101)))).Details!.Keys);
        Assert.Single(_store.GetAll());
    }

    [Fact]
    public void Due_date_uses_utc_calendar_day()
    {
        Ok(Create.Handle(new("Today", DueDate: new DateOnly(2026, 11, 3))));
        Ok(Create.Handle(new("Tomorrow", DueDate: new DateOnly(2026, 11, 4))));
        Ok(Create.Handle(new("No deadline")));
        Assert.Contains("DueDate", Fail(Create.Handle(new("Yesterday", DueDate: new DateOnly(2026, 11, 2)))).Details!.Keys);
    }

    [Fact]
    public void Edit_replaces_fields_and_failed_validation_preserves_original()
    {
        TodoTask original = Ok(Create.Handle(new("Original", "Description", new DateOnly(2026, 11, 4))));
        EditTaskHandler edit = new(_store, _validation);
        Fail(edit.Handle(original.Id, new("")));
        Assert.Equal(original, Ok(_store.Find(original.Id)));
        TodoTask changed = Ok(edit.Handle(original.Id, new("Changed")));
        Assert.Equal(original.Id, changed.Id);
        Assert.Equal("Changed", changed.Title);
        Assert.Null(changed.Description);
        Assert.Null(changed.DueDate);
    }

    [Fact]
    public void Completed_task_rejects_edit_and_repeat_completion()
    {
        TodoTask task = Ok(Create.Handle(new("Finish")));
        TodoTask completed = Ok(Complete.Handle(task.Id));
        Assert.Equal(TodoStatus.Completed, completed.Status);
        Assert.Equal("Task.AlreadyCompleted", Fail(Complete.Handle(task.Id)).Code);
        Assert.Equal("Task.AlreadyCompleted", Fail(new EditTaskHandler(_store, _validation).Handle(task.Id, new("Changed"))).Code);
        Assert.Equal(completed, Ok(_store.Find(task.Id)));
    }

    [Fact]
    public void Missing_task_short_circuits_lookup_edit_and_completion()
    {
        Assert.Equal("Task.NotFound", Fail(new LookupTaskHandler(_store).Handle(999)).Code);
        Assert.Equal("Task.NotFound", Fail(new EditTaskHandler(_store, _validation).Handle(999, new(""))).Code);
        Assert.Equal("Task.NotFound", Fail(Complete.Handle(999)).Code);
        Assert.Empty(_store.GetAll());
    }

    [Fact]
    public void Listing_filters_status_and_keeps_id_order()
    {
        TodoTask first = Ok(Create.Handle(new("First")));
        TodoTask second = Ok(Create.Handle(new("Second")));
        Ok(Complete.Handle(first.Id));
        LookupTaskHandler lookup = new(_store);
        Assert.Equal(new[] { first.Id, second.Id }, lookup.HandleAll(null).Select(task => task.Id));
        Assert.Equal(second.Id, Assert.Single(lookup.HandleAll(TodoStatus.Open)).Id);
        Assert.Equal(first.Id, Assert.Single(lookup.HandleAll(TodoStatus.Completed)).Id);
    }

    [Fact]
    public void Batch_preserves_input_order_and_does_not_repeat_side_effects()
    {
        BatchCreateResponse response = Ok(new BatchCreateTaskHandler(Create).Handle(
            [new("First"), new(""), null, new("Last")]));
        Assert.Equal(4, response.Total);
        Assert.Equal(2, response.Succeeded);
        Assert.Equal(2, response.Failed);
        Assert.Equal(new[] { 0, 1, 2, 3 }, response.Results.Select(item => item.Index));
        Assert.Equal("First", response.Results[0].Task!.Title);
        Assert.Equal("Validation.Failed", response.Results[1].Error!.Code);
        Assert.Equal("Task.InvalidItem", response.Results[2].Error!.Code);
        Assert.Equal("Last", response.Results[3].Task!.Title);
        Assert.Equal(2, _store.GetAll().Count);
        Assert.All(response.Results, item => Assert.True((item.Task is null) != (item.Error is null)));
    }

    [Fact]
    public void Empty_batch_fails_and_all_invalid_batch_succeeds_with_item_failures()
    {
        BatchCreateTaskHandler batch = new(Create);
        Assert.StartsWith("Validation.", Fail(batch.Handle([])).Code);
        BatchCreateResponse response = Ok(batch.Handle([new(""), null]));
        Assert.Equal(0, response.Succeeded);
        Assert.Equal(2, response.Failed);
        Assert.Empty(_store.GetAll());
    }

    [Fact]
    public async Task Concurrent_completion_has_one_success_and_one_conflict()
    {
        int id = Ok(Create.Handle(new("Finish once"))).Id;
        Result<TodoTask>[] results = await Task.WhenAll(
            Task.Run(() => Complete.Handle(id), cancellationToken: TestContext.Current.CancellationToken), Task.Run(() => Complete.Handle(id), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Single(results.OfType<Result<TodoTask>.Ok>());
        Assert.Equal("Task.AlreadyCompleted", Assert.Single(results.OfType<Result<TodoTask>.Fail>()).Error.Code);
    }

    [Fact]
    public void Undated_task_still_collects_other_field_errors()
    {
        Error error = Fail(Create.Handle(new(" ", new string('x', 1001))));
        Assert.Equal("Validation.Failed", error.Code);
        Assert.Equal(new[] { "Description", "Title" }, error.Details!.Keys.Order());
        Assert.Empty(_store.GetAll());
    }

    private static T Ok<T>(Result<T> result) => Assert.IsType<Result<T>.Ok>(result).Value;
    private static Error Fail<T>(Result<T> result) => Assert.IsType<Result<T>.Fail>(result).Error;

    private sealed class FixedClock : TimeProvider
    {
        // Local calendar date differs; rules must use UTC, not this offset's date.
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 2, 23, 30, 0, TimeSpan.FromHours(-2));
    }
}
