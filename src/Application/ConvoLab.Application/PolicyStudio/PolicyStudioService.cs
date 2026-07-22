using System.Globalization;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Domain.Policy.Aggregates;
using ConvoLab.Domain.Policy.Enums;
using ConvoLab.Domain.Policy.ValueObjects;

namespace ConvoLab.Application.PolicyStudio;

public sealed class PolicyStudioService(IPolicyStudioRepository repository) : IPolicyStudioService
{
    private static readonly SemaphoreSlim SeedGate = new(1, 1);
    public async Task<PolicyOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(cancellationToken);
        var policies = await repository.ListPoliciesAsync(cancellationToken);
        var decisions = await repository.ListDecisionsAsync(500, cancellationToken: cancellationToken);
        var summaries = policies.Select(MapSummary).OrderByDescending(item => item.UpdatedAt).ToList();
        var logical = policies.Select(item => item.PolicyKey).Distinct().Count();
        var denials = decisions.Count(item => item.Effect == PolicyEffect.Deny);
        var constrained = decisions.Count(item => item.Effect == PolicyEffect.AllowWithConstraints);
        var metrics = new PolicyMetricsDto(
            logical,
            policies.Count,
            policies.Count(item => item.Status == PolicyStatus.Active),
            policies.Count(item => item.Status == PolicyStatus.Draft),
            decisions.Count,
            denials,
            constrained,
            decisions.Count == 0 ? 0 : (double)denials / decisions.Count);

        var coverage = Enum.GetValues<PolicyDomain>().Select(domain =>
        {
            var active = policies.Count(item => item.Domain == domain && item.Status == PolicyStatus.Active);
            var domainDecisions = decisions.Where(item => item.Domain == domain).ToList();
            var domainDenials = domainDecisions.Count(item => item.Effect == PolicyEffect.Deny);
            return new PolicyCoverageDto(
                domain,
                active,
                domainDecisions.Count,
                domainDenials,
                domainDecisions.Count == 0 ? 0 : (double)domainDenials / domainDecisions.Count,
                active > 0 ? "Covered" : "Uncovered");
        }).ToList();

