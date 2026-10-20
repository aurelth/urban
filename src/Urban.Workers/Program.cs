// Host de background: jobs do Hangfire e consumers do MassTransit entram aqui.
// Usa o SDK Web para expor /health e /metrics (monitoramento) sem pacotes extras.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

await app.RunAsync();
