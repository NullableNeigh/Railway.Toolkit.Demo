using Railway.Toolkit.Demo.Api.Features.Users.Login;
using Railway.Toolkit.Demo.Api.Features.Users.Lookup;
using Railway.Toolkit.Demo.Api.Features.Users.Register;
using Railway.Toolkit.Demo.Api.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<IUserStore, UserStore>();
builder.Services.AddScoped<RegisterHandler>();
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<LookupHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Railway.Toolkit.Demo",
        Version = "v1",
        Description = "A demonstration API showcasing Railway-Oriented Programming with Railway.Toolkit."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
