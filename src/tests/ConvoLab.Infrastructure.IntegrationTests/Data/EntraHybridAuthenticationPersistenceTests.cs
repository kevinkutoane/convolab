using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.IntegrationTests.Data;

public sealed class EntraHybridAuthenticationPersistenceTests
{
    [Fact]
    public async Task External_identity_key_is_unique_and_session_provider_round_trips()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        var user = new IdentityUserRecord
        {
            Id = Guid.NewGuid(), Email = "linked@example.test", NormalizedEmail = "LINKED@EXAMPLE.TEST",
            DisplayName = "Linked user", Status = "Active", CreatedAt = now, UpdatedAt = now
        };
        var identity = new ExternalIdentityRecord
        {
            Id = Guid.NewGuid(), UserId = user.Id, Provider = "Entra", Issuer = "https://issuer.example/v2.0",
            Subject = "subject-1", TenantId = "tenant-1", CreatedAt = now, LastLoginAt = now
        };
        db.IdentityUsers.Add(user); db.ExternalIdentities.Add(identity);
        db.AuthenticationSessions.Add(new AuthenticationSessionRecord
        {
            Id = Guid.NewGuid(), UserId = user.Id, TokenHash = new string('A', 64), CreatedAt = now,
            LastSeenAt = now, ExpiresAt = now.AddHours(8), AbsoluteExpiresAt = now.AddHours(24),
            AuthenticationProvider = "Entra", ExternalIdentityId = identity.Id, SessionFamilyId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();
        db.ExternalIdentities.Add(new ExternalIdentityRecord
        {
            Id = Guid.NewGuid(), UserId = user.Id, Provider = "Entra", Issuer = identity.Issuer,
            Subject = identity.Subject, TenantId = "tenant-1", CreatedAt = now, LastLoginAt = now
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var session = await db.AuthenticationSessions.AsNoTracking().SingleAsync();
        Assert.Equal("Entra", session.AuthenticationProvider);
        Assert.Equal(identity.Id, session.ExternalIdentityId);
        Assert.DoesNotContain("token", string.Join('|', await db.ExternalIdentities.Select(item => item.Subject).ToListAsync()), StringComparison.OrdinalIgnoreCase);
    }
}
