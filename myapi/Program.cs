using Prometheus;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Habilita a coleta de métricas padrão do ASP.NET Core
app.UseHttpMetrics();

app.MapGet("/", () => "Olá! Gere algumas métricas acessando este endpoint.");

// Expõe o endpoint /metrics para o Prometheus ler (scrape)
app.MapMetrics();

app.Run("http://localhost:5000");

