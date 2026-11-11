namespace Railway.Toolkit.Demo.Api.Features.Examples.Branching;

public sealed class DeliveryQuoteHandler
{
    public Result<DeliveryQuoteResponse> Handle(DeliveryQuoteRequest request) =>
        ValidateParcel(request)
            // Both are valid business routes. Failure is not a signal to choose the other one.
            .Bind(parcel => parcel.Service == DeliveryService.International
                ? QuoteInternational(parcel)
                : QuoteDomestic(parcel))
            .Map(quote => new DeliveryQuoteResponse(quote.Service, quote.Price, "GBP", quote.EstimatedDays));

    private static Result<DeliveryQuoteRequest> ValidateParcel(DeliveryQuoteRequest request) =>
        Validation.Combine(new[]
        {
            Result.Ok(request.WeightKg)
                .GreaterThan(0m, "WeightKg")
                .Ensure(weight => weight <= 20m, "Parcel must weigh at most 20 kg", "Validation.Range",
                    new Dictionary<string, string[]> { ["WeightKg"] = ["must be at most 20 kg"] })
                .ToUnit(),
            Result.Ok(request.Service)
                .Ensure(service => service.HasValue && Enum.IsDefined(service.Value),
                    "A valid delivery service is required", "Validation.Required",
                    new Dictionary<string, string[]> { ["Service"] = ["must be a valid delivery service"] })
                .ToUnit()
        }, () => request);

    private static Result<DeliveryQuote> QuoteDomestic(DeliveryQuoteRequest parcel) =>
        parcel.Service == DeliveryService.DomesticExpress
            ? QuoteExpress(parcel)
            : Result.Ok(new DeliveryQuote(DeliveryService.DomesticStandard, 5m, 3));

    private static Result<DeliveryQuote> QuoteExpress(DeliveryQuoteRequest parcel) =>
        Result.Ok(parcel)
            .Ensure(current => current.WeightKg <= 5m,
                "Domestic express is limited to parcels weighing at most 5 kg", "Delivery.ExpressWeightLimit",
                new Dictionary<string, string[]> { ["WeightKg"] = ["must be at most 5 kg for domestic express"] })
            .Map(_ => new DeliveryQuote(DeliveryService.DomesticExpress, 12m, 1));

    private static Result<DeliveryQuote> QuoteInternational(DeliveryQuoteRequest parcel) =>
        Result.Ok(parcel.CustomsDescription ?? string.Empty)
            .NotNullOrWhiteSpace("CustomsDescription")
            .Map(_ => new DeliveryQuote(DeliveryService.International, 25m, 7));

    private sealed record DeliveryQuote(DeliveryService Service, decimal Price, int EstimatedDays);
}
