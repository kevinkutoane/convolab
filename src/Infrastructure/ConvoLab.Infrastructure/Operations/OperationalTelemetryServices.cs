using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net.Sockets;
using ConvoLab.Application.Operations;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConvoLab.Infrastructure.Operations;

public sealed class OperationalMetricsSnapshotService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPlatformOperationalState _safeMode;
    private readonly ILogger<OperationalMetricsSnapshotService> _logger;
    private readonly TelemetryOptions _options;
    private readonly Meter _meter = new(ConvoLabTelemetry.DatabaseMeterName);
    private OperationalMetricSnapshot _snapshot = OperationalMetricSnapshot.Unavailable;

    public OperationalMetricsSnapshotService(
        IServiceScopeFactory scopeFactory,
        IPlatformOperationalState safeMode,
        IOptions<TelemetryOptions> options,
        ILogger<OperationalMetricsSnapshotService> logger)
    {
        _scopeFactory = scopeFactory;
        _safeMode = safeMode;
        _options = options.Value;
        _logger = logger;
        _meter.CreateObservableGauge(
            "convolab.analytics.outbox.pending",
            () => ObserveLong(_snapshot.PendingOutbox));
        _meter.CreateObservableGauge(
            "convolab.analytics.outbox.failed",
            () => ObserveLong(_snapshot.FailedOutbox));
        _meter.CreateObservableGauge(
            "convolab.analytics.outbox.oldest_age",
            () => ObserveDouble(_snapshot.OldestPendingAgeSeconds),
            "s");
        _meter.CreateObservableGauge(
            "convolab.analytics.aggregate.lag",
            () => ObserveDouble(_snapshot.AggregationLagSeconds),
            "s");
        _meter.CreateObservableGauge(
            "convolab.worker.lease.active",
            () => ObserveLong(_snapshot.WorkerLeaseActive));
        _meter.CreateObservableGauge(
            "convolab.worker.heartbeat.age",
            () => ObserveDouble(_snapshot.WorkerHeartbeatAgeSeconds),
            "s");
        _meter.CreateObservableGauge(
            "convolab.worker.last_iteration.status",
            () => ObserveLong(_snapshot.WorkerStatus));
        _meter.CreateObservableGauge(
            "convolab.safe_mode.active",
            () => ObserveLong(_snapshot.SafeModeActive));
        _meter.CreateObservableGauge(
            "convolab.auth.session.active",
            () => ObserveLong(_snapshot.ActiveSessions));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Operational metric snapshot refresh failed {ExceptionType}",
                    exception.GetType().Name);
            }
            await Task.Delay(
                TimeSpan.FromSeconds(_options.OperationalSnapshotSeconds),
                stoppingToken);
        }
    }

    internal async Task RefreshOnceAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var evidence = await scope.ServiceProvider
            .GetRequiredService<IAnalyticsOperationalEvidenceReader>()
            .ReadAsync(ct);
        var now = await ReadDatabaseTimeAsync(db, ct);
        var worker = await db.OperationalWorkerHeartbeats.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.WorkerName == "analytics-maintenance",
                ct);
        var sessions = await CountActiveSessionsAsync(db, now, ct);
        var safeMode = await _safeMode.GetAsync(ct);
        _snapshot = new(
            evidence.PendingCount,
            evidence.FailedCount,
            evidence.OldestPendingAgeSeconds,
            evidence.MaximumAggregationLagSeconds,
            worker is null ? 0 : worker.LeaseExpiresAt > now ? 1 : 0,
            worker is null ? null : Math.Max(0, (now - worker.LastHeartbeatAt).TotalSeconds),
            WorkerStatus(worker?.CurrentStatus),
            safeMode.EffectiveSafeModeEnabled ? 1 : 0,
            sessions);
    }

    private static async Task<long> CountActiveSessionsAsync(
        ApplicationDbContext db,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM "AuthenticationSessions"
            WHERE "RevokedAt" IS NULL AND "ExpiresAt" > @now
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "now";
        parameter.Value = now;
        command.Parameters.Add(parameter);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
    }

    private static async Task<DateTimeOffset> ReadDatabaseTimeAsync(
        ApplicationDbContext db,
        CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = db.Database.IsNpgsql()
            ? "SELECT clock_timestamp()"
            : "SELECT CURRENT_TIMESTAMP";
        var value = await command.ExecuteScalarAsync(ct);
        return value switch
        {
            DateTimeOffset timestamp => timestamp,
            DateTime timestamp => new DateTimeOffset(
                DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)),
            string timestamp when DateTimeOffset.TryParse(
                timestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var parsed) => parsed,
            _ => throw new InvalidOperationException(
                "The database returned an unsupported operational timestamp value.")
        };
    }

    private static long? WorkerStatus(string? status) => status switch
    {
        "Healthy" => 3,
        "Running" => 2,
        "Starting" => 1,
        "Degraded" => 0,
        "Failed" => -1,
        "LeaseLost" => -2,
        "Stopped" => -3,
        _ => null
    };

    private static IEnumerable<Measurement<long>> ObserveLong(long? value)
    {
        if (value.HasValue) yield return new(value.Value);
    }

    private static IEnumerable<Measurement<double>> ObserveDouble(double? value)
    {
        if (value.HasValue) yield return new(value.Value);
    }

    public override void Dispose()
    {
        _meter.Dispose();
        base.Dispose();
    }

    private sealed record OperationalMetricSnapshot(
        long? PendingOutbox,
        long? FailedOutbox,
        double? OldestPendingAgeSeconds,
        double? AggregationLagSeconds,
        long? WorkerLeaseActive,
        double? WorkerHeartbeatAgeSeconds,
        long? WorkerStatus,
        long? SafeModeActive,
        long? ActiveSessions)
    {
        public static OperationalMetricSnapshot Unavailable { get; } = new(
            null, null, null, null, null, null, null, null, null);
    }
}

