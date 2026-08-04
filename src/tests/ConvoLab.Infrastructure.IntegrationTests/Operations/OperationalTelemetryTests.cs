using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using ConvoLab.Application.Operations;
using ConvoLab.Application.Settings;
using ConvoLab.Infrastructure.Analytics;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.Operations;
using ConvoLab.Infrastructure.Settings;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ConvoLab.Infrastructure.IntegrationTests.Operations;

public sealed class OperationalTelemetryTests
{
    [Fact]
    public async Task Sensitive_outbound_requests_are_suppressed_and_custom_secret_evidence_is_sanitized()
    {
        var sentinels = new[]
        {
            "password-sentinel", "gemini-key-sentinel", "azure-credential-sentinel",
            "docker-secret-value-sentinel", "cookie-sentinel", "authorization-sentinel",
            "antiforgery-sentinel", "secret-reference-sentinel", "prompt-sentinel",
            "customer-message-sentinel", "provider-response-sentinel", "otlp-header-sentinel"
        };
        var capturedActivities = new ConcurrentBag<string>();
        var activityIds = new ConcurrentBag<(ActivityTraceId TraceId, ActivitySpanId SpanId)>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ConvoLabTelemetry.SourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                capturedActivities.Add(string.Join('|',
                    activity.DisplayName,
                    string.Join('|', activity.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"))));
                activityIds.Add((activity.TraceId, activity.SpanId));
            }
        };
        ActivitySource.AddActivityListener(activityListener);

        var capturedMetrics = new ConcurrentBag<string>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ConvoLabTelemetry.MeterName)
                    listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            capturedMetrics.Add($"{instrument.Name}:{measurement}:{Tags(tags)}"));
        meterListener.Start();

        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 20 });
        var store = new CompositeSecretStore(
            [new SentinelSecretProvider(string.Join('|', sentinels))],
            cache,
            new SecretProviderEvidenceRegistry(),
            Options.Create(new SecretStoreOptions()));
        var resolved = await store.ResolveAsync("env:SENSITIVE_TEST_VALUE");
        Assert.True(resolved.IsResolved);
        var failed = await store.ResolveAsync("env:FAIL_SAFELY");
        Assert.False(failed.IsResolved);

        var gemini = new HttpRequestMessage(
            HttpMethod.Post,
            "https://generativelanguage.googleapis.com/v1beta/models/prompt-sentinel");
        var vault = new HttpRequestMessage(
            HttpMethod.Get,
            "https://allowed.vault.azure.net/secrets/secret-reference-sentinel");
        var plugin = new HttpRequestMessage(
            HttpMethod.Get,
            "https://plugins.example.test/customer-message-sentinel");
        plugin.Options.Set(
            SensitiveTelemetryHttpRequestOptions.SuppressAutomaticInstrumentation,
            true);
        var ordinary = new HttpRequestMessage(
            HttpMethod.Get,
            "https://telemetry-safe.example.test/health");

        Assert.False(SensitiveTelemetryHttpRequestOptions.ShouldInstrument(gemini));
        Assert.False(SensitiveTelemetryHttpRequestOptions.ShouldInstrument(vault));
        Assert.False(SensitiveTelemetryHttpRequestOptions.ShouldInstrument(plugin));
        Assert.True(SensitiveTelemetryHttpRequestOptions.ShouldInstrument(ordinary));
        Assert.All(activityIds, ids =>
        {
            Assert.NotEqual(default, ids.TraceId);
            Assert.NotEqual(default, ids.SpanId);
        });

        var emitted = string.Join('|', capturedActivities.Concat(capturedMetrics));
        Assert.Contains("secret.resolve", emitted, StringComparison.Ordinal);
        Assert.Contains("convolab.secret.resolve.failure", emitted, StringComparison.Ordinal);
        Assert.All(sentinels, sentinel =>
            Assert.DoesNotContain(sentinel, emitted, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Configured_otlp_is_not_overclaimed_as_live_validation()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://collector.invalid:4317"
            }).Build();
        using var service = new OtlpDependencyEvidenceService(
            configuration,
            Options.Create(new TelemetryOptions()),
            NullLogger<OtlpDependencyEvidenceService>.Instance);

        var evidence = service.Snapshot();

        Assert.Equal(OperationalDependencyState.Configured, evidence.State);
        Assert.True(evidence.EndpointConfigured);
        Assert.Null(evidence.LastLiveValidatedAt);
    }

    [Fact]
    public void Provider_cost_is_emitted_only_for_actual_or_estimated_values_with_bounded_labels()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var db = new ApplicationDbContext(dbOptions);
        var measurements = new ConcurrentBag<(double Value, string Tags)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == ConvoLabTelemetry.MeterName
                    && instrument.Name == "convolab.provider.cost.zar")
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, measurement, tags, _) =>
            measurements.Add((measurement, Tags(tags))));
        listener.Start();

        AnalyticsOutboxFactory.Enqueue(db, CostEvent("Actual", 1.25m));
        AnalyticsOutboxFactory.Enqueue(db, CostEvent("Estimated", 2.50m));
        AnalyticsOutboxFactory.Enqueue(db, CostEvent("Unavailable", 0m));
        AnalyticsOutboxFactory.Enqueue(db, CostEvent("Actual", null));

        Assert.Equal(2, measurements.Count);
        Assert.Contains(measurements, item => item.Value == 1.25);
        Assert.Contains(measurements, item => item.Value == 2.5);
        Assert.All(measurements, item =>
        {
            Assert.Contains("provider_type=external", item.Tags, StringComparison.Ordinal);
            Assert.Contains("cost_type=", item.Tags, StringComparison.Ordinal);
            Assert.Contains("outcome=succeeded", item.Tags, StringComparison.Ordinal);
            Assert.DoesNotContain("model", item.Tags, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Observable_gauges_reflect_database_backed_state()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var db = new ApplicationDbContext(dbOptions);
        await db.Database.OpenConnectionAsync();
        await db.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        db.IdentityUsers.Add(new IdentityUserRecord
        {
            Id = userId,
            Email = "metric@convolab.test",
            NormalizedEmail = "METRIC@CONVOLAB.TEST",
            DisplayName = "Metric Session",
            Status = "Active",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AuthenticationSessions.Add(new AuthenticationSessionRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Guid.NewGuid().ToString("N"),
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now.AddMinutes(5)
        });
        db.AnalyticsOutbox.AddRange(
            Outbox("Pending", now.AddSeconds(-30)),
            Outbox("Failed", now.AddSeconds(-60)));
        db.AnalyticsAggregationCheckpoints.Add(new AnalyticsAggregationCheckpointRecord
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Granularity = "hour",
            Status = "Dirty",
            DirtyFromUtc = now.AddSeconds(-90),
            UpdatedAt = now
        });
        db.OperationalWorkerHeartbeats.Add(new OperationalWorkerHeartbeatRecord
        {
            WorkerName = "analytics-maintenance",
            InstanceId = "metrics-owner",
            StartedAt = now,
            LastHeartbeatAt = now.AddSeconds(-2),
            CurrentStatus = "Healthy",
            LeaseToken = 8,
            LeaseExpiresAt = now.AddMinutes(1)
        });
        await db.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddScoped<IAnalyticsOperationalEvidenceReader, AnalyticsOperationalEvidenceReader>();
        await using var provider = services.BuildServiceProvider();
        var service = new OperationalMetricsSnapshotService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new ActiveSafeMode(),
            Options.Create(new TelemetryOptions { OperationalSnapshotSeconds = 5 }),
            NullLogger<OperationalMetricsSnapshotService>.Instance);
        var values = new ConcurrentDictionary<string, double>(StringComparer.Ordinal);
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == ConvoLabTelemetry.DatabaseMeterName)
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
            values[instrument.Name] = measurement);
        listener.SetMeasurementEventCallback<double>((instrument, measurement, _, _) =>
            values[instrument.Name] = measurement);
        listener.Start();
        await service.RefreshOnceAsync(CancellationToken.None);
        listener.RecordObservableInstruments();
        service.Dispose();

        Assert.Equal(1, values["convolab.analytics.outbox.pending"]);
        Assert.Equal(1, values["convolab.analytics.outbox.failed"]);
        Assert.InRange(values["convolab.analytics.outbox.oldest_age"], 20, 45);
        Assert.InRange(values["convolab.analytics.aggregate.lag"], 75, 105);
        Assert.Equal(1, values["convolab.worker.lease.active"]);
        Assert.InRange(values["convolab.worker.heartbeat.age"], 0, 10);
        Assert.Equal(3, values["convolab.worker.last_iteration.status"]);
        Assert.Equal(1, values["convolab.safe_mode.active"]);
        Assert.Equal(1, values["convolab.auth.session.active"]);
    }

    private static AnalyticsOutboxRecord Outbox(string status, DateTimeOffset createdAt) => new()
    {
        Id = Guid.NewGuid(),
        EventKey = Guid.NewGuid().ToString("N"),
        PayloadJson = "{}",
        Status = status,
        CreatedAt = createdAt,
        AvailableAt = createdAt
    };

    private static AnalyticsEventRecord CostEvent(string costType, decimal? cost) => new()
    {
        Id = Guid.NewGuid(),
        EventKey = Guid.NewGuid().ToString("N"),
        OrganisationId = Guid.NewGuid(),
        WorkspaceId = Guid.NewGuid(),
        EnvironmentId = Guid.NewGuid(),
        ActorType = "System",
        Capability = "Simulation",
        EventType = "ProviderInvocationCompleted",
        Outcome = "Succeeded",
        Provider = "Gemini",
        Model = "user-configurable-model-must-not-be-a-label",
        CostZar = cost,
        CostType = costType,
        SourceType = "SimulationRun",
        SourceId = Guid.NewGuid(),
        ConfigurationRevision = "test",
        CorrelationId = Guid.NewGuid().ToString("N"),
        OccurredAt = DateTimeOffset.UtcNow
    };

    private static string Tags(ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
        string.Join(',', tags.ToArray().Select(tag => $"{tag.Key}={tag.Value}"));

    private sealed class SentinelSecretProvider(string value) : ISecretProvider
    {
        public string Scheme => "env";

        public Task<SecretResolutionResult> ResolveAsync(
            string key,
            CancellationToken ct) => Task.FromResult(
            key == "FAIL_SAFELY"
                ? SecretResolutionResult.Failed(
                    Scheme,
                    SecretResolutionStatus.Unavailable,
                    "secret.test.unavailable")
                : SecretResolutionResult.Resolved(Scheme, value));
    }

    private sealed class ActiveSafeMode : IPlatformOperationalState
    {
        public Task<PlatformOperationalState> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(new PlatformOperationalState(
                true, false, true, false, true, "metric test", 1, DateTimeOffset.UtcNow));
        public Task EnsureExternalExecutionAllowedAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureDeterministicExecutionAllowedAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureAnalyticsExportAllowedAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
