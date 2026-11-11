using System.Globalization;

namespace Railway.Toolkit.Demo.Api.Features.Examples.Recovery;

public sealed class RecoveryExamplesHandler
{
    public Result<RecoveryResponse> WithDefault(DefaultValueRequest request) =>
        Parse(request.Value, "Value")
            .Map(value => new RecoveryResponse(value, "primary"))
            .OrElse(new RecoveryResponse(10, "default"));

    public Result<RecoveryResponse> WithAlternative(AlternativeValueRequest request) =>
        Parse(request.Preferred, "Preferred")
            .Map(value => new RecoveryResponse(value, "primary"))
            // The fallback is parsed only on the failure track, and can also fail.
            .OrElseWith(_ => Parse(request.Fallback, "Fallback")
                .Map(value => new RecoveryResponse(value, "fallback")));

    private static Result<int> Parse(string? text, string field) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? Result.Ok(value)
            : Result.Fail<int>(new Error
            {
                Code = "Example.InvalidInteger",
                Message = $"{field} must be a 32-bit integer",
                Details = new Dictionary<string, string[]>
                {
                    [field] = ["must be a 32-bit integer"]
                }
            });
}
