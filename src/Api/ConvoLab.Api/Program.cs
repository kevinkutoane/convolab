using System.Text.Json.Serialization;
using ConvoLab.Api.Health;
using ConvoLab.Api.Middleware;
using ConvoLab.Api.Security;
using ConvoLab.Application;
using ConvoLab.Infrastructure;
using ConvoLab.Infrastructure.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
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
    if (builder.Environment.IsEnvironment("Testing")) builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
    builder.Services.AddConvoLabSecurity();
    builder.Services.AddRateLimiter(options => options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
    builder.Services.AddScoped<WorkspaceIdentityBootstrapper>();

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
        .AddCheck<ProviderConfigurationHealthCheck>("providers", tags: ["ready"])
        .AddCheck<BootstrapIdentityHealthCheck>("workspace-identity", tags: ["ready"]);

    var app = builder.Build();

    var migrateOnStartup = app.Environment.IsDevelopment()
        || app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");
    if (migrateOnStartup)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<WorkspaceIdentityBootstrapper>().ApplyAsync();
    }

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none'; object-src 'none'";
        await next();
    });
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
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseMiddleware<CookieAntiforgeryMiddleware>();
    app.UseAuthorization();
    app.UseStatusCodePages(async (StatusCodeContext statusContext) =>
    {
        var response = statusContext.HttpContext.Response;
        if (response.HasStarted || response.ContentLength.HasValue) return;
        var code = response.StatusCode == 401 ? "auth.required" : response.StatusCode == 403 ? "permission.denied" : "request.rejected";
        response.ContentType = "application/problem+json";
        await response.WriteAsJsonAsync(new
        {
            type = $"https://errors.convolab.dev/{code}",
            title = response.StatusCode == 401 ? "Authentication required" : "Access denied",
            status = response.StatusCode,
            code,
            correlationId = statusContext.HttpContext.TraceIdentifier
        });
    });
    app.UseMiddleware<CapabilityPermissionMiddleware>();
    app.UseMiddleware<GovernedActivityAuditMiddleware>();
    app.MapControllers();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live"),
        ResponseWriter = WriteHealthResponseAsync
    }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = WriteHealthResponseAsync
    }).AllowAnonymous();
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = WriteHealthResponseAsync
    }).AllowAnonymous();

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
