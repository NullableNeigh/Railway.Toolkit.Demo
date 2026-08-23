namespace Railway.Toolkit.Demo.Api.Features.Users.PasswordReset;

public sealed record PasswordResetRequest
{
    public required string Email { get; init; }
}
