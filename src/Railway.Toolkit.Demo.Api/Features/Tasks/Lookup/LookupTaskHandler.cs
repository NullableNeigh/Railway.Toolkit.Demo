using Railway.Toolkit.Demo.Api.Domain;

namespace Railway.Toolkit.Demo.Api.Features.Tasks.Lookup;

public sealed class LookupTaskHandler(ITaskStore store)
{
    public Result<TodoTask> Handle(int id) => store.Find(id);

    public IReadOnlyList<TodoTask> HandleAll(TodoStatus? status) =>
        store.GetAll().Where(task => status is null || task.Status == status).ToArray();
}
