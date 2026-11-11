namespace Railway.Toolkit.Demo.Api.Features.Examples.Recovery;

public sealed record DefaultValueRequest(string? Value);
public sealed record AlternativeValueRequest(string? Preferred, string? Fallback);
public sealed record RecoveryResponse(int Value, string Source);
