namespace Railway.Toolkit.Demo.Api.Features.Users.PasswordReset;

public sealed record PasswordResetResponse
{
    public required string Message { get; init; }

    /// <summary>
    /// Exposed here for demo purposes only — in production this would be sent by email only.
    /// </summary>
    public required string Token { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}
