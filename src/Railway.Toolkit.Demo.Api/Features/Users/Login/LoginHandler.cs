using Railway.Toolkit.Demo.Api.Domain;

namespace Railway.Toolkit.Demo.Api.Features.Users.Login;

public sealed class LoginHandler
{
    // Both "not found" and "wrong password" map to the same error — prevents user enumeration.
    private static readonly Error InvalidCredentials = Error.Create(
        "Invalid email or password",
        "Auth.InvalidCredentials");

    private readonly IUserStore _store;

    public LoginHandler(IUserStore store)
    {
        _store = store;
    }

    public Result<LoginResponse> Handle(LoginRequest request)
    {
        Result<LoginRequest> validated = Validation.Combine(
            new[]
            {
                Result.Ok(request.Email).NotNullOrEmpty("Email").ToUnit(),
                Result.Ok(request.Password).NotNullOrEmpty("Password").ToUnit(),
            },
            () => request);

        return validated
            .Bind(FindUser)
            .Bind(user => VerifyPassword(user, request.Password))
            .Ensure(
                user => user.IsActive,
                Error.Create("Your account has been deactivated", "Auth.AccountInactive"))
            .Map(user => new LoginResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                IsActive = user.IsActive
            });
    }

    private Result<User> FindUser(LoginRequest request)
    {
        // MapError replaces the store's "not found" error with the generic invalid credentials
        // error so callers cannot tell whether the email address exists in the system.
        return _store.FindByEmail(request.Email)
            .MapError(_ => InvalidCredentials);
    }

    private static Result<User> VerifyPassword(User user, string password)
    {
        return PasswordHasher.Verify(password, user.PasswordHash)
            ? Result.Ok(user)
            : Result.Fail<User>(InvalidCredentials);
    }
}
