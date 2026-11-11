namespace Railway.Toolkit.Demo.Api.Features.Examples.Branching;

public enum DeliveryService { DomesticStandard, DomesticExpress, International }
public sealed record DeliveryQuoteRequest(decimal WeightKg, DeliveryService? Service, string? CustomsDescription = null);
public sealed record DeliveryQuoteResponse(DeliveryService Service, decimal Price, string Currency, int EstimatedDays);
