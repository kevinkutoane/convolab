using System.Text.Json.Serialization;
using ConvoLab.Api.Health;
using ConvoLab.Api.Middleware;
using ConvoLab.Application;
using ConvoLab.Infrastructure;
using ConvoLab.Infrastructure.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("Starting ConvoLab API");
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    var enableConsoleTelemetry = builder.Configuration.GetValue<bool>("Telemetry:ConsoleExporter:Enabled");
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing =>
        {
            tracing
                .AddSource("ConvoLab.Api")
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ConvoLab.Api"))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation();
            if (enableConsoleTelemetry) tracing.AddConsoleExporter();
        })
        .WithMetrics(metrics =>
        {
            metrics
                .AddMeter("ConvoLab.Api")
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation();
            if (enableConsoleTelemetry) metrics.AddConsoleExporter();
        });

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy("ConvoLab API is running."), tags: ["live"])
        .AddCheck<DatabaseReadinessHealthCheck>("database", tags: ["ready"])
        .AddCheck<DocumentStorageHealthCheck>("document-storage", tags: ["ready"])
        .AddCheck<ProviderConfigurationHealthCheck>("providers", tags: ["ready"]);

    var app = builder.Build();

    var migrateOnStartup = app.Environment.IsDevelopment()
        || app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");
    if (migrateOnStartup)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        };
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    if (app.Configuration.GetValue("Http:UseHttpsRedirection", !app.Environment.IsDevelopment()))
        app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live"),
        ResponseWriter = WriteHealthResponseAsync
    });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = WriteHealthResponseAsync
    });
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = WriteHealthResponseAsync
    });

    await app.RunAsync();
}
catch (HostAbortedException)
{
    throw;
}
catch (Exception exception)
{
    Log.Fatal(exception, "Application terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static async Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        correlationId = context.TraceIdentifier,
        checks = report.Entries.Select(entry => new
        {
            component = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            durationMs = entry.Value.Duration.TotalMilliseconds,
            data = entry.Value.Data
        }),
        durationMs = report.TotalDuration.TotalMilliseconds
    });
}

public partial class Program { }
