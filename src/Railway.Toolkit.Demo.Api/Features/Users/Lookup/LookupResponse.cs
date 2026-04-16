namespace Railway.Toolkit.Demo.Api.Features.Users.Lookup;

public sealed record LookupResponse
{
    public required int Id { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    public required bool IsActive { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
