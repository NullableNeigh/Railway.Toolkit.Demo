using Railway.Toolkit.Demo.Api.Domain;

namespace Railway.Toolkit.Demo.Api.Features.Users.Lookup;

public sealed class LookupHandler
{
    private readonly IUserStore _store;

    public LookupHandler(IUserStore store)
    {
        _store = store;
    }

    public Result<LookupResponse> Handle(int id)
    {
        return _store.FindById(id).Map(ToResponse);
    }

    public Result<IReadOnlyList<LookupResponse>> HandleAll()
    {
        return _store.GetAll().Map(users =>
        {
            List<LookupResponse> responses = new(users.Count);

            foreach (User user in users)
            {
                responses.Add(ToResponse(user));
            }

            return (IReadOnlyList<LookupResponse>)responses;
        });
    }

    private static LookupResponse ToResponse(User user)
    {
        return new LookupResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DateOfBirth = user.DateOfBirth,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        };
    }
}
