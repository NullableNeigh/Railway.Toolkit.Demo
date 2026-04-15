using Railway.Toolkit.Demo.Api.Domain;

namespace Railway.Toolkit.Demo.Api.Infrastructure;

public interface IUserStore
{
    /// <summary>
    /// Adds a user to the store. The Id on the supplied user is ignored — the store
    /// assigns the next available Id and returns the user with it populated.
    /// </summary>
    Result<User> Add(User user);

    Result<User> FindById(int id);
    Result<User> FindByEmail(string email);
    Result<User> FindByUsername(string username);
    Result<IReadOnlyList<User>> GetAll();
}
