namespace Railway.Toolkit.Demo.Api.Features.Users.Register;

public sealed record RegisterRequest
{
    public required string Username { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required DateOnly DateOfBirth { get; init; }
}
