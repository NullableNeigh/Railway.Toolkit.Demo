namespace Railway.Toolkit.Demo.Api.Features.Examples.Exceptions;

public sealed record Base64DecodeRequest(string? Value);
public sealed record Base64DecodeResponse(int ByteCount, string Hex);
