using Microsoft.AspNetCore.Mvc;

namespace Railway.Toolkit.Demo.Api.Features.Examples.Branching;

[ApiController]
[Route("api/examples/branching")]
public sealed class BranchingExamplesController(DeliveryQuoteHandler handler) : ControllerBase
{
    [HttpPost("delivery-quote")]
    [EndpointSummary("Bind: choose a business route and rejoin the pipeline")]
    [EndpointDescription("Send {\"weightKg\":2,\"service\":\"DomesticStandard\"}, DomesticExpress, or International with customsDescription. Parcels must weigh more than zero and at most 20 kg. Express is limited to 5 kg; international requires a nonblank customs description. Domestic routes ignore customs information. Illustrative quotes: standard GBP 5 / 3 days, express GBP 12 / 1 day, international GBP 25 / 7 days. Exactly one route runs; branch failures propagate without fallback. No carrier, payment or stored state is involved.")]
    [ProducesResponseType<DeliveryQuoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<Error>(StatusCodes.Status422UnprocessableEntity)]
    public IActionResult DeliveryQuote(DeliveryQuoteRequest request) =>
        handler.Handle(request).Match<IActionResult>(
            ok => Ok(ok.Value), fail => UnprocessableEntity(fail.Error));
}
