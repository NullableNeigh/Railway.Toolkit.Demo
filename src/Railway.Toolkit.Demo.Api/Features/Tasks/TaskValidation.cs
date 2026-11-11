namespace Railway.Toolkit.Demo.Api.Features.Tasks;

public sealed class TaskValidation(TimeProvider clock)
{
    public Result<TaskRequest> Validate(TaskRequest request)
    {
        DateOnly today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        // Collect failures across fields; each field's own pipeline stops on failure.
        return Validation.Combine(
            new[]
            {
                Result.Ok(request.Title ?? string.Empty)
                    .NotNullOrWhiteSpace("Title")
                    .HasMaxLength(100, "Title").ToUnit(),
                Result.Ok(request.Description ?? string.Empty)
                    .HasMaxLength(1_000, "Description").ToUnit(),
                ValidateDueDate(request.DueDate, today)
            },
            () => request);
    }

    // An undated task is a valid choice, not a failure to recover from.
    // Keep both paths inside Combine so date errors still collect with other field errors.
    private static Result<Unit> ValidateDueDate(DateOnly? dueDate, DateOnly today) =>
        dueDate is { } date
            ? Result.Ok(date)
                .Ensure(value => value >= today,
                    "Due date cannot be in the past", "Validation.Range",
                    new Dictionary<string, string[]> { ["DueDate"] = ["cannot be in the past"] })
                .ToUnit()
            : Result.Ok();
}
