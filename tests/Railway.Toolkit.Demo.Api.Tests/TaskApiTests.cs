using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Railway.Toolkit.Demo.Api.Tests;

public sealed class TaskApiTests
{
    [Fact]
    public async Task Task_lifecycle_has_working_location_and_state_conflicts()
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        Assert.Equal("[]", await client.GetStringAsync("/api/tasks", cancellationToken: TestContext.Current.CancellationToken));
        using HttpResponseMessage created = await client.PostAsJsonAsync("/api/tasks", new { title = "Learn Railway" }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        JsonElement task = await Read(created);
        int id = task.GetProperty("id").GetInt32();
        Assert.Equal("Open", task.GetProperty("status").GetString());
        Assert.EndsWith($"/api/tasks/{id}", created.Headers.Location!.ToString());
        using HttpResponseMessage fetched = await client.GetAsync(created.Headers.Location, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(id, (await Read(fetched)).GetProperty("id").GetInt32());

        using HttpResponseMessage edited = await client.PutAsJsonAsync($"/api/tasks/{id}", new { title = "Updated" }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal("Updated", (await Read(edited)).GetProperty("title").GetString());
        using HttpResponseMessage completed = await client.PostAsync($"/api/tasks/{id}/complete", null, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal("Completed", (await Read(completed)).GetProperty("status").GetString());
        using HttpResponseMessage repeat = await client.PostAsync($"/api/tasks/{id}/complete", null, cancellationToken: TestContext.Current.CancellationToken);
        await AssertError(repeat, HttpStatusCode.Conflict, "Task.AlreadyCompleted");
        using HttpResponseMessage blockedEdit = await client.PutAsJsonAsync($"/api/tasks/{id}", new { title = "Blocked" }, cancellationToken: TestContext.Current.CancellationToken);
        await AssertError(blockedEdit, HttpStatusCode.Conflict, "Task.AlreadyCompleted");
        Assert.Equal("[]", await client.GetStringAsync("/api/tasks?status=Open", cancellationToken: TestContext.Current.CancellationToken));
        JsonElement completedList = JsonDocument.Parse(await client.GetStringAsync("/api/tasks?status=Completed", cancellationToken: TestContext.Current.CancellationToken)).RootElement;
        Assert.Equal(id, Assert.Single(completedList.EnumerateArray()).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Domain_validation_returns_all_field_details_without_creating_a_task()
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/tasks",
            new { title = " ", description = new string('x', 1001), dueDate = "2000-01-01" }, cancellationToken: TestContext.Current.CancellationToken);
        await AssertError(response, HttpStatusCode.UnprocessableEntity, "Validation.Failed");
        JsonElement details = (await Read(response)).GetProperty("details");
        Assert.Equal(new[] { "Description", "DueDate", "Title" }, details.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal("[]", await client.GetStringAsync("/api/tasks", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("{}", 422)]
    [InlineData("{\"title\":null}", 422)]
    [InlineData("null", 400)]
    [InlineData("{", 400)]
    [InlineData("{\"title\":\"Task\",\"dueDate\":\"invalid\"}", 400)]
    public async Task Invalid_request_shapes_and_domain_failures_are_distinguished(string body, int status)
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpResponseMessage response = await client.PostAsync("/api/tasks", new StringContent(body, Encoding.UTF8, "application/json"), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(status, (int)response.StatusCode);
    }

    [Theory]
    [InlineData("/api/tasks/999", "GET")]
    [InlineData("/api/tasks/999", "PUT")]
    [InlineData("/api/tasks/999/complete", "POST")]
    public async Task Missing_tasks_return_structured_404(string path, string method)
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpRequestMessage request = new(new HttpMethod(method), path);
        if (method == "PUT") request.Content = JsonContent.Create(new { title = "Task" });
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken: TestContext.Current.CancellationToken);
        await AssertError(response, HttpStatusCode.NotFound, "Task.NotFound");
    }

    [Fact]
    public async Task Invalid_status_filter_returns_400()
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/api/tasks?status=Unknown", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Batch_reports_ordered_partial_outcomes_and_persists_only_successes()
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/tasks/batch",
            new object?[] { new { title = "First" }, new { title = "" }, null, new { title = "Last" } }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement batch = await Read(response);
        Assert.Equal(4, batch.GetProperty("total").GetInt32());
        Assert.Equal(2, batch.GetProperty("succeeded").GetInt32());
        Assert.Equal(2, batch.GetProperty("failed").GetInt32());
        JsonElement[] items = batch.GetProperty("results").EnumerateArray().ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3 }, items.Select(item => item.GetProperty("index").GetInt32()));
        Assert.Equal("First", items[0].GetProperty("task").GetProperty("title").GetString());
        Assert.Equal("Validation.Failed", items[1].GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("Task.InvalidItem", items[2].GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("Last", items[3].GetProperty("task").GetProperty("title").GetString());
        JsonElement tasks = JsonDocument.Parse(await client.GetStringAsync("/api/tasks", cancellationToken: TestContext.Current.CancellationToken)).RootElement;
        Assert.Equal(2, tasks.GetArrayLength());
    }

    [Fact]
    public async Task Empty_batch_is_a_validation_failure()
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/tasks/batch", Array.Empty<object>(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.StartsWith("Validation.", (await Read(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Swagger_describes_task_contracts_and_user_routes_are_absent()
    {
        using ApiFactory host = new();
        using HttpClient client = host.CreateClient();
        JsonElement swagger = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json", cancellationToken: TestContext.Current.CancellationToken)).RootElement;
        JsonElement paths = swagger.GetProperty("paths");
        JsonElement responses = paths.GetProperty("/api/tasks").GetProperty("post").GetProperty("responses");
        Assert.True(responses.TryGetProperty("201", out JsonElement created));
        Assert.True(responses.TryGetProperty("422", out _));
        Assert.Contains("TodoTask", created.GetProperty("content").GetProperty("application/json")
            .GetProperty("schema").GetProperty("$ref").GetString());
        Assert.True(paths.GetProperty("/api/tasks/{id}/complete").GetProperty("post").GetProperty("responses").TryGetProperty("409", out _));
        Assert.DoesNotContain(paths.EnumerateObject(), path => path.Name.StartsWith("/api/users"));
        Assert.Contains("Swagger UI", await client.GetStringAsync("/swagger/index.html", cancellationToken: TestContext.Current.CancellationToken));
        using HttpResponseMessage oldRoute = await client.GetAsync("/api/users", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, oldRoute.StatusCode);
    }

    private static async Task<JsonElement> Read(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken: TestContext.Current.CancellationToken)).RootElement;

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await Read(response)).GetProperty("code").GetString());
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
