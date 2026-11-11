using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Railway.Toolkit;
using Railway.Toolkit.Demo.Api.Features.Examples.Branching;

namespace Railway.Toolkit.Demo.Api.Tests;

public sealed class BranchingExamplesTests
{
    private readonly DeliveryQuoteHandler _handler = new();

    [Theory]
    [InlineData(DeliveryService.DomesticStandard, 5, 3, null)]
    [InlineData(DeliveryService.DomesticExpress, 12, 1, null)]
    [InlineData(DeliveryService.International, 25, 7, "Books")]
    public void Selected_route_rejoins_the_common_response(DeliveryService service, int price, int days, string? customs)
    {
        DeliveryQuoteResponse quote = Ok(_handler.Handle(new(2m, service, customs)));
        Assert.Equal(service, quote.Service);
        Assert.Equal((decimal)price, quote.Price);
        Assert.Equal(days, quote.EstimatedDays);
        Assert.Equal("GBP", quote.Currency);
    }

    [Theory]
    [InlineData(DeliveryService.DomesticExpress)]
    [InlineData(DeliveryService.International)]
    public void Common_validation_failure_skips_branch_specific_validation(DeliveryService service)
    {
        Error error = Fail(_handler.Handle(new(21m, service)));
        Assert.Equal("Validation.Failed", error.Code);
        Assert.Equal("WeightKg", Assert.Single(error.Details!).Key);
    }

    [Fact]
    public void Common_validation_collects_invalid_weight_and_missing_service()
    {
        Error error = Fail(_handler.Handle(new(0m, null)));
        Assert.Equal(new[] { "Service", "WeightKg" }, error.Details!.Keys.Order());
    }

    [Fact]
    public void Express_failure_does_not_fall_back_to_standard()
    {
        Error error = Fail(_handler.Handle(new(6m, DeliveryService.DomesticExpress)));
        Assert.Equal("Delivery.ExpressWeightLimit", error.Code);
        Assert.Equal("WeightKg", Assert.Single(error.Details!).Key);
        Assert.Equal(DeliveryService.DomesticStandard,
            Ok(_handler.Handle(new(6m, DeliveryService.DomesticStandard))).Service);
    }

    [Theory]
    [InlineData(DeliveryService.DomesticStandard, 20)]
    [InlineData(DeliveryService.DomesticExpress, 5)]
    public void Weight_limits_are_inclusive(DeliveryService service, int weight) =>
        Assert.Equal(service, Ok(_handler.Handle(new(weight, service))).Service);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void International_route_requires_customs_information(string? customs)
    {
        Error error = Fail(_handler.Handle(new(2m, DeliveryService.International, customs)));
        Assert.Equal("Validation.Required", error.Code);
        Assert.Equal("CustomsDescription", Assert.Single(error.Details!).Key);
    }

    [Theory]
    [InlineData(DeliveryService.DomesticStandard, 5, 3, null)]
    [InlineData(DeliveryService.DomesticExpress, 12, 1, null)]
    [InlineData(DeliveryService.International, 25, 7, "Books")]
    public async Task Http_returns_the_selected_quote(DeliveryService service, int price, int days, string? customs)
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/examples/branching/delivery-quote",
            new { weightKg = 2, service = service.ToString(), customsDescription = customs }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement quote = await Read(response);
        Assert.Equal(service.ToString(), quote.GetProperty("service").GetString());
        Assert.Equal((decimal)price, quote.GetProperty("price").GetDecimal());
        Assert.Equal(days, quote.GetProperty("estimatedDays").GetInt32());
        Assert.Equal("GBP", quote.GetProperty("currency").GetString());
        Assert.Equal("[]", await client.GetStringAsync("/api/tasks", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("DomesticExpress", 6, "Delivery.ExpressWeightLimit")]
    [InlineData("International", 2, "Validation.Required")]
    public async Task Http_branch_failure_propagates_without_selecting_another_route(string service, int weight, string code)
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/examples/branching/delivery-quote", new { weightKg = weight, service }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(code, (await Read(response)).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"weightKg\":2,\"service\":10}")]
    [InlineData("{\"weightKg\":2,\"service\":\"Unknown\"}")]
    public async Task Invalid_transport_input_returns_400(string body)
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpResponseMessage response = await client.PostAsync("/api/examples/branching/delivery-quote",
            new StringContent(body, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_describes_branching_and_typed_responses()
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        JsonElement swagger = JsonSerializer.Deserialize<JsonElement>(
            await client.GetStringAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken));
        JsonElement operation = swagger.GetProperty("paths")
            .GetProperty("/api/examples/branching/delivery-quote").GetProperty("post");
        Assert.Contains("Bind", operation.GetProperty("summary").GetString());
        Assert.Contains("without fallback", operation.GetProperty("description").GetString());
        JsonElement responses = operation.GetProperty("responses");
        Assert.Contains("DeliveryQuoteResponse", responses.GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
        Assert.True(responses.TryGetProperty("400", out _));
        Assert.True(responses.TryGetProperty("422", out _));
    }

    private static T Ok<T>(Result<T> result) => Assert.IsType<Result<T>.Ok>(result).Value;
    private static Error Fail<T>(Result<T> result) => Assert.IsType<Result<T>.Fail>(result).Error;
    private static async Task<JsonElement> Read(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureServices(services => services.AddLogging(logging => logging.ClearProviders()));

        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);
            client.BaseAddress = new Uri("https://localhost");
        }
    }
}
