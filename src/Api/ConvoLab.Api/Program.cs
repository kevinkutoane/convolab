using System.Text.Json.Serialization;
using System.Net;
using System.Reflection;
using ConvoLab.Api.Health;
using ConvoLab.Api.Middleware;
using ConvoLab.Api.Operations;
using ConvoLab.Api.Security;
using ConvoLab.Application;
using ConvoLab.Application.Operations;
using ConvoLab.Infrastructure;
using ConvoLab.Infrastructure.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("Starting ConvoLab API");
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((context, services, logger) => logger
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "ConvoLab.Api")
        .Enrich.WithProperty("Version", AssemblyVersion())
        .Enrich.WithProperty("Workstream", OperationalWorkstream.Marker));
    builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

    ProductionReadinessValidator.ValidateStaticOrThrow(builder.Configuration, builder.Environment);

    AddOperationalOptions(builder.Services, builder.Configuration);
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddSingleton<IProductionReadinessValidator, ProductionReadinessValidator>();
    builder.Services.AddSingleton<OperationalReadinessSummary>();
    builder.Services.AddConvoLabDataProtection(builder.Configuration, builder.Environment);
    builder.Services.AddConvoLabSecurity(builder.Environment);
    ConfigureForwardedHeaders(builder.Services, builder.Configuration);
    builder.Services.AddHsts(options =>
    {
        options.MaxAge = TimeSpan.FromDays(365);
        options.IncludeSubDomains = true;
        options.Preload = true;
    });
    builder.Services.AddHttpsRedirection(options =>
        options.HttpsPort = builder.Configuration.GetValue<int?>("Http:HttpsPort"));
    builder.Services.AddRateLimiter(options => options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
    builder.Services.AddScoped<WorkspaceIdentityBootstrapper>();
    builder.Services.AddScoped<ConvoLab.Infrastructure.Settings.SettingsBootstrapper>();

    var telemetryOptions = builder.Configuration.GetSection("Telemetry").Get<TelemetryOptions>() ?? new();
    var enableConsoleTelemetry = telemetryOptions.ConsoleExporter.Enabled;
    var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                       ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
    var tracesExporter = builder.Configuration["OTEL_TRACES_EXPORTER"]
                         ?? Environment.GetEnvironmentVariable("OTEL_TRACES_EXPORTER");
    var metricsExporter = builder.Configuration["OTEL_METRICS_EXPORTER"]
                          ?? Environment.GetEnvironmentVariable("OTEL_METRICS_EXPORTER");
    var enableOtlpTraces = ExporterIncludesOtlp(tracesExporter)
                           || (string.IsNullOrWhiteSpace(tracesExporter)
                               && !string.IsNullOrWhiteSpace(otlpEndpoint));
    var enableOtlpMetrics = ExporterIncludesOtlp(metricsExporter)
                            || (string.IsNullOrWhiteSpace(metricsExporter)
                                && !string.IsNullOrWhiteSpace(otlpEndpoint));
    var version = AssemblyVersion();
    var serviceName = builder.Configuration["OTEL_SERVICE_NAME"]
                      ?? Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")
                      ?? telemetryOptions.ServiceName;
    var resource = ResourceBuilder.CreateDefault()
        .AddService(serviceName, serviceVersion: version)
        .AddAttributes([
            new KeyValuePair<string, object>("deployment.environment.name", builder.Environment.EnvironmentName),
            new KeyValuePair<string, object>("convolab.workstream", OperationalWorkstream.Marker)
        ]);
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing =>
        {
            tracing
                .AddSource("ConvoLab.Api", ConvoLabTelemetry.SourceName)
                .SetResourceBuilder(resource)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation(options => options.FilterHttpRequestMessage =
                    SensitiveTelemetryHttpRequestOptions.ShouldInstrument)
                .AddEntityFrameworkCoreInstrumentation();
            if (enableConsoleTelemetry) tracing.AddConsoleExporter();
            if (enableOtlpTraces) tracing.AddOtlpExporter();
        })
        .WithMetrics(metrics =>
        {
            metrics
                .SetResourceBuilder(resource)
                .AddMeter(
                    "ConvoLab.Api",
                    ConvoLabTelemetry.MeterName,
                    ConvoLabTelemetry.DatabaseMeterName)
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation();
            if (enableConsoleTelemetry) metrics.AddConsoleExporter();
            if (enableOtlpMetrics) metrics.AddOtlpExporter();
        });

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy("ConvoLab API is running."), tags: ["live"])
        .AddCheck<ProductionConfigurationHealthCheck>("production-configuration", tags: ["startup", "ready"])
        .AddCheck<DataProtectionReadinessHealthCheck>("data-protection", tags: ["startup", "ready"])
        .AddCheck<DatabaseReadinessHealthCheck>("database", tags: ["startup", "ready"])
        .AddCheck<DocumentStorageHealthCheck>("document-storage", tags: ["ready"])
        .AddCheck<ProviderConfigurationHealthCheck>("providers", tags: ["ready"])
        .AddCheck<BootstrapIdentityHealthCheck>("workspace-identity", tags: ["ready"])
        .AddCheck<RequiredSecretsHealthCheck>("required-secrets", tags: ["ready"])
        .AddCheck<WorkerHeartbeatHealthCheck>("analytics-worker", tags: ["ready"])
        .AddCheck<AnalyticsPipelineHealthCheck>("analytics-pipeline", tags: ["ready"]);

    var app = builder.Build();

    var migrateOnStartup = app.Environment.IsDevelopment()
        || app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");
    if (migrateOnStartup)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<WorkspaceIdentityBootstrapper>().ApplyAsync();
        await scope.ServiceProvider.GetRequiredService<ConvoLab.Infrastructure.Settings.SettingsBootstrapper>().ApplyAsync();
    }

    if (app.Configuration.GetValue<bool>("Proxy:Enabled")) app.UseForwardedHeaders();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("EventName", "HttpRequest");
            diagnosticContext.Set("Outcome", httpContext.Response.StatusCode < 400 ? "Succeeded" : "Failed");
            var runtime = httpContext.RequestServices.GetService<ConvoLab.Infrastructure.WorkspaceIdentity.WorkspaceRequestContext>();
            if (runtime?.WorkspaceId is not null)
                diagnosticContext.Set("WorkspaceContext", runtime.WorkspaceId.Value);
            if (!string.IsNullOrWhiteSpace(runtime?.EnvironmentName))
                diagnosticContext.Set("EnvironmentContext", runtime.EnvironmentName);
        };
    });
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

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    if (app.Environment.IsProduction()) app.UseHsts();
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
    app.UseMiddleware<RouteScopeAuthorizationMiddleware>();
    app.UseMiddleware<RuntimeEnvironmentMiddleware>();
    app.UseMiddleware<GovernedActivityAuditMiddleware>();
    app.MapControllers();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live"),
        ResponseWriter = WriteHealthResponseAsync
    }).AllowAnonymous();
    app.MapHealthChecks("/health/startup", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("startup"),
        ResponseWriter = WriteHealthResponseAsync
    }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = WriteReadinessHealthResponseAsync
    }).AllowAnonymous();
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = WriteReadinessHealthResponseAsync
    }).AllowAnonymous();

    await app.RunAsync();
}
catch (HostAbortedException)
{
    throw;
}
catch (Exception exception)
{
    Log.Fatal(
        "Application terminated unexpectedly {ExceptionType}",
        exception.GetType().Name);
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
        version = AssemblyVersion(),
        correlationId = context.TraceIdentifier
    });
}

