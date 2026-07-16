using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Prompt.Entities;

/// <summary>
/// Defines a governance policy applied to a prompt, such as requiring approval
/// before activation or restricting usage to specific environments.
/// </summary>
public class PromptPolicy : BaseEntity<Guid>
{
    public string Name { get; private set; }
    public string PolicyType { get; private set; }
    public string Configuration { get; private set; }
    public bool IsActive { get; private set; }

    private PromptPolicy() { Name = null!; PolicyType = null!; Configuration = null!; }

    private PromptPolicy(Guid id, string name, string policyType, string configuration) : base(id)
    {
        Name = name;
        PolicyType = policyType;
        Configuration = configuration;
        IsActive = true;
    }

    public static PromptPolicy RequireApproval()
        => new(Guid.NewGuid(), "RequireApproval", "Governance", "{\"approvalRequired\": true}");

    public static PromptPolicy RestrictToEnvironment(string environment)
        => new(Guid.NewGuid(), $"RestrictTo:{environment}", "Deployment", $"{{\"environment\": \"{environment}\"}}");

    public static PromptPolicy Create(string name, string policyType, string configuration)
        => new(Guid.NewGuid(), name, policyType, configuration);

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
