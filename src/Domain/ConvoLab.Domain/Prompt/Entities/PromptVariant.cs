using ConvoLab.Domain.Common;
using ConvoLab.Domain.Prompt.ValueObjects;

namespace ConvoLab.Domain.Prompt.Entities;

/// <summary>
/// Represents a named variant of a prompt, used in A/B testing experiments.
/// A variant points to a specific PromptVersionId and carries a traffic weight.
/// </summary>
public class PromptVariant : BaseEntity<Guid>
{
    public string Name { get; private set; }
    public PromptVersionId VersionId { get; private set; }
    public int TrafficWeight { get; private set; }
    public string? Description { get; private set; }

    private PromptVariant() { Name = null!; VersionId = null!; }

    private PromptVariant(Guid id, string name, PromptVersionId versionId, int trafficWeight, string? description) : base(id)
    {
        Name = name;
        VersionId = versionId;
        TrafficWeight = trafficWeight;
        Description = description;
    }

    public static PromptVariant Create(string name, PromptVersionId versionId, int trafficWeight, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Variant name cannot be empty.", nameof(name));
        if (trafficWeight < 0 || trafficWeight > 100)
            throw new ArgumentException("Traffic weight must be between 0 and 100.", nameof(trafficWeight));

        return new PromptVariant(Guid.NewGuid(), name, versionId, trafficWeight, description);
    }
}
