using ConvoLab.Infrastructure.Analytics;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.IntegrationTests.Operations;

public sealed class AnalyticsOperationalEvidenceTests
{
    [Fact]
    public async Task Reader_reports_pending_failed_and_aggregation_evidence_without_payloads()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        db.AnalyticsOutbox.AddRange(
            Outbox("Pending", now.AddSeconds(-70)),
            Outbox("Failed", now.AddSeconds(-310)),
            new AnalyticsOutboxRecord
            {
                Id = Guid.NewGuid(),
                EventKey = Guid.NewGuid().ToString("N"),
                PayloadJson = "{\"sentinel\":\"must-not-appear\"}",
                Status = "Processed",
                CreatedAt = now.AddMinutes(-5),
                AvailableAt = now.AddMinutes(-5),
                ProcessedAt = now.AddSeconds(-5)
            });
        db.AnalyticsAggregationCheckpoints.Add(new AnalyticsAggregationCheckpointRecord
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Granularity = "hour",
            Status = "Failed",
            DirtyFromUtc = now.AddSeconds(-130),
            FailureReason = "analytics.aggregation.rebuild_failed",
            UpdatedAt = now.AddSeconds(-20),
            LastSuccessfulRunAt = now.AddMinutes(-10)
        });
        await db.SaveChangesAsync();

        var evidence = await new AnalyticsOperationalEvidenceReader(db).ReadAsync();

        Assert.Equal(1, evidence.PendingCount);
        Assert.Equal(1, evidence.FailedCount);
        Assert.InRange(evidence.OldestPendingAgeSeconds!.Value, 60, 90);
        Assert.InRange(evidence.OldestFailedAgeSeconds!.Value, 300, 330);
        Assert.Equal(1, evidence.AggregationDirtyCheckpointCount);
        Assert.Equal(1, evidence.AggregationFailedCheckpointCount);
        Assert.InRange(evidence.MaximumAggregationLagSeconds, 120, 150);
        Assert.NotNull(evidence.LastSuccessfulOutboxDispatchAt);
        Assert.NotNull(evidence.LastSuccessfulAggregationAt);
        Assert.DoesNotContain("sentinel", evidence.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static AnalyticsOutboxRecord Outbox(string status, DateTimeOffset createdAt) => new()
    {
        Id = Guid.NewGuid(),
        EventKey = Guid.NewGuid().ToString("N"),
        PayloadJson = "{}",
        Status = status,
        CreatedAt = createdAt,
        AvailableAt = createdAt,
        Attempts = status == "Failed" ? 10 : 0,
        LastError = status == "Failed" ? "analytics.outbox.dispatch_failed" : null
    };
}
