using Railway.Toolkit.Demo.Api.Domain;

namespace Railway.Toolkit.Demo.Api.Infrastructure;

// Process-local demonstration storage. Immutable values cannot be changed by callers.
public sealed class TaskStore : ITaskStore
{
    private readonly Dictionary<int, TodoTask> _tasks = new();
    private readonly object _gate = new();
    private int _nextId;

    public Result<TodoTask> Add(TodoTask task)
    {
        lock (_gate)
        {
            TodoTask stored = task with { Id = ++_nextId };
            _tasks.Add(stored.Id, stored);
            return Result.Ok(stored);
        }
    }

    public Result<TodoTask> Find(int id)
    {
        lock (_gate)
        {
            return FindCore(id);
        }
    }

    public IReadOnlyList<TodoTask> GetAll()
    {
        lock (_gate)
        {
            return _tasks.Values.OrderBy(task => task.Id).ToArray();
        }
    }

    public Result<TodoTask> Update(int id, Func<TodoTask, Result<TodoTask>> change)
    {
        // Check the current state and apply the change as one operation.
        lock (_gate)
        {
            return FindCore(id).Bind(change).Tap(task => _tasks[id] = task);
        }
    }

    private Result<TodoTask> FindCore(int id) =>
        Result.FromNullable(_tasks.GetValueOrDefault(id),
            $"Task '{id}' was not found", "Task.NotFound");
}
