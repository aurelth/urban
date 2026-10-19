// Gateway (YARP): roteamento, validação de JWT e rate limiting entram aqui.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
