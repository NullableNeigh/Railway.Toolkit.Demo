namespace Railway.Toolkit.Demo.Api.Features.Examples.Exceptions;

public sealed class Base64DecodeHandler
{
    public Result<Base64DecodeResponse> Handle(Base64DecodeRequest request) =>
        Result.FromNullable(request.Value, "Value is required", "Example.ValueRequired")
            // Convert throws for malformed Base64. TryMap converts that boundary to a failure.
            .TryMap(Convert.FromBase64String, exceptionMapper: MapException)
            .Map(bytes => new Base64DecodeResponse(bytes.Length, Convert.ToHexString(bytes)));

    private static Error MapException(Exception exception) => exception is FormatException
        ? Error.Create("Value must be valid Base64", "Example.InvalidBase64")
        : Error.Create("Unable to decode the value", "Example.DecodeFailed");
}
