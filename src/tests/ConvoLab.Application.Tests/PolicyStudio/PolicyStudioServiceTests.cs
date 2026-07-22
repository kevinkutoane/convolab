using ConvoLab.Application.PolicyStudio;
using ConvoLab.Domain.Policy.Enums;

namespace ConvoLab.Application.Tests.PolicyStudio;

public sealed class PolicyStudioServiceTests
{
    [Fact]
    public async Task Runtime_guardrails_apply_most_restrictive_constraints()
    {
        var repository = new InMemoryPolicyRepository([
            Policy(
                "Budget envelope",
                PolicyDomain.BudgetLimit,
                PolicyEffect.Allow,
                [Rule("Constrain simulator", PolicyEffect.AllowWithConstraints, 100,
                    new Dictionary<string, string> { ["source"] = "ConversationSimulator" },
                    new Dictionary<string, string>
                    {
                        ["maxCostPerExecution"] = "0.02",
                        ["maxOutputTokens"] = "512",
                        ["allowFallback"] = "false"
                    })])
        ]);
        var service = new PolicyStudioService(repository);

        var result = await service.EvaluateExecutionAsync(new PolicyExecutionRequest(
            "Deterministic", "Primary", .05m, "ZAR", 2000, true, true,
            "ConversationSimulator", "Development"));

        Assert.True(result.IsAllowed);
        Assert.Equal(.02m, result.MaxCostPerExecution);
        Assert.Equal(512, result.MaxOutputTokens);
        Assert.False(result.AllowFallback);
        Assert.True(result.AllowStreaming);
        Assert.Contains(result.Decisions, item => item.Effect == PolicyEffect.AllowWithConstraints);
    }

    [Fact]
    public async Task Provider_deny_policy_blocks_runtime_execution()
    {
        var repository = new InMemoryPolicyRepository([
            Policy(
                "Approved providers",
                PolicyDomain.ProviderAccess,
                PolicyEffect.Deny,
                [Rule("Allow deterministic", PolicyEffect.Allow, 100,
                    new Dictionary<string, string> { ["provider"] = "Deterministic" },
                    new Dictionary<string, string>())])
        ]);
        var service = new PolicyStudioService(repository);

        var result = await service.EvaluateExecutionAsync(new PolicyExecutionRequest(
            "Gemini", "gemini-test", .01m, "ZAR", 400, true, true,
            "ConversationSimulator", "Development"));

        Assert.False(result.IsAllowed);
        Assert.Contains("default effect", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Decisions, item => item.Domain == PolicyDomain.ProviderAccess && item.Effect == PolicyEffect.Deny);
    }

    [Fact]
    public async Task Activating_new_version_retires_previous_active_version()
    {
        var key = Guid.NewGuid();
        var active = Policy("Model rules", PolicyDomain.ModelAccess, PolicyEffect.Allow, [], key, 1, PolicyStatus.Active);
        var draft = Policy("Model rules", PolicyDomain.ModelAccess, PolicyEffect.Allow, [], key, 2, PolicyStatus.Draft);
        var repository = new InMemoryPolicyRepository([active, draft]);
        var service = new PolicyStudioService(repository);

        var result = await service.TransitionAsync(draft.Id, "activate");
        var versions = await repository.GetVersionHistoryAsync(key);

        Assert.Equal(PolicyStatus.Active, result.Summary.Status);
        Assert.Equal(PolicyStatus.Retired, versions.Single(item => item.Id == active.Id).Status);
    }

    private static PolicyDefinitionState Policy(
        string name,
        PolicyDomain domain,
        PolicyEffect defaultEffect,
        IReadOnlyList<PolicyRuleState> rules,
        Guid? key = null,
        int version = 1,
        PolicyStatus status = PolicyStatus.Active)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        return new PolicyDefinitionState(
            id, key ?? Guid.NewGuid(), version, name, "Test policy", "Test",
            domain, status, PolicyScope.Global, "All", null, defaultEffect,
            1, now, now, status == PolicyStatus.Active ? now : null,
            rules.Select(rule => rule with { PolicyId = id }).ToList());
    }

    private static PolicyRuleState Rule(
        string name,
        PolicyEffect effect,
        int priority,
        IReadOnlyDictionary<string, string> match,
        IReadOnlyDictionary<string, string> constraints)
        => new(Guid.NewGuid(), Guid.Empty, name, effect, priority, match, constraints);

    private sealed class InMemoryPolicyRepository(IReadOnlyList<PolicyDefinitionState> initial) : IPolicyStudioRepository
    {
        private readonly List<PolicyDefinitionState> _policies = initial.ToList();
        private readonly List<PolicyDecisionState> _decisions = [];

        public Task<int> CountPoliciesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_policies.Count);

        public Task<IReadOnlyList<PolicyDefinitionState>> ListPoliciesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PolicyDefinitionState>>(_policies.OrderByDescending(item => item.UpdatedAt).ToList());

        public Task<PolicyDefinitionState?> GetPolicyAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_policies.SingleOrDefault(item => item.Id == id));

        public Task<IReadOnlyList<PolicyDefinitionState>> GetVersionHistoryAsync(Guid policyKey, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PolicyDefinitionState>>(_policies.Where(item => item.PolicyKey == policyKey).OrderByDescending(item => item.Version).ToList());

        public Task<IReadOnlyList<PolicyDefinitionState>> ListActiveByDomainAsync(PolicyDomain domain, Guid? tenantId, string environment, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PolicyDefinitionState>>(_policies.Where(item => item.Domain == domain && item.Status == PolicyStatus.Active).ToList());

        public Task AddPolicyAsync(PolicyDefinitionState policy, CancellationToken cancellationToken = default)
        {
            _policies.Add(policy);
            return Task.CompletedTask;
        }

        public Task UpdatePolicyAsync(PolicyDefinitionState policy, long expectedRevision, CancellationToken cancellationToken = default)
        {
            var index = _policies.FindIndex(item => item.Id == policy.Id);
            _policies[index] = policy;
            return Task.CompletedTask;
        }

        public Task ActivateVersionAsync(
            PolicyDefinitionState policy,
            long expectedRevision,
            IReadOnlyList<PolicyVersionUpdate> retiredVersions,
            CancellationToken cancellationToken = default)
        {
            foreach (var retirement in retiredVersions)
            {
                var retiredIndex = _policies.FindIndex(item => item.Id == retirement.State.Id);
                _policies[retiredIndex] = retirement.State;
            }

            var activeIndex = _policies.FindIndex(item => item.Id == policy.Id);
            _policies[activeIndex] = policy;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PolicyDecisionState>> ListDecisionsAsync(int limit = 250, Guid? policyId = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PolicyDecisionState>>(_decisions.Where(item => !policyId.HasValue || item.PolicyId == policyId).OrderByDescending(item => item.CreatedAt).Take(limit).ToList());

        public Task AddDecisionAsync(PolicyDecisionState decision, CancellationToken cancellationToken = default)
        {
            _decisions.Add(decision);
            return Task.CompletedTask;
        }
    }
}
