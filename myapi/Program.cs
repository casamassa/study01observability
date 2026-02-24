using Prometheus;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog.Enrichers.Span;
using System.Diagnostics;

// Nome do serviço que aparecerá no Grafana
var serviceName = "my-api-dotnet";

var activitySource = new ActivitySource("MinhaApi.Negocio");

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
        .AddSource("MinhaApi.Negocio")
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

app.MapGet("/lento", async () =>
{
    var tempoEspera = Random.Shared.Next(500, 3000);
    Log.Information("Iniciando processamento lento de {Tempo}ms", tempoEspera);

    await Task.Delay(tempoEspera); // Simula uma demora (banco de dados ou API externa)

    Log.Information("Processamento lento finalizado.");
    return $"Demorei {tempoEspera}ms";
});

app.MapGet("/clientes-assiduos", async () =>
{
    // Span Pai (Automático pelo AspNetCoreInstrumentation)
    // Criamos um Sub-Span Manual para a lógica de Cache
    using (var activityCache = activitySource.StartActivity("Consulta Redis"))
    {
        Log.Information("Buscando no cache...");
        await Task.Delay(100); // Simula 100ms de Redis
        activityCache?.SetTag("cache.hit", false); // Adiciona metadados ao rastro
    }

    // Criamos outro Sub-Span para a lógica de Banco de Dados
    using (var activityDb = activitySource.StartActivity("Consulta MongoDB"))
    {
        Log.Information("Buscando no MongoDB...");
        await Task.Delay(500); // Simula 500ms de Mongo
        activityDb?.SetTag("db.query", "db.customers.find()");
    }

    return new { Status = "Sucesso", TempoTotal = "600ms" };
});

// Expõe o endpoint /metrics para o Prometheus ler (scrape)
app.MapMetrics();

app.Run("http://localhost:5000");

