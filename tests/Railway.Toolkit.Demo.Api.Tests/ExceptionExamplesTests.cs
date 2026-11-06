using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Railway.Toolkit;
using Railway.Toolkit.Demo.Api.Features.Examples.Exceptions;

namespace Railway.Toolkit.Demo.Api.Tests;

public sealed class ExceptionExamplesTests
{
    private readonly Base64DecodeHandler _handler = new();

    [Theory]
    [InlineData("SGVsbG8=", 5, "48656C6C6F")]
    [InlineData("AAH/", 3, "0001FF")]
    [InlineData("", 0, "")]
    [InlineData(" \r\n", 0, "")]
    [InlineData("SGVs bG8=\r\n", 5, "48656C6C6F")]
    public void Valid_Base64_projects_decoded_bytes(string value, int count, string hex)
    {
        Base64DecodeResponse response = Ok(_handler.Handle(new(value)));
        Assert.Equal(count, response.ByteCount);
        Assert.Equal(hex, response.Hex);
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("a")]
    [InlineData("====")]
    [InlineData("SGVsbG8===")]
    public void Format_exceptions_become_stable_failures_without_exception_objects(string value)
    {
        Error error = Fail(_handler.Handle(new(value)));
        Assert.Equal("Example.InvalidBase64", error.Code);
        Assert.Equal("Value must be valid Base64", error.Message);
        Assert.Null(error.Exception);
    }

    [Fact]
    public void Null_value_stays_on_the_guard_failure_track()
    {
        Error error = Fail(_handler.Handle(new(null)));
        Assert.Equal("Example.ValueRequired", error.Code);
        Assert.Null(error.Exception);
    }

    [Theory]
    [InlineData("SGVsbG8=", 5, "48656C6C6F")]
    [InlineData("AAH/", 3, "0001FF")]
    public async Task Http_success_returns_byte_count_and_hex(string value, int count, string hex)
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/examples/exceptions/decode-base64", new { value }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await Read(response);
        Assert.Equal(count, body.GetProperty("byteCount").GetInt32());
        Assert.Equal(hex, body.GetProperty("hex").GetString());
        Assert.Equal("[]", await client.GetStringAsync("/api/tasks", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Http_invalid_Base64_returns_a_serializable_error_without_internals()
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/examples/exceptions/decode-base64", new { value = "not base64!" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        JsonElement error = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.Equal("Example.InvalidBase64", error.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, error.GetProperty("exception").ValueKind);
        Assert.DoesNotContain("not base64!", json);
        Assert.DoesNotContain("FormatException", json);
        Assert.DoesNotContain("StackTrace", json);
    }

    [Fact]
    public async Task Http_missing_value_returns_the_guard_failure()
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/examples/exceptions/decode-base64", new { }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("Example.ValueRequired", (await Read(response)).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"value\":10}")]
    public async Task Invalid_transport_input_returns_400(string body)
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpResponseMessage response = await client.PostAsync("/api/examples/exceptions/decode-base64",
            new StringContent(body, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_describes_exception_conversion_and_typed_responses()
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        JsonElement swagger = JsonSerializer.Deserialize<JsonElement>(
            await client.GetStringAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken));
        JsonElement operation = swagger.GetProperty("paths")
            .GetProperty("/api/examples/exceptions/decode-base64").GetProperty("post");
        Assert.Contains("TryMap", operation.GetProperty("summary").GetString());
        JsonElement responses = operation.GetProperty("responses");
        Assert.Contains("Base64DecodeResponse", responses.GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
        Assert.True(responses.TryGetProperty("400", out _));
        Assert.True(responses.TryGetProperty("422", out _));
        Assert.True(responses.TryGetProperty("500", out _));
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
