using System.Text.RegularExpressions;
using Railway.Toolkit.Demo.Api.Domain;

namespace Railway.Toolkit.Demo.Api.Features.Users.Register;

public sealed class RegisterHandler
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly IUserStore _store;

    public RegisterHandler(IUserStore store)
    {
        _store = store;
    }

    public Result<RegisterResponse> Handle(RegisterRequest request)
    {
        Result<RegisterRequest> validated = Validation.Combine(
            new[]
            {
                Result.Ok(request.Username)
                    .NotNullOrEmpty("Username")
                    .HasMinLength(3, "Username")
                    .HasMaxLength(50, "Username")
                    .ToUnit(),

                Result.Ok(request.Email)
                    .NotNullOrEmpty("Email")
                    .Ensure(
                        e => EmailRegex.IsMatch(e),
                        "Email must be a valid email address",
                        "Validation.Format",
                        new Dictionary<string, string[]> { { "Email", ["must be a valid email address"] } })
                    .ToUnit(),

                Result.Ok(request.Password)
                    .NotNullOrEmpty("Password")
                    .HasMinLength(8, "Password")
                    .Ensure(
                        p => p.Any(char.IsDigit),
                        "Password must contain at least one digit",
                        "Validation.Complexity",
                        new Dictionary<string, string[]> { { "Password", ["must contain at least one digit"] } })
                    .ToUnit(),

                Result.Ok(request.DateOfBirth)
                    .Ensure(
                        dob => CalculateAge(dob) >= 18,
                        "Must be at least 18 years old",
                        "Validation.Range",
                        new Dictionary<string, string[]> { { "DateOfBirth", ["must be at least 18 years old"] } })
                    .ToUnit(),
            },
            () => request);

        return validated
            .Bind(EnsureEmailNotTaken)
            .Bind(EnsureUsernameNotTaken)
            .Bind(CreateAndStoreUser);
    }

    private Result<RegisterRequest> EnsureEmailNotTaken(RegisterRequest request)
    {
        Result<User> existing = _store.FindByEmail(request.Email);

        return existing is Result<User>.Ok
            ? Result.Fail<RegisterRequest>(Error.Create(
                $"Email '{request.Email}' is already registered",
                "User.EmailAlreadyExists"))
            : Result.Ok(request);
    }

    private Result<RegisterRequest> EnsureUsernameNotTaken(RegisterRequest request)
    {
        Result<User> existing = _store.FindByUsername(request.Username);

        return existing is Result<User>.Ok
            ? Result.Fail<RegisterRequest>(Error.Create(
                $"Username '{request.Username}' is already taken",
                "User.UsernameAlreadyExists"))
            : Result.Ok(request);
    }

    private Result<RegisterResponse> CreateAndStoreUser(RegisterRequest request)
    {
        User user = new User
        {
            Id = 0, // assigned by store
            Username = request.Username,
            Email = request.Email.ToLowerInvariant(),
            PasswordHash = PasswordHasher.Hash(request.Password),
            DateOfBirth = request.DateOfBirth,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        return _store.Add(user).Map(stored => new RegisterResponse
        {
            Id = stored.Id,
            Username = stored.Username,
            Email = stored.Email,
            DateOfBirth = stored.DateOfBirth,
            CreatedAt = stored.CreatedAt
        });
    }

    private static int CalculateAge(DateOnly dateOfBirth)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        int age = today.Year - dateOfBirth.Year;

        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}
