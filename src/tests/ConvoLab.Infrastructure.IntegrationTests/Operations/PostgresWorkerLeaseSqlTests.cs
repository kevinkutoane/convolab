using ConvoLab.Infrastructure.Operations;

namespace ConvoLab.Infrastructure.IntegrationTests.Operations;

public sealed class PostgresWorkerLeaseSqlTests
{
    public static TheoryData<FormattableString> Statements => new()
    {
        PostgresWorkerLeaseSql.AcquireOrRenew("worker", "instance", 120),
        PostgresWorkerLeaseSql.RecordSuccess("worker", "instance", 1, 120),
        PostgresWorkerLeaseSql.RecordFailure("worker", "instance", "worker.failed", 120)
    };

    [Theory]
    [MemberData(nameof(Statements))]
    public void Each_statement_captures_postgres_server_time_exactly_once(FormattableString statement)
    {
        var sql = statement.Format;

        Assert.Equal(1, Count(sql, "clock_timestamp()"));
        Assert.Contains("WITH server_clock AS MATERIALIZED", sql, StringComparison.Ordinal);
        Assert.Contains("server_clock.value", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CURRENT_TIMESTAMP", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NOW()", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Acquisition_uses_captured_time_for_eligibility_and_expiry()
    {
        var sql = PostgresWorkerLeaseSql.AcquireOrRenew("worker", "instance", 120).Format;

        Assert.Contains("LeaseExpiresAt\" = EXCLUDED.\"LeaseExpiresAt", sql, StringComparison.Ordinal);
        Assert.Contains("LeaseExpiresAt\" <= (SELECT value FROM server_clock)", sql, StringComparison.Ordinal);
        Assert.Contains("server_clock.value + (", sql, StringComparison.Ordinal);
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
