using Microsoft.AspNetCore.Mvc;

namespace Railway.Toolkit.Demo.Api.Features.Examples.Recovery;

[ApiController]
[Route("api/examples/recovery")]
public sealed class RecoveryExamplesController(RecoveryExamplesHandler handler) : ControllerBase
{
    [HttpPost("default")]
    [EndpointSummary("OrElse: recover with a fixed default value")]
    [EndpointDescription("Send {\"value\":\"42\"} to keep the primary value, or {\"value\":\"bad\"} to recover with 10. Missing/null values also use the default. Source identifies primary versus default. This is a parsing example, not a change to application configuration.")]
    [ProducesResponseType<RecoveryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public IActionResult WithDefault(DefaultValueRequest request) =>
        handler.WithDefault(request).Match<IActionResult>(
            ok => Ok(ok.Value), _ => StatusCode(StatusCodes.Status500InternalServerError));

    [HttpPost("alternative")]
    [EndpointSummary("OrElseWith: try another operation that can also fail")]
    [EndpointDescription("Send {\"preferred\":\"bad\",\"fallback\":\"7\"} to recover. A valid preferred value bypasses fallback parsing. If both are invalid, returns only the fallback error with 422; failures are not aggregated. Source identifies primary versus fallback. Integer parsing is invariant and this example has no side effects.")]
    [ProducesResponseType<RecoveryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<Error>(StatusCodes.Status422UnprocessableEntity)]
    public IActionResult WithAlternative(AlternativeValueRequest request) =>
        handler.WithAlternative(request).Match<IActionResult>(
            ok => Ok(ok.Value), fail => UnprocessableEntity(fail.Error));
}
