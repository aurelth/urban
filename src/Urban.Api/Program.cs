using Urban.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapVersionEndpoint();

app.Run();

// Necessário para o WebApplicationFactory<Program> dos testes de integração.
public partial class Program;
