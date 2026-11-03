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
                Result.Ok(request.DueDate)
                    .Ensure(date => date is null || date >= today,
                        "Due date cannot be in the past", "Validation.Range",
                        new Dictionary<string, string[]> { ["DueDate"] = ["cannot be in the past"] })
                    .ToUnit()
            },
            () => request);
    }
}
