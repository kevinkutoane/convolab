using ConvoLab.Application;
using ConvoLab.Infrastructure;
using ConvoLab.Api.Middleware;
using Serilog;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

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

    // Add Clean Architecture Layers
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Add OpenTelemetry
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .AddSource("ConvoLab.Api")
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ConvoLab.Api"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddConsoleExporter())
        .WithMetrics(metrics => metrics
            .AddMeter("ConvoLab.Api")
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            .AddConsoleExporter());

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Middleware Workflow
    app.UseMiddleware<GlobalExceptionMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();
    
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(x => new
                {
                    component = x.Key,
                    status = x.Value.Status.ToString(),
                    description = x.Value.Description
                }),
                duration = report.TotalDuration
            };
            await context.Response.WriteAsJsonAsync(response);
        }
    });

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
