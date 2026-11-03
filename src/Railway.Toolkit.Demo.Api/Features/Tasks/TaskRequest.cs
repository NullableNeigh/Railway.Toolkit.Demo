namespace Railway.Toolkit.Demo.Api.Features.Tasks;

// Used by create and full edit; nullable input lets the railway report missing titles.
public sealed record TaskRequest(string? Title, string? Description = null, DateOnly? DueDate = null);