        return new PolicyOverviewDto(
            metrics,
            summaries,
            decisions.Take(40).Select(MapDecision).ToList(),
            coverage,
            policies.Select(item => item.Environment).Append("All").Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item).ToList(),
            DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<PolicySummaryDto>> ListPoliciesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(cancellationToken);
        return (await repository.ListPoliciesAsync(cancellationToken)).Select(MapSummary).OrderByDescending(item => item.UpdatedAt).ToList();
    }

    public async Task<PolicyDetailDto> GetPolicyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(cancellationToken);
        var state = await repository.GetPolicyAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("policy.not_found", $"Policy version '{id}' was not found.");
        var decisions = await repository.ListDecisionsAsync(80, id, cancellationToken);
        var versions = await repository.GetVersionHistoryAsync(state.PolicyKey, cancellationToken);
        return new PolicyDetailDto(
            MapSummary(state),
            state.Rules.OrderByDescending(item => item.Priority).Select(MapRule).ToList(),
            decisions.Select(MapDecision).ToList(),
            versions.Select(MapSummary).ToList());
    }

    public async Task<PolicyDetailDto> CreatePolicyAsync(CreatePolicyCommand command, CancellationToken cancellationToken = default)
    {
        var rules = command.Rules ?? [];
        ValidateCommand(command.Name, command.Owner, rules);
        PolicyDefinition aggregate;
        try
        {
            aggregate = PolicyDefinition.Create(
                command.Name,
                command.Domain,
                command.DefaultEffect,
                command.TenantId,
                command.Description,
                command.Owner,
                command.Scope,
                command.Environment);
            aggregate.ReplaceRules(rules.Select(MapRule));
        }
        catch (ArgumentException exception)
        {
            throw new RequestValidationException("policy.definition.invalid", exception.Message);
        }

        await repository.AddPolicyAsync(MapState(aggregate), cancellationToken);
        return await GetPolicyAsync(aggregate.Id.Value, cancellationToken);
    }

    public async Task<PolicyDetailDto> UpdatePolicyAsync(Guid id, UpdatePolicyCommand command, CancellationToken cancellationToken = default)
    {
        var rules = command.Rules ?? [];
        ValidateCommand(command.Name, command.Owner, rules);
        var state = await repository.GetPolicyAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("policy.not_found", $"Policy version '{id}' was not found.");
        var aggregate = Restore(state);
        try
        {
            aggregate.UpdateDraft(command.Name, command.Description, command.Owner, command.DefaultEffect,
                command.Scope, command.Environment, command.TenantId);
            aggregate.ReplaceRules(rules.Select(MapRule));
        }
        catch (ArgumentException exception)
        {
            throw new RequestValidationException("policy.definition.invalid", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            throw new DomainRuleViolationException("policy.version.immutable", exception.Message);
        }

        await repository.UpdatePolicyAsync(MapState(aggregate), command.Revision, cancellationToken);
        return await GetPolicyAsync(id, cancellationToken);
    }

    public async Task<PolicyDetailDto> CreateVersionAsync(Guid id, CreatePolicyVersionCommand command, CancellationToken cancellationToken = default)
    {
        var state = await repository.GetPolicyAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("policy.not_found", $"Policy version '{id}' was not found.");
        PolicyDefinition next;
        try
        {
            next = Restore(state).CreateNextVersion(command.Owner);
        }
        catch (InvalidOperationException exception)
        {
            throw new DomainRuleViolationException("policy.version.invalid_source", exception.Message);
        }

        await repository.AddPolicyAsync(MapState(next), cancellationToken);
        return await GetPolicyAsync(next.Id.Value, cancellationToken);
    }

    public async Task<PolicyDetailDto> TransitionAsync(Guid id, string action, CancellationToken cancellationToken = default)
    {
        var state = await repository.GetPolicyAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("policy.not_found", $"Policy version '{id}' was not found.");
        var aggregate = Restore(state);
        try
        {
            switch (action.Trim().ToLowerInvariant())
            {
                case "submit":
                    aggregate.SubmitForApproval();
                    break;
                case "activate":
                    if (state.Status == PolicyStatus.Retired)
                        throw new InvalidOperationException("A retired policy cannot be activated.");
                    var retiredVersions = await BuildRetiredVersionUpdatesAsync(state, cancellationToken);
                    aggregate.Activate();
                    await repository.ActivateVersionAsync(
                        MapState(aggregate),
                        state.Revision,
                        retiredVersions,
                        cancellationToken);
                    return await GetPolicyAsync(id, cancellationToken);
                case "suspend":
                    aggregate.Suspend();
                    break;
                case "retire":
                    aggregate.Retire();
                    break;
                default:
                    throw new RequestValidationException("policy.action.invalid", $"Unsupported policy lifecycle action '{action}'.");
            }
        }
        catch (InvalidOperationException exception)
        {
            throw new DomainRuleViolationException("policy.lifecycle.invalid_transition", exception.Message);
        }

        await repository.UpdatePolicyAsync(MapState(aggregate), state.Revision, cancellationToken);
        return await GetPolicyAsync(id, cancellationToken);
    }

    public async Task<PolicyEvaluationResultDto> EvaluateAsync(EvaluatePolicyCommand command, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(cancellationToken);
        var attributes = command.Attributes ?? new Dictionary<string, string>();
        var environment = GetValue(attributes, "environment") ?? "Development";
        var policies = await repository.ListActiveByDomainAsync(command.Domain, command.TenantId, environment, cancellationToken);
        var correlationId = string.IsNullOrWhiteSpace(command.CorrelationId) ? Guid.NewGuid().ToString("N") : command.CorrelationId.Trim();
        var context = PolicyEvaluationContext.Create(command.Domain, command.TenantId,
            attributes.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase));
        var persisted = new List<PolicyDecisionDto>();

        foreach (var state in policies.OrderBy(item => ScopeOrder(item.Scope)).ThenBy(item => item.Version))
        {
            var decision = Restore(state).Evaluate(context);
            var decisionState = new PolicyDecisionState(
                Guid.NewGuid(), state.Id, state.PolicyKey, state.Version, state.Name,
                command.Domain, decision.Effect, decision.Reason,
                attributes.ToDictionary(item => item.Key, item => item.Value),
                decision.Constraints.ToDictionary(item => item.Key, item => item.Value),
                string.IsNullOrWhiteSpace(command.Source) ? "Manual" : command.Source.Trim(),
                correlationId, command.SimulationId, command.RunId, DateTimeOffset.UtcNow);
            await repository.AddDecisionAsync(decisionState, cancellationToken);
            persisted.Add(MapDecision(decisionState));
        }

        if (persisted.Count == 0)
        {
            var noPolicy = new PolicyDecisionState(
                Guid.NewGuid(), null, null, null, "No active policy",
                command.Domain, PolicyEffect.Allow,
                $"No active policy governs {command.Domain}; execution was allowed.",
                attributes.ToDictionary(item => item.Key, item => item.Value),
                new Dictionary<string, string>(),
                string.IsNullOrWhiteSpace(command.Source) ? "Manual" : command.Source.Trim(),
                correlationId, command.SimulationId, command.RunId, DateTimeOffset.UtcNow);
            await repository.AddDecisionAsync(noPolicy, cancellationToken);
            persisted.Add(MapDecision(noPolicy));
        }

        var denied = persisted.FirstOrDefault(item => item.Effect == PolicyEffect.Deny);
        var constraints = MergeConstraints(persisted.Where(item => item.Effect == PolicyEffect.AllowWithConstraints));
        var effect = denied is not null
            ? PolicyEffect.Deny
            : constraints.Count > 0 ? PolicyEffect.AllowWithConstraints : PolicyEffect.Allow;
        var reason = denied?.Reason
            ?? (constraints.Count > 0
                ? $"{persisted.Count} policy decision(s) allowed execution with constraints."
                : $"{persisted.Count} policy decision(s) allowed execution.");
        return new PolicyEvaluationResultDto(effect, effect != PolicyEffect.Deny, reason, constraints, persisted, correlationId, DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<PolicyDecisionDto>> ListDecisionsAsync(int limit = 250, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(cancellationToken);
        return (await repository.ListDecisionsAsync(Math.Clamp(limit, 1, 1000), cancellationToken: cancellationToken)).Select(MapDecision).ToList();
    }

    public async Task<PolicyExecutionGuardrails> EvaluateExecutionAsync(PolicyExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var common = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["provider"] = request.Provider,
            ["model"] = request.Model,
            ["estimatedCost"] = request.EstimatedCost.ToString(CultureInfo.InvariantCulture),
            ["currency"] = NormalizeCurrency(request.Currency),
            ["requestedMaxOutputTokens"] = request.RequestedMaxOutputTokens.ToString(CultureInfo.InvariantCulture),
            ["source"] = request.Source,
            ["environment"] = request.Environment,
            ["allowFallback"] = request.AllowFallback.ToString(),
            ["allowStreaming"] = request.AllowStreaming.ToString()
        };

        var results = new List<PolicyEvaluationResultDto>();
        foreach (var domain in new[] { PolicyDomain.ProviderAccess, PolicyDomain.ModelAccess, PolicyDomain.BudgetLimit, PolicyDomain.Safety })
        {
            results.Add(await EvaluateAsync(new EvaluatePolicyCommand(
                domain, request.TenantId, common, request.Source, correlationId,
                request.SimulationId, request.RunId), cancellationToken));
        }

        var decisions = results.SelectMany(item => item.Decisions).ToList();
        var denied = results.FirstOrDefault(item => !item.IsAllowed);
        var constraints = MergeConstraints(decisions.Where(item => item.Effect == PolicyEffect.AllowWithConstraints));
        var maxCost = Math.Min(1.00m, ParseDecimal(constraints, "maxCostPerExecution", 1.00m));
        var maxTokens = Math.Min(request.RequestedMaxOutputTokens, ParseInt(constraints, "maxOutputTokens", request.RequestedMaxOutputTokens));
        var fallback = request.AllowFallback && ParseBool(constraints, "allowFallback", true);
        var streaming = request.AllowStreaming && ParseBool(constraints, "allowStreaming", true);
        return new PolicyExecutionGuardrails(
            denied is null,
            denied?.Reason ?? (constraints.Count > 0 ? "Runtime execution allowed with policy constraints." : "Runtime execution allowed."),
            maxCost,
            NormalizeCurrency(request.Currency),
            Math.Clamp(maxTokens, 32, 8192),
            fallback,
            streaming,
            decisions,
            correlationId);
    }

    private async Task EnsureDefaultsAsync(CancellationToken cancellationToken)
    {
        var requiredNames = new[]
        {
            "Studio provider access",
            "Studio model access",
            "Simulator execution envelope",
            "Baseline safety posture"
        };
        var current = await repository.ListPoliciesAsync(cancellationToken);
        if (requiredNames.All(name => current.Any(policy => policy.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))) return;

        await SeedGate.WaitAsync(cancellationToken);
        try
        {
            current = await repository.ListPoliciesAsync(cancellationToken);
            if (requiredNames.All(name => current.Any(policy => policy.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))) return;

            var defaults = new List<PolicyDefinition>();

            var providers = PolicyDefinition.Create(
                "Studio provider access",
                PolicyDomain.ProviderAccess,
                PolicyEffect.Allow,
                description: "Allows configured Studio providers unless a stricter environment or tenant policy denies access.");
            providers.Activate();
            defaults.Add(providers);

            var models = PolicyDefinition.Create(
                "Studio model access",
                PolicyDomain.ModelAccess,
                PolicyEffect.Allow,
                description: "Allows models selected from the canonical Intelligence Center catalogue.");
            models.Activate();
            defaults.Add(models);

            var budget = PolicyDefinition.Create(
                "Simulator execution envelope",
                PolicyDomain.BudgetLimit,
                PolicyEffect.Allow,
                description: "Constrains Conversation Simulator and Replay Studio execution cost, output size, streaming, and fallback.");
            budget.AddRule(PolicyRule.Create(
                "Constrain Studio execution",
                PolicyEffect.AllowWithConstraints,
                100,
                new Dictionary<string, string> { ["source"] = "ConversationSimulator" },
                new Dictionary<string, string>
                {
                    ["maxCostPerExecution"] = "1.00",
                    ["currency"] = "ZAR",
                    ["maxOutputTokens"] = "2048",
                    ["allowFallback"] = "true",
                    ["allowStreaming"] = "true"
                }));
            budget.AddRule(PolicyRule.Create(
                "Constrain replay execution",
                PolicyEffect.AllowWithConstraints,
                100,
                new Dictionary<string, string> { ["source"] = "ReplayStudio" },
                new Dictionary<string, string>
                {
                    ["maxCostPerExecution"] = "1.00",
                    ["currency"] = "ZAR",
                    ["maxOutputTokens"] = "2048",
                    ["allowFallback"] = "true",
                    ["allowStreaming"] = "true"
                }));
            budget.Activate();
            defaults.Add(budget);

            var safety = PolicyDefinition.Create(
                "Baseline safety posture",
                PolicyDomain.Safety,
                PolicyEffect.Allow,
                description: "Provides the central extension point for runtime safety rules without embedding them in providers.");
            safety.Activate();
            defaults.Add(safety);

            foreach (var policy in defaults.Where(seed =>
                         current.All(existing => !existing.Name.Equals(seed.Name, StringComparison.OrdinalIgnoreCase))))
                await repository.AddPolicyAsync(MapState(policy), cancellationToken);
        }
        finally
        {
            SeedGate.Release();
        }
    }

    private async Task<IReadOnlyList<PolicyVersionUpdate>> BuildRetiredVersionUpdatesAsync(
        PolicyDefinitionState state,
        CancellationToken cancellationToken)
    {
        var versions = await repository.GetVersionHistoryAsync(state.PolicyKey, cancellationToken);
        var updates = new List<PolicyVersionUpdate>();
        foreach (var other in versions.Where(item => item.Id != state.Id && item.Status == PolicyStatus.Active))
        {
            var aggregate = Restore(other);
            aggregate.Retire();
            updates.Add(new PolicyVersionUpdate(MapState(aggregate), other.Revision));
        }
        return updates;
    }

    private static string NormalizeCurrency(string? currency)
        => "ZAR";

    private static PolicyDefinition Restore(PolicyDefinitionState state)
        => PolicyDefinition.Restore(
            PolicyDefinitionId.FromGuid(state.Id), state.PolicyKey, state.Version,
            state.Name, state.Description, state.Owner, state.Domain, state.DefaultEffect,
            state.Scope, state.Environment, state.TenantId, state.Status, state.Revision,
            state.CreatedAt, state.UpdatedAt, state.ActivatedAt,
            state.Rules.Select(rule => PolicyRule.Create(rule.Name, rule.Effect, rule.Priority,
                rule.Match.ToDictionary(item => item.Key, item => item.Value),
                rule.Constraints.ToDictionary(item => item.Key, item => item.Value))));

    private static PolicyDefinitionState MapState(PolicyDefinition aggregate)
        => new(
            aggregate.Id.Value, aggregate.PolicyKey, aggregate.Version, aggregate.Name,
            aggregate.Description, aggregate.Owner, aggregate.Domain, aggregate.Status,
            aggregate.Scope, aggregate.Environment, aggregate.TenantId, aggregate.DefaultEffect,
            aggregate.Revision, aggregate.CreatedAt, aggregate.UpdatedAt, aggregate.ActivatedAt,
            aggregate.Rules.Select(rule => new PolicyRuleState(
                Guid.NewGuid(), aggregate.Id.Value, rule.Name, rule.Effect, rule.Priority,
                rule.Match.ToDictionary(item => item.Key, item => item.Value),
                rule.Constraints.ToDictionary(item => item.Key, item => item.Value))).ToList());

    private static PolicySummaryDto MapSummary(PolicyDefinitionState state)
        => new(state.Id, state.PolicyKey, state.Version, state.Name, state.Description,
            state.Owner, state.Domain, state.Status, state.Scope, state.Environment, state.TenantId,
            state.DefaultEffect, state.Rules.Count, state.Revision, state.CreatedAt, state.UpdatedAt, state.ActivatedAt);

    private static PolicyRuleDto MapRule(PolicyRuleState state)
        => new(state.Name, state.Effect, state.Priority, state.Match, state.Constraints);

    private static PolicyRule MapRule(PolicyRuleInput input)
        => PolicyRule.Create(
            input.Name,
            input.Effect,
            input.Priority,
            (input.Match ?? new Dictionary<string, string>()).ToDictionary(item => item.Key, item => item.Value),
            (input.Constraints ?? new Dictionary<string, string>()).ToDictionary(item => item.Key, item => item.Value));

    private static PolicyDecisionDto MapDecision(PolicyDecisionState state)
        => new(state.Id, state.PolicyId, state.PolicyKey, state.PolicyVersion, state.PolicyName,
            state.Domain, state.Effect, state.Reason, state.Context, state.Constraints,
            state.Source, state.CorrelationId, state.SimulationId, state.RunId, state.CreatedAt);

    private static Dictionary<string, string> MergeConstraints(IEnumerable<PolicyDecisionDto> decisions)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var decision in decisions)
            foreach (var constraint in decision.Constraints)
                merged[constraint.Key] = MoreRestrictive(constraint.Key, merged.GetValueOrDefault(constraint.Key), constraint.Value);
        return merged;
    }

    private static string MoreRestrictive(string key, string? current, string candidate)
    {
        if (current is null) return candidate;
        if (key.Equals("maxCostPerExecution", StringComparison.OrdinalIgnoreCase)
            && decimal.TryParse(current, NumberStyles.Number, CultureInfo.InvariantCulture, out var currentMoney)
            && decimal.TryParse(candidate, NumberStyles.Number, CultureInfo.InvariantCulture, out var candidateMoney))
            return Math.Min(currentMoney, candidateMoney).ToString(CultureInfo.InvariantCulture);
        if (key.Equals("maxOutputTokens", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(current, out var currentTokens) && int.TryParse(candidate, out var candidateTokens))
            return Math.Min(currentTokens, candidateTokens).ToString(CultureInfo.InvariantCulture);
        if ((key.Equals("allowFallback", StringComparison.OrdinalIgnoreCase) || key.Equals("allowStreaming", StringComparison.OrdinalIgnoreCase))
            && bool.TryParse(current, out var currentBool) && bool.TryParse(candidate, out var candidateBool))
            return (currentBool && candidateBool).ToString().ToLowerInvariant();
        return candidate;
    }

    private static int ScopeOrder(PolicyScope scope) => scope switch
    {
        PolicyScope.Global => 0,
        PolicyScope.Environment => 1,
        PolicyScope.Tenant => 2,
        _ => 0
    };

    private static string? GetValue(IReadOnlyDictionary<string, string> values, string key)
        => values.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;

    private static decimal ParseDecimal(IReadOnlyDictionary<string, string> values, string key, decimal fallback)
        => values.TryGetValue(key, out var value)
            && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                ? parsed : fallback;

    private static int ParseInt(IReadOnlyDictionary<string, string> values, string key, int fallback)
        => values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    private static bool ParseBool(IReadOnlyDictionary<string, string> values, string key, bool fallback)
        => values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback;

    private static void ValidateCommand(string name, string owner, IReadOnlyList<PolicyRuleInput> rules)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new RequestValidationException("policy.name.required", "Policy name is required.");
        if (string.IsNullOrWhiteSpace(owner)) throw new RequestValidationException("policy.owner.required", "Policy owner is required.");
        if (rules.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new RequestValidationException("policy.rules.duplicate", "Rule names must be unique within a policy version.");
        if (rules.Any(item => string.IsNullOrWhiteSpace(item.Name)))
            throw new RequestValidationException("policy.rule.name.required", "Every policy rule requires a name.");
    }
}
