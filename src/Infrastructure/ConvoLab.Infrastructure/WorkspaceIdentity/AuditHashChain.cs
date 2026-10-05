using System.Security.Cryptography;
using System.Text;

namespace ConvoLab.Infrastructure.WorkspaceIdentity;

public sealed record AuditChainVerification(
    string ChainKey,
    bool IsIntact,
    long VerifiedCount,
    long? FirstBrokenSequence,
    string? Reason);

/// <summary>
/// Tamper-evident hash chain over audit events. Each sealed event commits to its own content
/// and to the previous event's hash, so editing, reordering, or removing a middle event is
/// detectable. Chains are partitioned per workspace (plus one "platform" chain).
/// Limitation: removing the newest events leaves a valid shorter chain; detecting that needs the
/// latest hash to be anchored outside the database.
/// </summary>
public static class AuditHashChain
{
    public const string Genesis = "GENESIS";
    public const string PlatformChainKey = "platform";

    public static string ChainKeyFor(Guid? workspaceId) =>
        workspaceId is { } id && id != Guid.Empty ? id.ToString("N") : PlatformChainKey;

    public static void Seal(AuditEventRecord record, long sequence, string previousHash)
    {
        record.ChainKey = ChainKeyFor(record.WorkspaceId);
        record.Sequence = sequence;
        record.PreviousHash = previousHash;
        record.Hash = Compute(record);
    }

    public static string Compute(AuditEventRecord record)
    {
        var builder = new StringBuilder();
        Append(builder, record.Id.ToString("D"));
        Append(builder, record.ChainKey);
        Append(builder, record.Sequence?.ToString());
        Append(builder, record.PreviousHash);
        Append(builder, record.Scope);
        Append(builder, record.OrganisationId?.ToString("D"));
        Append(builder, record.WorkspaceId?.ToString("D"));
        Append(builder, record.ActorType);
        Append(builder, record.ActorId?.ToString("D"));
        Append(builder, record.ActorDisplay);
        Append(builder, record.Action);
        Append(builder, record.ResourceType);
        Append(builder, record.ResourceId);
        Append(builder, record.Outcome);
        Append(builder, record.DetailJson);
        Append(builder, record.CorrelationId);
        // Databases store microsecond precision; hashing at that precision keeps the value
        // stable across a write/read round trip.
        Append(builder, (record.OccurredAt.UtcTicks / 10).ToString());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    /// <summary>Verifies sealed events of one chain, supplied in ascending sequence order.</summary>
    public static AuditChainVerification Verify(string chainKey, IEnumerable<AuditEventRecord> sealedInOrder)
    {
        var expectedSequence = 1L;
        var previousHash = Genesis;
        foreach (var record in sealedInOrder)
        {
            if (record.Sequence != expectedSequence)
                return Broken(chainKey, expectedSequence - 1, expectedSequence, "Sequence gap or reordering detected.");
            if (!string.Equals(record.PreviousHash, previousHash, StringComparison.Ordinal))
                return Broken(chainKey, expectedSequence - 1, expectedSequence, "Previous-hash link does not match.");
            if (!string.Equals(record.Hash, Compute(record), StringComparison.Ordinal))
                return Broken(chainKey, expectedSequence - 1, expectedSequence, "Event content does not match its hash.");
            previousHash = record.Hash!;
            expectedSequence++;
        }
        return new AuditChainVerification(chainKey, true, expectedSequence - 1, null, null);
    }

    private static AuditChainVerification Broken(string chainKey, long verified, long brokenAt, string reason) =>
        new(chainKey, false, verified, brokenAt, reason);

    // Length-prefixed fields so adjacent values can never be re-split to produce the same input.
    private static void Append(StringBuilder builder, string? value) =>
        builder.Append(value is null ? "~" : $"{value.Length}:{value}").Append('|');
}
