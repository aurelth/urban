using Scalar.AspNetCore;
using Urban.Api.Endpoints;
using Urban.Api.Extensions;
using Urban.Api.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SessionStoreExceptionHandler>();
builder.Services.AddUrbanOpenApi();
builder.Services.AddUrbanModules(builder.Configuration);
builder.Services.AddUrbanAuthentication();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateModulesAsync();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapVersionEndpoint();
app.MapAuthEndpoints();
app.MapRegistrationEndpoints();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

await app.RunAsync();