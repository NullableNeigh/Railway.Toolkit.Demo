using Railway.Toolkit.Demo.Api.Domain;

namespace Railway.Toolkit.Demo.Api.Features.Tasks.Create;

public sealed class CreateTaskHandler(ITaskStore store, TaskValidation validation)
{
    public Result<TodoTask> Handle(TaskRequest request) =>
        validation.Validate(request)
            .Map(valid => new TodoTask(0, valid.Title!, valid.Description, valid.DueDate, TodoStatus.Open))
            .Bind(store.Add);
}
