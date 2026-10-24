using Scalar.AspNetCore;
using Urban.Api.Endpoints;
using Urban.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddUrbanModules(builder.Configuration);

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateModulesAsync();
}

app.MapControllers();
app.MapHealthChecks("/health");
app.MapVersionEndpoint();
app.MapAuthEndpoints();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

await app.RunAsync();