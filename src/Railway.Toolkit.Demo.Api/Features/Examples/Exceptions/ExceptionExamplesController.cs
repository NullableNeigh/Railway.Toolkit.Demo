using Microsoft.AspNetCore.Mvc;

namespace Railway.Toolkit.Demo.Api.Features.Examples.Exceptions;

[ApiController]
[Route("api/examples/exceptions")]
public sealed class ExceptionExamplesController(Base64DecodeHandler handler) : ControllerBase
{
    [HttpPost("decode-base64")]
    [EndpointSummary("TryMap: convert a Base64 decoding exception into a failure")]
    [EndpointDescription("Send {\"value\":\"SGVsbG8=\"} for five bytes (hex 48656C6C6F), or {\"value\":\"not base64!\"} for a structured 422 failure. Missing/null Value fails before decoding. Empty or whitespace-only Base64 represents zero bytes. The custom exception mapper returns stable errors without retaining exception details. This example has no side effects.")]
    [ProducesResponseType<Base64DecodeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<Error>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult DecodeBase64(Base64DecodeRequest request) =>
        handler.Handle(request).Match<IActionResult>(
            ok => Ok(ok.Value),
            fail => fail.Error.Code is "Example.ValueRequired" or "Example.InvalidBase64"
                ? UnprocessableEntity(fail.Error)
                : StatusCode(StatusCodes.Status500InternalServerError));
}
