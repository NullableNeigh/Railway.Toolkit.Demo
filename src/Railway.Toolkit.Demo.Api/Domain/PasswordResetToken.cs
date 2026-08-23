namespace Railway.Toolkit.Demo.Api.Domain;

public sealed record PasswordResetToken
{
    public required int UserId { get; init; }
    public required string Token { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
