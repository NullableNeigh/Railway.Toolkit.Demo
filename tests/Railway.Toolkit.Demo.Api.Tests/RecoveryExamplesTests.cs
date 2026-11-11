using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Railway.Toolkit;
using Railway.Toolkit.Demo.Api.Features.Examples.Recovery;

namespace Railway.Toolkit.Demo.Api.Tests;

public sealed class RecoveryExamplesTests
{
    private readonly RecoveryExamplesHandler _handler = new();

    [Theory]
    [InlineData("42", 42, "primary")]
    [InlineData(" -2 ", -2, "primary")]
    [InlineData("0", 0, "primary")]
    [InlineData("bad", 10, "default")]
    [InlineData(null, 10, "default")]
    public void Default_keeps_primary_success_or_recovers_with_10(string? text, int value, string source)
    {
        RecoveryResponse response = Ok(_handler.WithDefault(new(text)));
        Assert.Equal(value, response.Value);
        Assert.Equal(source, response.Source);
    }

    [Theory]
    [InlineData("42", "bad", 42, "primary")]
    [InlineData("bad", "7", 7, "fallback")]
    [InlineData(null, " -3 ", -3, "fallback")]
    public void Alternative_keeps_primary_success_or_uses_fallback(string? preferred, string? fallback, int value, string source)
    {
        RecoveryResponse response = Ok(_handler.WithAlternative(new(preferred, fallback)));
        Assert.Equal(value, response.Value);
        Assert.Equal(source, response.Source);
    }

    [Theory]
    [InlineData("bad", "also bad")]
    [InlineData(null, null)]
    public void Both_failures_return_only_the_fallback_error(string? preferred, string? fallback)
    {
        Error error = Fail(_handler.WithAlternative(new(preferred, fallback)));
        Assert.Equal("Example.InvalidInteger", error.Code);
        Assert.Equal("Fallback must be a 32-bit integer", error.Message);
        Assert.Equal("Fallback", Assert.Single(error.Details!).Key);
        Assert.Null(error.InnerErrors);
    }

    [Theory]
    [InlineData("42", 42, "primary")]
    [InlineData("bad", 10, "default")]
    public async Task Http_default_exposes_the_chosen_value_and_source(string text, int value, string source)
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/examples/recovery/default", new { value = text }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertValue(response, value, source);
        Assert.Equal("[]", await client.GetStringAsync("/api/tasks", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("42", "bad", 42, "primary")]
    [InlineData("bad", "7", 7, "fallback")]
    public async Task Http_alternative_exposes_primary_or_recovered_success(string preferred, string fallback, int value, string source)
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/examples/recovery/alternative", new { preferred, fallback }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertValue(response, value, source);
        Assert.Equal("[]", await client.GetStringAsync("/api/tasks", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Http_alternative_can_remain_a_failure()
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/examples/recovery/alternative", new { preferred = "bad", fallback = "also bad" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        JsonElement error = await Read(response);
        Assert.Equal("Example.InvalidInteger", error.GetProperty("code").GetString());
        Assert.True(error.GetProperty("details").TryGetProperty("Fallback", out _));
        Assert.False(error.GetProperty("details").TryGetProperty("Preferred", out _));
        Assert.Equal(JsonValueKind.Null, error.GetProperty("innerErrors").ValueKind);
    }

    [Theory]
    [InlineData("default", "null")]
    [InlineData("alternative", "{")]
    [InlineData("default", "{\"value\":10}")]
    public async Task Invalid_transport_input_returns_400_without_recovery(string mode, string body)
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpResponseMessage response = await client.PostAsync($"/api/examples/recovery/{mode}",
            new StringContent(body, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_explains_the_different_recovery_contracts()
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        JsonElement swagger = JsonSerializer.Deserialize<JsonElement>(
            await client.GetStringAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken));
        JsonElement paths = swagger.GetProperty("paths");
        JsonElement defaultOperation = paths.GetProperty("/api/examples/recovery/default").GetProperty("post");
        Assert.Contains("OrElse:", defaultOperation.GetProperty("summary").GetString());
        Assert.False(defaultOperation.GetProperty("responses").TryGetProperty("422", out _));
        JsonElement alternativeOperation = paths.GetProperty("/api/examples/recovery/alternative").GetProperty("post");
        Assert.Contains("OrElseWith:", alternativeOperation.GetProperty("summary").GetString());
        Assert.True(alternativeOperation.GetProperty("responses").TryGetProperty("422", out _));
        foreach (JsonElement operation in new[] { defaultOperation, alternativeOperation })
        {
            Assert.True(operation.GetProperty("responses").TryGetProperty("400", out _));
            Assert.Contains("RecoveryResponse", operation.GetProperty("responses").GetProperty("200")
                .GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
        }
    }

    private static T Ok<T>(Result<T> result) => Assert.IsType<Result<T>.Ok>(result).Value;
    private static Error Fail<T>(Result<T> result) => Assert.IsType<Result<T>.Fail>(result).Error;
    private static async Task<JsonElement> Read(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

    private static async Task AssertValue(HttpResponseMessage response, int value, string source)
    {
        JsonElement body = await Read(response);
        Assert.Equal(value, body.GetProperty("value").GetInt32());
        Assert.Equal(source, body.GetProperty("source").GetString());
    }

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
