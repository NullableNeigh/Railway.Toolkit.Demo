using Railway.Toolkit.Demo.Api.Domain;

namespace Railway.Toolkit.Demo.Api.Features.Tasks.Edit;

public sealed class EditTaskHandler(ITaskStore store, TaskValidation validation)
{
    public Result<TodoTask> Handle(int id, TaskRequest request) =>
        store.Update(id, task => Result.Ok(task)
            .Ensure(current => current.Status == TodoStatus.Open,
                "Completed tasks cannot be edited", "Task.AlreadyCompleted")
            .Bind(current => validation.Validate(request).Map(valid => current with
            {
                Title = valid.Title!, Description = valid.Description, DueDate = valid.DueDate
            })));
}