public sealed class OtlpDependencyEvidenceService : BackgroundService,
    ITelemetryDependencyEvidenceSource
{
    private readonly IConfiguration _configuration;
    private readonly TelemetryOptions _options;
    private readonly ILogger<OtlpDependencyEvidenceService> _logger;
    private TelemetryDependencyEvidence _snapshot;

    public OtlpDependencyEvidenceService(
        IConfiguration configuration,
        IOptions<TelemetryOptions> options,
        ILogger<OtlpDependencyEvidenceService> logger)
    {
        _configuration = configuration;
        _options = options.Value;
        _logger = logger;
        _snapshot = InitialEvidence();
    }

    public TelemetryDependencyEvidence Snapshot() => _snapshot;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProbeAsync(stoppingToken);
            await Task.Delay(
                TimeSpan.FromSeconds(_options.CollectorProbeSeconds),
                stoppingToken);
        }
    }

    private async Task ProbeAsync(CancellationToken ct)
    {
        var current = InitialEvidence();
        if (!current.TraceExportEnabled && !current.MetricExportEnabled)
        {
            _snapshot = current;
            return;
        }

        var endpoint = Endpoint() ?? new Uri("http://localhost:4317");
        var port = endpoint.IsDefaultPort
            ? endpoint.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80
            : endpoint.Port;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            using var client = new TcpClient();
            await client.ConnectAsync(endpoint.Host, port, timeout.Token);
            _snapshot = current with
            {
                State = OperationalDependencyState.LiveValidated,
                LastLiveValidatedAt = DateTimeOffset.UtcNow,
                LastFailureCode = null
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _snapshot = current with
            {
                State = OperationalDependencyState.Unavailable,
                LastLiveValidatedAt = _snapshot.LastLiveValidatedAt,
                LastFailureCode = "telemetry.collector_unavailable"
            };
            _logger.LogDebug(
                "OTLP collector probe failed {ExceptionType}",
                exception.GetType().Name);
        }
    }

    private TelemetryDependencyEvidence InitialEvidence()
    {
        var traces = ExportEnabled("OTEL_TRACES_EXPORTER");
        var metrics = ExportEnabled("OTEL_METRICS_EXPORTER");
        var endpointConfigured = Endpoint() is not null;
        if (!traces && !metrics && endpointConfigured)
            traces = metrics = true;
        return new(
            traces || metrics
                ? OperationalDependencyState.Configured
                : OperationalDependencyState.NotConfigured,
            endpointConfigured,
            traces,
            metrics,
            _configuration["OTEL_SERVICE_NAME"]
                ?? Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")
                ?? _options.ServiceName,
            null,
            null);
    }

    private Uri? Endpoint()
    {
        var value = _configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                    ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        return Uri.TryCreate(value, UriKind.Absolute, out var endpoint) ? endpoint : null;
    }

    private bool ExportEnabled(string key)
    {
        var configured = _configuration[key] ?? Environment.GetEnvironmentVariable(key);
        return configured?.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(value => value.Equals("otlp", StringComparison.OrdinalIgnoreCase)) == true;
    }
}
