using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.IntegrationTests.Data;

public sealed class AuditHashChainTests
{
    private static AuditEventRecord Event(Guid? workspaceId, string action) => new()
    {
        Id = Guid.NewGuid(), Scope = "Workspace", WorkspaceId = workspaceId, ActorType = "User",
        ActorDisplay = "Tester", Action = action, ResourceType = "Prompt", ResourceId = "r-1",
        Outcome = "Succeeded", DetailJson = "{}", CorrelationId = "corr-1", OccurredAt = DateTimeOffset.UtcNow
    };

    private static async Task<(SqliteConnection Connection, ApplicationDbContext Db)> OpenAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        return (connection, db);
    }

    [Fact]
    public async Task Saved_events_are_sealed_into_a_per_workspace_chain_across_saves()
    {
        var (connection, db) = await OpenAsync();
        await using var _ = connection; await using var __ = db;
        var workspace = Guid.NewGuid();
        var other = Guid.NewGuid();

        db.WorkspaceAuditEvents.AddRange(Event(workspace, "A"), Event(workspace, "B"), Event(other, "X"));
        await db.SaveChangesAsync();
        db.WorkspaceAuditEvents.Add(Event(workspace, "C"));
        await db.SaveChangesAsync();

        var chain = await db.WorkspaceAuditEvents.AsNoTracking()
            .Where(e => e.WorkspaceId == workspace).OrderBy(e => e.Sequence).ToListAsync();
        Assert.Equal([1L, 2L, 3L], chain.Select(e => e.Sequence!.Value));
        Assert.Equal(AuditHashChain.Genesis, chain[0].PreviousHash);
        Assert.Equal(chain[0].Hash, chain[1].PreviousHash);
        Assert.Equal(chain[1].Hash, chain[2].PreviousHash);
        Assert.True(AuditHashChain.Verify(chain[0].ChainKey!, chain).IsIntact);

        var otherChain = await db.WorkspaceAuditEvents.AsNoTracking().Where(e => e.WorkspaceId == other).ToListAsync();
        Assert.Equal(1L, Assert.Single(otherChain).Sequence);
    }

    [Fact]
    public async Task Events_without_a_workspace_share_the_platform_chain()
    {
        var (connection, db) = await OpenAsync();
        await using var _ = connection; await using var __ = db;
        db.WorkspaceAuditEvents.AddRange(Event(null, "P1"), Event(null, "P2"));
        await db.SaveChangesAsync();

        var chain = await db.WorkspaceAuditEvents.AsNoTracking()
            .Where(e => e.ChainKey == AuditHashChain.PlatformChainKey).OrderBy(e => e.Sequence).ToListAsync();
        Assert.Equal(2, chain.Count);
        Assert.True(AuditHashChain.Verify(AuditHashChain.PlatformChainKey, chain).IsIntact);
    }

    [Fact]
    public async Task Verification_detects_edited_deleted_and_reordered_events()
    {
        var (connection, db) = await OpenAsync();
        await using var _ = connection; await using var __ = db;
        var workspace = Guid.NewGuid();
        db.WorkspaceAuditEvents.AddRange(Event(workspace, "A"), Event(workspace, "B"), Event(workspace, "C"));
        await db.SaveChangesAsync();
        var key = AuditHashChain.ChainKeyFor(workspace);
        List<AuditEventRecord> Load() => db.WorkspaceAuditEvents.AsNoTracking()
            .Where(e => e.ChainKey == key).OrderBy(e => e.Sequence).ToList();

        // Edit: change the action of event #2 behind the application's back.
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE WorkspaceAuditEvents SET Action = 'Tampered' WHERE ChainKey = {0} AND Sequence = 2", key);
        var edited = AuditHashChain.Verify(key, Load());
        Assert.False(edited.IsIntact);
        Assert.Equal(2, edited.FirstBrokenSequence);

        // Delete the middle event: sequence gap.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM WorkspaceAuditEvents WHERE ChainKey = {0} AND Sequence = 2", key);
        var deleted = AuditHashChain.Verify(key, Load());
        Assert.False(deleted.IsIntact);
        Assert.Equal(2, deleted.FirstBrokenSequence);
    }

    [Fact]
    public async Task Application_still_refuses_to_modify_or_remove_audit_events()
    {
        var (connection, db) = await OpenAsync();
        await using var _ = connection; await using var __ = db;
        var record = Event(Guid.NewGuid(), "A");
        db.WorkspaceAuditEvents.Add(record);
        await db.SaveChangesAsync();
        record.Action = "Changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void Verify_treats_an_empty_chain_as_intact()
    {
        var result = AuditHashChain.Verify("none", []);
        Assert.True(result.IsIntact);
        Assert.Equal(0, result.VerifiedCount);
    }
}
