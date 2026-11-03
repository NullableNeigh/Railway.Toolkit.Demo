using Railway.Toolkit.Demo.Api.Features.Examples.Collections;
using System.Text.Json.Serialization;
using Railway.Toolkit.Demo.Api.Features.Tasks;
using Railway.Toolkit.Demo.Api.Features.Tasks.BatchCreate;
using Railway.Toolkit.Demo.Api.Features.Tasks.Complete;
using Railway.Toolkit.Demo.Api.Features.Tasks.Create;
using Railway.Toolkit.Demo.Api.Features.Tasks.Edit;
using Railway.Toolkit.Demo.Api.Features.Tasks.Lookup;
using Railway.Toolkit.Demo.Api.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<ITaskStore, TaskStore>();
builder.Services.AddScoped<TaskValidation>();
builder.Services.AddScoped<CollectionExamplesHandler>();
builder.Services.AddScoped<CreateTaskHandler>();
builder.Services.AddScoped<LookupTaskHandler>();
builder.Services.AddScoped<EditTaskHandler>();
builder.Services.AddScoped<CompleteTaskHandler>();
builder.Services.AddScoped<BatchCreateTaskHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Railway.Toolkit.Demo",
        Version = "v1",
        Description = "An in-memory task API showcasing Railway-Oriented Programming with Railway.Toolkit."
    });
});

WebApplication app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.MapControllers();
app.Run();

// Expose the entry point to the HTTP integration test host.
public partial class Program;
