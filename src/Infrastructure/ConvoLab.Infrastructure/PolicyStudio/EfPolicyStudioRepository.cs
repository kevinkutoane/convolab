using System.Text.Json;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.PolicyStudio;
using ConvoLab.Domain.Policy.Enums;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.PolicyStudio;

public sealed class EfPolicyStudioRepository(ApplicationDbContext db) : IPolicyStudioRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<int> CountPoliciesAsync(CancellationToken cancellationToken = default)
        => db.PolicyDefinitions.AsNoTracking().CountAsync(cancellationToken);

    public async Task<IReadOnlyList<PolicyDefinitionState>> ListPoliciesAsync(CancellationToken cancellationToken = default)
    {
        var records = await db.PolicyDefinitions.AsNoTracking().ToListAsync(cancellationToken);
        var hydrated = await HydrateAsync(records, cancellationToken);
        return hydrated.OrderByDescending(item => item.UpdatedAt).ToList();
    }

    public async Task<PolicyDefinitionState?> GetPolicyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await db.PolicyDefinitions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (record is null) return null;
        return (await HydrateAsync([record], cancellationToken)).Single();
    }

    public async Task<IReadOnlyList<PolicyDefinitionState>> GetVersionHistoryAsync(Guid policyKey, CancellationToken cancellationToken = default)
    {
        var records = await db.PolicyDefinitions.AsNoTracking()
            .Where(item => item.PolicyKey == policyKey)
            .OrderByDescending(item => item.Version)
            .ToListAsync(cancellationToken);
        return await HydrateAsync(records, cancellationToken);
    }

    public async Task<IReadOnlyList<PolicyDefinitionState>> ListActiveByDomainAsync(
        PolicyDomain domain,
        Guid? tenantId,
        string environment,
        CancellationToken cancellationToken = default)
    {
        var domainName = domain.ToString();
        var records = await db.PolicyDefinitions.AsNoTracking()
            .Where(item => item.Domain == domainName && item.Status == nameof(PolicyStatus.Active))
            .OrderBy(item => item.Scope)
            .ThenBy(item => item.Version)
            .ToListAsync(cancellationToken);
        records = records.Where(item =>
            item.Scope == nameof(PolicyScope.Global)
            || (item.Scope == nameof(PolicyScope.Environment)
                && item.Environment.Equals(environment, StringComparison.OrdinalIgnoreCase))
            || (item.Scope == nameof(PolicyScope.Tenant) && tenantId.HasValue && item.TenantId == tenantId)).ToList();
        return await HydrateAsync(records, cancellationToken);
    }

    public async Task AddPolicyAsync(PolicyDefinitionState policy, CancellationToken cancellationToken = default)
    {
        db.PolicyDefinitions.Add(MapRecord(policy));
        db.PolicyRules.AddRange(policy.Rules.Select(MapRecord));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new ResourceConflictException("policy.version.conflict", $"Policy '{policy.Name}' version {policy.Version} already exists. {exception.GetBaseException().Message}");
        }
    }

    public async Task UpdatePolicyAsync(PolicyDefinitionState policy, long expectedRevision, CancellationToken cancellationToken = default)
    {
        await ApplyUpdateAsync(policy, expectedRevision, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("policy", policy.Id);
        }
    }

    public async Task ActivateVersionAsync(
        PolicyDefinitionState policy,
        long expectedRevision,
        IReadOnlyList<PolicyVersionUpdate> retiredVersions,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var retired in retiredVersions)
                await ApplyUpdateAsync(retired.State, retired.ExpectedRevision, cancellationToken);
            await ApplyUpdateAsync(policy, expectedRevision, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConcurrencyConflictException("policy", policy.Id);
        }
    }

    private async Task ApplyUpdateAsync(
        PolicyDefinitionState policy,
        long expectedRevision,
        CancellationToken cancellationToken)
    {
        var record = await db.PolicyDefinitions.SingleOrDefaultAsync(item => item.Id == policy.Id, cancellationToken)
            ?? throw new ResourceNotFoundException("policy.not_found", $"Policy version '{policy.Id}' was not found.");
        if (record.Revision != expectedRevision)
            throw new ConcurrencyConflictException("policy", policy.Id);

        record.Name = policy.Name;
        record.Description = policy.Description;
        record.Owner = policy.Owner;
        record.Domain = policy.Domain.ToString();
        record.Status = policy.Status.ToString();
        record.Scope = policy.Scope.ToString();
        record.Environment = policy.Environment;
        record.TenantId = policy.TenantId;
        record.DefaultEffect = policy.DefaultEffect.ToString();
        record.Revision = policy.Revision;
        record.UpdatedAt = policy.UpdatedAt;
        record.ActivatedAt = policy.ActivatedAt;

        var existingRules = await db.PolicyRules.Where(item => item.PolicyId == policy.Id).ToListAsync(cancellationToken);
        db.PolicyRules.RemoveRange(existingRules);
        db.PolicyRules.AddRange(policy.Rules.Select(MapRecord));
    }

    public async Task<IReadOnlyList<PolicyDecisionState>> ListDecisionsAsync(
        int limit = 250,
        Guid? policyId = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.PolicyDecisions.AsNoTracking().AsQueryable();
        if (policyId.HasValue) query = query.Where(item => item.PolicyId == policyId);
        var records = await query.ToListAsync(cancellationToken);
        return records.OrderByDescending(item => item.CreatedAt).Take(limit).Select(Map).ToList();
    }

    public async Task AddDecisionAsync(PolicyDecisionState decision, CancellationToken cancellationToken = default)
    {
        db.PolicyDecisions.Add(MapRecord(decision));
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<PolicyDefinitionState>> HydrateAsync(
        IReadOnlyList<PolicyDefinitionRecord> records,
        CancellationToken cancellationToken)
    {
        if (records.Count == 0) return [];
        var ids = records.Select(item => item.Id).ToList();
        var rules = await db.PolicyRules.AsNoTracking()
            .Where(item => ids.Contains(item.PolicyId))
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return records.Select(record => Map(record, rules.Where(rule => rule.PolicyId == record.Id))).ToList();
    }

    private static PolicyDefinitionState Map(PolicyDefinitionRecord record, IEnumerable<PolicyRuleRecord> rules)
        => new(
            record.Id,
            record.PolicyKey,
            record.Version,
            record.Name,
            record.Description,
            record.Owner,
            Parse(record.Domain, PolicyDomain.Compliance),
            Parse(record.Status, PolicyStatus.Draft),
            Parse(record.Scope, PolicyScope.Global),
            record.Environment,
            record.TenantId,
            Parse(record.DefaultEffect, PolicyEffect.Deny),
            record.Revision,
            record.CreatedAt,
            record.UpdatedAt,
            record.ActivatedAt,
            rules.Select(Map).ToList());

    private static PolicyRuleState Map(PolicyRuleRecord record)
        => new(record.Id, record.PolicyId, record.Name, Parse(record.Effect, PolicyEffect.Deny), record.Priority,
            Deserialize(record.MatchJson), Deserialize(record.ConstraintsJson));

    private static PolicyDecisionState Map(PolicyDecisionRecord record)
        => new(record.Id, record.PolicyId, record.PolicyKey, record.PolicyVersion, record.PolicyName,
            Parse(record.Domain, PolicyDomain.Compliance), Parse(record.Effect, PolicyEffect.Allow), record.Reason,
            Deserialize(record.ContextJson), Deserialize(record.ConstraintsJson), record.Source,
            record.CorrelationId, record.SimulationId, record.RunId, record.CreatedAt);

    private static PolicyDefinitionRecord MapRecord(PolicyDefinitionState state) => new()
    {
        Id = state.Id,
        PolicyKey = state.PolicyKey,
        Version = state.Version,
        Name = state.Name,
        Description = state.Description,
        Owner = state.Owner,
        Domain = state.Domain.ToString(),
        Status = state.Status.ToString(),
        Scope = state.Scope.ToString(),
        Environment = state.Environment,
        TenantId = state.TenantId,
        DefaultEffect = state.DefaultEffect.ToString(),
        Revision = state.Revision,
        CreatedAt = state.CreatedAt,
        UpdatedAt = state.UpdatedAt,
        ActivatedAt = state.ActivatedAt
    };

    private static PolicyRuleRecord MapRecord(PolicyRuleState state) => new()
    {
        Id = state.Id,
        PolicyId = state.PolicyId,
        Name = state.Name,
        Effect = state.Effect.ToString(),
        Priority = state.Priority,
        MatchJson = JsonSerializer.Serialize(state.Match, JsonOptions),
        ConstraintsJson = JsonSerializer.Serialize(state.Constraints, JsonOptions)
    };

    private static PolicyDecisionRecord MapRecord(PolicyDecisionState state) => new()
    {
        Id = state.Id,
        PolicyId = state.PolicyId,
        PolicyKey = state.PolicyKey,
        PolicyVersion = state.PolicyVersion,
        PolicyName = state.PolicyName,
        Domain = state.Domain.ToString(),
        Effect = state.Effect.ToString(),
        Reason = state.Reason,
        ContextJson = JsonSerializer.Serialize(state.Context, JsonOptions),
        ConstraintsJson = JsonSerializer.Serialize(state.Constraints, JsonOptions),
        Source = state.Source,
        CorrelationId = state.CorrelationId,
        SimulationId = state.SimulationId,
        RunId = state.RunId,
        CreatedAt = state.CreatedAt
    };

    private static IReadOnlyDictionary<string, string> Deserialize(string json)
        => JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
            ?? new Dictionary<string, string>();

    private static T Parse<T>(string value, T fallback) where T : struct, Enum
        => Enum.TryParse<T>(value, true, out var parsed) ? parsed : fallback;
}
