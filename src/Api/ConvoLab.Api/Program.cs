using Serilog;
using OpenTelemetry.Trace;
using OpenTelemetry.Instrumentation.AspNetCore;
using ConvoLab.Infrastructure;

// Configure Serilog before building the host
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/convolab-.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ConvoLab")
    .CreateLogger();

try
{
    Log.Information("Starting ConvoLab API");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog
    builder.Host.UseSerilog();

    // Add services to the container
    builder.Services
        .AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = null;
        });

    // Add OpenAPI/Swagger
    builder.Services.AddOpenApi();
    builder.Services.AddSwaggerGen();

    // Add health checks
    builder.Services.AddHealthChecks();

    // Add OpenTelemetry
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddConsoleExporter());

    // Add Infrastructure services
    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "ConvoLab API v1");
            options.RoutePrefix = string.Empty;
        });
    }

    app.UseHttpsRedirection();
    app.UseAuthorization();

    // Map health checks
    app.MapHealthChecks("/health");

    // Map controllers
    app.MapControllers();

    // Global exception handler middleware
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
            Log.Error(exception, "An unhandled exception occurred");

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = "An error occurred while processing your request",
                message = app.Environment.IsDevelopment() ? exception?.Message : null
            };

            await context.Response.WriteAsJsonAsync(response);
        });
    });

    Log.Information("ConvoLab API started successfully");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "ConvoLab API terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
