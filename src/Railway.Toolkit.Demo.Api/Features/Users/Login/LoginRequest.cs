namespace Railway.Toolkit.Demo.Api.Features.Users.Login;

public sealed record LoginRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}
