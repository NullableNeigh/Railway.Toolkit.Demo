using System.Collections.Concurrent;
using Railway.Toolkit.Demo.Api.Domain;

namespace Railway.Toolkit.Demo.Api.Infrastructure;

public sealed class UserStore : IUserStore
{
    private readonly ConcurrentDictionary<int, User> _users = new();
    private int _nextId = 0;

    public Result<User> Add(User user)
    {
        int id = Interlocked.Increment(ref _nextId);
        User stored = user with { Id = id };
        _users[id] = stored;
        return Result.Ok(stored);
    }

    public Result<User> FindById(int id)
    {
        return _users.TryGetValue(id, out User? user)
            ? Result.Ok(user)
            : Result.Fail<User>($"No user found with id '{id}'", "User.NotFound");
    }

    public Result<User> FindByEmail(string email)
    {
        foreach (User user in _users.Values)
        {
            if (string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Ok(user);
            }
        }

        return Result.Fail<User>($"No user found with email '{email}'", "User.NotFound");
    }

    public Result<User> FindByUsername(string username)
    {
        foreach (User user in _users.Values)
        {
            if (string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Ok(user);
            }
        }

        return Result.Fail<User>($"No user found with username '{username}'", "User.NotFound");
    }

    public Result<IReadOnlyList<User>> GetAll()
    {
        List<User> users = new(_users.Values);
        users.Sort((a, b) => a.Id.CompareTo(b.Id));
        return Result.Ok<IReadOnlyList<User>>(users);
    }
}
