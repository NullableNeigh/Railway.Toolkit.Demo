using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Railway.Toolkit;
using Railway.Toolkit.Demo.Api.Features.Examples.Collections;

namespace Railway.Toolkit.Demo.Api.Tests;

public sealed class CollectionExamplesTests
{
    private readonly CollectionExamplesHandler _handler = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Valid_integers_keep_input_order_in_both_modes(bool collectAll)
    {
        Result<IReadOnlyList<int>> result = Process(["12", " -3 ", "0", "+4", "-2147483648", "2147483647"], collectAll);
        Assert.Equal(new[] { 12, -3, 0, 4, int.MinValue, int.MaxValue }, Ok(result));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Empty_input_is_a_successful_empty_collection(bool collectAll) =>
        Assert.Empty(Ok(Process([], collectAll)));

    [Fact]
    public void Traverse_returns_the_first_invalid_input_index()
    {
        Error error = Fail(_handler.Traverse(["10", "bad", "20", "also bad"]));
        Assert.Equal("Example.InvalidInteger", error.Code);
        Assert.Equal("Items[1]", Assert.Single(error.Details!).Key);
        Assert.Null(error.InnerErrors);
    }

    [Fact]
    public void TraverseAll_returns_indexed_errors_in_input_order_without_partial_values()
    {
        Result<IReadOnlyList<int>> result = _handler.TraverseAll(["10", "bad", "20", "also bad"]);
        Error error = Fail(result);
        Assert.Equal("AggregateError", error.Code);
        Assert.Equal(new[] { "Items[1]", "Items[3]" }, error.Details!.Keys);
        Assert.Equal(new[] { "Items[1]", "Items[3]" },
            error.InnerErrors!.Select(inner => Assert.Single(inner.Details!).Key));
    }

    [Fact]
    public void TraverseAll_preserves_a_single_error_instead_of_wrapping_it()
    {
        Error error = Fail(_handler.TraverseAll(["10", "bad", "20"]));
        Assert.Equal("Example.InvalidInteger", error.Code);
        Assert.Null(error.InnerErrors);
        Assert.Equal("Items[1]", Assert.Single(error.Details!).Key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("2147483648")]
    [InlineData("1.5")]
    [InlineData("1,000")]
    public void Invalid_integer_text_is_an_item_failure(string? input) =>
        Assert.Equal("Example.InvalidInteger", Fail(_handler.Traverse([input])).Code);

    [Theory]
    [InlineData("traverse")]
    [InlineData("traverse-all")]
    public async Task Http_success_returns_numbers_and_does_not_create_tasks(string mode)
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/examples/collections/{mode}", new[] { "10", "-2", "3" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new[] { 10, -2, 3 },
            await response.Content.ReadFromJsonAsync<int[]>(TestContext.Current.CancellationToken));
        Assert.Equal("[]", await client.GetStringAsync("/api/tasks", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("traverse", "Example.InvalidInteger", 1)]
    [InlineData("traverse-all", "AggregateError", 2)]
    public async Task Http_failures_expose_the_selected_collection_semantics(string mode, string code, int errorCount)
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/examples/collections/{mode}", new string?[] { "10", null, "20", "bad" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        JsonElement error = await Read(response);
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Equal(errorCount, error.GetProperty("details").EnumerateObject().Count());
        Assert.True(error.GetProperty("details").TryGetProperty("Items[1]", out _));
        if (errorCount == 2)
        {
            Assert.True(error.GetProperty("details").TryGetProperty("Items[3]", out _));
            Assert.Equal(2, error.GetProperty("innerErrors").GetArrayLength());
        }
        Assert.Equal("[]", await client.GetStringAsync("/api/tasks", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("[10]")]
    public async Task Invalid_transport_input_returns_400(string body)
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpResponseMessage response = await client.PostAsync("/api/examples/collections/traverse",
            new StringContent(body, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_explains_the_two_modes_and_their_responses()
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        JsonElement swagger = JsonSerializer.Deserialize<JsonElement>(
            await client.GetStringAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken));
        foreach (string mode in new[] { "traverse", "traverse-all" })
        {
            JsonElement operation = swagger.GetProperty("paths")
                .GetProperty($"/api/examples/collections/{mode}").GetProperty("post");
            Assert.Contains("Traverse", operation.GetProperty("summary").GetString());
            Assert.True(operation.GetProperty("responses").TryGetProperty("200", out _));
            Assert.True(operation.GetProperty("responses").TryGetProperty("422", out _));
            Assert.True(operation.GetProperty("responses").TryGetProperty("400", out _));
        }
    }

    private Result<IReadOnlyList<int>> Process(string?[] items, bool collectAll) =>
        collectAll ? _handler.TraverseAll(items) : _handler.Traverse(items);

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
