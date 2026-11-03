using Railway.Toolkit.Demo.Api.Domain;

namespace Railway.Toolkit.Demo.Api.Infrastructure;

public interface ITaskStore
{
    Result<TodoTask> Add(TodoTask task);
    Result<TodoTask> Find(int id);
    IReadOnlyList<TodoTask> GetAll();
    Result<TodoTask> Update(int id, Func<TodoTask, Result<TodoTask>> change);
}
