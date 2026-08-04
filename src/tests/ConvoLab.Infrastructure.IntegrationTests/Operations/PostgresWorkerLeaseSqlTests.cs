using ConvoLab.Infrastructure.Operations;
using ConvoLab.Infrastructure.Analytics;
using ConvoLab.Application.Operations;

namespace ConvoLab.Infrastructure.IntegrationTests.Operations;

public sealed class PostgresWorkerLeaseSqlTests
{
    private static readonly WorkerLeaseHandle Lease = new(
        "worker", "instance", 42, DateTimeOffset.UtcNow.AddMinutes(2));

    public static TheoryData<FormattableString> Statements => new()
    {
        PostgresWorkerLeaseSql.Acquire("worker", "instance", 120),
        PostgresWorkerLeaseSql.Renew("worker", "instance", 42, 120),
        PostgresWorkerLeaseSql.IsOwned("worker", "instance", 42),
        PostgresWorkerLeaseSql.RecordStarted(Lease, 120),
        PostgresWorkerLeaseSql.RecordResult(
            Lease, AnalyticsMaintenanceResult.Empty, "Healthy", null, 120),
        PostgresWorkerLeaseSql.RecordFailure(
            Lease, "Failed", "worker.failed", "The iteration failed.", 120)
    };

    [Theory]
    [MemberData(nameof(Statements))]
    public void Each_statement_captures_postgres_server_time_exactly_once(FormattableString statement)
    {
        var sql = statement.Format;

        Assert.Equal(1, Count(sql, "clock_timestamp()"));
        Assert.Contains("WITH server_time AS MATERIALIZED", sql, StringComparison.Ordinal);
        Assert.Contains("server_time.now", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CURRENT_TIMESTAMP", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NOW()", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Acquisition_uses_captured_time_for_eligibility_and_expiry()
    {
        var sql = PostgresWorkerLeaseSql.Acquire("worker", "instance", 120).Format;

        Assert.Contains("LeaseExpiresAt\" = EXCLUDED.\"LeaseExpiresAt", sql, StringComparison.Ordinal);
        Assert.Contains("LeaseExpiresAt\" <= (SELECT now FROM server_time)", sql, StringComparison.Ordinal);
        Assert.Contains("server_time.now + (", sql, StringComparison.Ordinal);
        Assert.Contains("LeaseToken\" = \"OperationalWorkerHeartbeats\".\"LeaseToken\" + 1", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Ownership_query_remains_composable_for_ef_scalar_projection()
    {
        var sql = PostgresWorkerLeaseSql.IsOwned("worker", "instance", 42).Format;

        Assert.False(sql.TrimEnd().EndsWith(';'));
    }

    [Fact]
    public void Export_claim_captures_server_time_once_and_requires_the_fencing_token()
    {
        var sql = AnalyticsExportClaims.Statement(Lease, 120, 100).Format;

        Assert.Equal(1, Count(sql, "clock_timestamp()"));
        Assert.Contains("WITH server_time AS MATERIALIZED", sql, StringComparison.Ordinal);
        Assert.Contains("valid_worker AS MATERIALIZED", sql, StringComparison.Ordinal);
        Assert.Contains("worker.\"LeaseToken\"", sql, StringComparison.Ordinal);
        Assert.Contains("FOR UPDATE OF export SKIP LOCKED", sql, StringComparison.Ordinal);
        Assert.Contains("RETURNING export.\"Id\"", sql, StringComparison.Ordinal);
    }

    private static int Count(string value, string marker)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(marker, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += marker.Length;
        }

        return count;
    }
}
