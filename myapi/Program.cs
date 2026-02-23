using Prometheus;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog.Enrichers.Span;

// Nome do serviço que aparecerá no Grafana
var serviceName = "my-api-dotnet";

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithSpan() // <--- Adiciona TraceId e SpanId automaticamente aos logs
    .Filter.ByExcluding(logEvent =>
        logEvent.Properties.ContainsKey("RequestPath") &&
        logEvent.Properties["RequestPath"].ToString().Contains("/metrics")) // Ignora o Prometheus
    .WriteTo.Console()
    .WriteTo.GrafanaLoki("http://localhost:3100",
        new[] { new LokiLabel { Key = "application", Value = "my-api-dotnet" } })
    .CreateLogger();


builder.Host.UseSerilog(); // Substitui o logging padrão pelo Serilog

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(serviceName)
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
        .AddAspNetCoreInstrumentation(options =>
        {
            // Ignora o rastreio do endpoint de métricas
            options.Filter = (httpContext) => !httpContext.Request.Path.StartsWithSegments("/metrics");
        }) // Rastreia automaticamente todas as requisições HTTP
        .AddHttpClientInstrumentation() // Rastreia chamadas externas (se você fizer)
        .AddOtlpExporter(opt =>
        {
            opt.Endpoint = new Uri("http://localhost:4317"); // Endereço do nosso container Tempo
        }));

var app = builder.Build();

// Habilita a coleta de métricas padrão do ASP.NET Core
app.UseHttpMetrics();

app.MapGet("/", (ILogger<Program> logger) =>
{
    logger.LogInformation("Endpoint principal acessado às {Time}", DateTime.Now);
    return "Logs gerados!";
});

// Expõe o endpoint /metrics para o Prometheus ler (scrape)
app.MapMetrics();

app.Run("http://localhost:5000");

