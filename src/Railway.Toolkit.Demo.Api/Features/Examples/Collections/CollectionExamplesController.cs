using Microsoft.AspNetCore.Mvc;

namespace Railway.Toolkit.Demo.Api.Features.Examples.Collections;

[ApiController]
[Route("api/examples/collections")]
public sealed class CollectionExamplesController(CollectionExamplesHandler handler) : ControllerBase
{
    [HttpPost("traverse")]
    [EndpointSummary("Traverse: stop at the first invalid item")]
    [EndpointDescription("Send integer strings, such as [\"10\",\"bad\",\"20\",\"also bad\"]. Returns all numbers on success, or only the first error. Input indexes are zero-based. This example has no side effects.")]
    [ProducesResponseType<int[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<Error>(StatusCodes.Status422UnprocessableEntity)]
    public IActionResult Traverse([FromBody] string?[] items) =>
        handler.Traverse(items).Match<IActionResult>(
            ok => Ok(ok.Value), fail => UnprocessableEntity(fail.Error));

    [HttpPost("traverse-all")]
    [EndpointSummary("TraverseAll: collect every invalid item")]
    [EndpointDescription("Send the same integer strings to compare with Traverse. Returns all numbers on success, or all errors without partial values. A single error is preserved; multiple errors are aggregated. An empty array succeeds. Compare task batch creation for partial-success outcomes.")]
    [ProducesResponseType<int[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<Error>(StatusCodes.Status422UnprocessableEntity)]
    public IActionResult TraverseAll([FromBody] string?[] items) =>
        handler.TraverseAll(items).Match<IActionResult>(
            ok => Ok(ok.Value), fail => UnprocessableEntity(fail.Error));
}
