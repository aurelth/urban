using Microsoft.AspNetCore.Http;
using Scalar.AspNetCore;
using Urban.Api.Endpoints;
using Urban.Api.Extensions;
using Urban.Api.Http;
using Urban.BackgroundJobs;
using Urban.EmailDelivery;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SessionStoreExceptionHandler>();
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddUrbanOpenApi();
builder.Services.AddEmailDelivery(builder.Configuration);
builder.Services.AddUrbanModules(builder.Configuration);
builder.Services.AddBackgroundJobs(builder.Configuration);
builder.Services.AddUrbanAuthentication();
builder.Services.AddUrbanForwardedHeaders(builder.Configuration);
builder.Services.AddUrbanRateLimiting(builder.Configuration);

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateModulesAsync();
}

await app.Services.BootstrapSuperAdminAsync(app.Configuration);

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapVersionEndpoint();
app.MapAuthEndpoints();
app.MapTermsEndpoints();
app.MapRegistrationEndpoints();
app.MapCondominiumEndpoints();
app.MapPasswordResetEndpoints();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

await app.RunAsync();