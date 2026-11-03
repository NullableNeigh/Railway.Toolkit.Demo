using Railway.Toolkit.Demo.Api.Domain;

namespace Railway.Toolkit.Demo.Api.Features.Tasks.Complete;

public sealed class CompleteTaskHandler(ITaskStore store, ILogger<CompleteTaskHandler> logger)
{
    public Result<TodoTask> Handle(int id) =>
        store.Update(id, task => Result.Ok(task)
            .Ensure(current => current.Status == TodoStatus.Open,
                "Task is already completed", "Task.AlreadyCompleted")
            .Map(current => current with { Status = TodoStatus.Completed }))
        .Tap(task => logger.LogInformation("Completed task {TaskId}", task.Id));
}
