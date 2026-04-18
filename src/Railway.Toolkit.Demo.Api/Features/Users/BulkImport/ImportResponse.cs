namespace Railway.Toolkit.Demo.Api.Features.Users.BulkImport;

public sealed record ImportResponse
{
    public required int TotalRows { get; init; }
    public required int Succeeded { get; init; }
    public required int Failed { get; init; }
    public required IReadOnlyList<ImportRowResult> Results { get; init; }
}
