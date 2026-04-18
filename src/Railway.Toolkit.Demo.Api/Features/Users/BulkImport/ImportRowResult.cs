namespace Railway.Toolkit.Demo.Api.Features.Users.BulkImport;

public sealed record ImportRowResult
{
    public required int Row { get; init; }
    public required bool Success { get; init; }
    public int? UserId { get; init; }
    public string? Username { get; init; }
    public Error? Error { get; init; }
}
