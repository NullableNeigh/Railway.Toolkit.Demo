using Railway.Toolkit.Demo.Api.Domain;
using Railway.Toolkit.Demo.Api.Features.Tasks.Create;

namespace Railway.Toolkit.Demo.Api.Features.Tasks.BatchCreate;

public sealed class BatchCreateTaskHandler(CreateTaskHandler create)
{
    public Result<BatchCreateResponse> Handle(IReadOnlyList<TaskRequest?> requests)
    {
        return Result.Ok<IEnumerable<TaskRequest?>>(requests)
            .NotNullOrEmpty("Tasks")
            .Map(items =>
            {
                // Materialize once: creating tasks is a side effect, not a repeatable query.
                Result<TodoTask>[] outcomes = items.Select(request => request is null
                    ? Result.Fail<TodoTask>("Task item cannot be null", "Task.InvalidItem")
                    : create.Handle(request)).ToArray();

                var (successes, failures) = outcomes.Partition();
                BatchTaskResult[] results = outcomes.Select((outcome, index) => outcome.Match(
                    ok => new BatchTaskResult(index, ok.Value, null),
                    fail => new BatchTaskResult(index, null, fail.Error))).ToArray();

                // Partial item failures are part of a successful batch response.
                return new BatchCreateResponse(outcomes.Length, successes.Count, failures.Count, results);
            });
    }
}