static async Task WriteReadinessHealthResponseAsync(
    HttpContext context,
    HealthReport report)
{
    context.RequestServices
        .GetRequiredService<OperationalReadinessSummary>()
        .Record(report.Status);
    await WriteHealthResponseAsync(context, report);
}

static string AssemblyVersion()
{
    var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                  ?? typeof(Program).Assembly.GetName().Version?.ToString()
                  ?? "unknown";
    return version.Split('+', 2)[0];
}

static bool ExporterIncludesOtlp(string? value) =>
    value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Any(item => item.Equals("otlp", StringComparison.OrdinalIgnoreCase)) == true;

static void AddOperationalOptions(IServiceCollection services, IConfiguration configuration)
{
    services.AddOptions<ProxyOptions>()
        .Bind(configuration.GetSection("Proxy"))
        .Validate(value => !value.Enabled
            || value.ForwardLimit is >= 1 and <= 5
            && (value.KnownProxies.Length > 0 || value.KnownNetworks.Length > 0),
            "Enabled proxy forwarding requires bounded, explicit trust boundaries.")
        .ValidateOnStart();
    services.AddOptions<LocalAuthenticationOptions>()
        .Bind(configuration.GetSection("Authentication:Local"))
        .ValidateOnStart();
    services.AddOptions<DataProtectionOptions>()
        .Bind(configuration.GetSection("DataProtection"))
        .ValidateOnStart();
    services.AddOptions<SecretStoreOptions>()
        .Bind(configuration.GetSection("SecretStores"))
        .Validate(value => value.CacheTtlSeconds is >= 1 and <= 3600
            && value.AzureKeyVault.TimeoutSeconds is >= 1 and <= 60
            && value.AzureKeyVault.MaxRetries is >= 0 and <= 5,
            "Secret-store cache, timeout, or retry settings are outside safe bounds.")
        .ValidateOnStart();
    services.AddOptions<SafeModeOptions>()
        .Bind(configuration.GetSection("SafeMode"))
        .ValidateOnStart();
    services.AddOptions<OperationsThresholdOptions>()
        .Bind(configuration.GetSection("Operations:Thresholds"))
        .Validate(OperationsThresholdOptions.IsValid,
            "Operational warning thresholds must be positive and lower than unhealthy thresholds.")
        .ValidateOnStart();
    services.AddOptions<RequiredSecretReadinessOptions>()
        .Bind(configuration.GetSection("Operations:RequiredSecrets"))
        .ValidateOnStart();
    services.AddOptions<TelemetryOptions>()
        .Bind(configuration.GetSection("Telemetry"))
        .Validate(value => value.OperationalSnapshotSeconds is >= 5 and <= 300
            && value.CollectorProbeSeconds is >= 5 and <= 300
            && !string.IsNullOrWhiteSpace(value.ServiceName),
            "Telemetry refresh and service-name settings are invalid.")
        .ValidateOnStart();
    services.AddOptions<BuildOptions>()
        .Bind(configuration.GetSection("Build"))
        .ValidateOnStart();
    services.AddOptions<AnalyticsWorkerOptions>()
        .Bind(configuration.GetSection("AnalyticsWorker"))
        .Validate(AnalyticsWorkerOptions.IsValid,
            "Analytics worker lease, renewal, poll, batch, or tolerance settings are invalid.")
        .ValidateOnStart();
}

static void ConfigureForwardedHeaders(IServiceCollection services, IConfiguration configuration)
{
    var configured = configuration.GetSection("Proxy").Get<ProxyOptions>() ?? new();
    if (!configured.Enabled) return;
    services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                                   | ForwardedHeaders.XForwardedProto
                                   | ForwardedHeaders.XForwardedHost;
        options.RequireHeaderSymmetry = true;
        options.ForwardLimit = configured.ForwardLimit;
        options.KnownProxies.Clear();
        options.KnownNetworks.Clear();
        foreach (var value in configured.KnownProxies)
            options.KnownProxies.Add(IPAddress.Parse(value));
        foreach (var value in configured.KnownNetworks)
        {
            var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(
                IPAddress.Parse(parts[0]), int.Parse(parts[1])));
        }
    });
}

public partial class Program { }
