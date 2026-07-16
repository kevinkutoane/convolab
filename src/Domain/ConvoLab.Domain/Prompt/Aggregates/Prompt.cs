using ConvoLab.Domain.Common;
using ConvoLab.Domain.Prompt.Entities;
using ConvoLab.Domain.Prompt.Enums;
using ConvoLab.Domain.Prompt.Events;
using ConvoLab.Domain.Prompt.ValueObjects;

namespace ConvoLab.Domain.Prompt.Aggregates;

/// <summary>
/// The Prompt aggregate root. Represents an enterprise prompt asset with full lifecycle
/// governance: versioning, approval, composition, and experimentation.
/// 
/// Core invariants:
/// - Prompts are immutable. Editing always creates a new PromptVersion.
/// - A prompt must have an Active version to be rendered.
/// - Archiving is only allowed when the prompt is not Active.
/// </summary>
public class Prompt : BaseAggregateRoot<PromptId>
{
    public string Name { get; private set; }
    public PromptStatus Status { get; private set; }
    public PromptOwner Owner { get; private set; }
    public PromptMetadata Metadata { get; private set; }
    public PromptVersionId? ActiveVersionId { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<PromptVersion> _versions = new();
    private readonly List<PromptSection> _sections = new();
    private readonly List<PromptPolicy> _policies = new();
    private readonly List<PromptVariant> _variants = new();

    public IReadOnlyCollection<PromptVersion> Versions => _versions.AsReadOnly();
    public IReadOnlyCollection<PromptSection> Sections => _sections.AsReadOnly();
    public IReadOnlyCollection<PromptPolicy> Policies => _policies.AsReadOnly();
    public IReadOnlyCollection<PromptVariant> Variants => _variants.AsReadOnly();

    private Prompt() { Name = null!; Owner = null!; Metadata = null!; }

    private Prompt(
        PromptId id,
        string name,
        PromptOwner owner,
        PromptMetadata metadata,
        string initialContent,
        Guid authorId,
        string authorName) : base(id)
    {
        Name = name;
        Owner = owner;
        Metadata = metadata;
        Status = PromptStatus.Draft;
        UpdatedAt = DateTime.UtcNow;

        var initialVersion = PromptVersion.Create(
            id,
            initialContent,
            SemanticVersion.Initial(),
            authorId,
            authorName,
            "Initial version");

        _versions.Add(initialVersion);

        AddDomainEvent(new PromptCreatedEvent(id, name, metadata.Category, authorId));
    }

    public static Prompt Create(
        string name,
        string initialContent,
        PromptOwner owner,
        PromptMetadata metadata,
        Guid authorId,
        string authorName)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Prompt name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(initialContent))
            throw new ArgumentException("Initial prompt content cannot be empty.", nameof(initialContent));

        return new Prompt(PromptId.CreateUnique(), name, owner, metadata, initialContent, authorId, authorName);
    }

    #region Versioning

    /// <summary>
    /// Creates a new version of the prompt. Prompts are immutable; this is the only way to change content.
    /// </summary>
    public PromptVersion CreateNewVersion(
        string content,
        Guid authorId,
        string authorName,
        string changeReason,
        IEnumerable<PromptVariable>? variables = null)
    {
        var latestVersion = GetLatestVersion();
        var newSemanticVersion = latestVersion.Version.IncrementMinor();

        var newVersion = PromptVersion.Create(Id, content, newSemanticVersion, authorId, authorName, changeReason, variables);
        _versions.Add(newVersion);
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PromptVersionCreatedEvent(Id, newVersion.Id, newSemanticVersion.ToString(), authorId));

        return newVersion;
    }

    /// <summary>
    /// Submits the latest draft version for approval review.
    /// </summary>
    public void SubmitForApproval()
    {
        var latestDraft = _versions.LastOrDefault(v => v.Status == PromptStatus.Draft)
            ?? throw new InvalidOperationException("No draft version found to submit for approval.");

        latestDraft.SubmitForReview();
        Status = PromptStatus.InReview;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Approves the version currently under review.
    /// </summary>
    public void Approve(Guid reviewerId, string reviewerName, string? reason = null)
    {
        var versionUnderReview = _versions.LastOrDefault(v => v.Status == PromptStatus.InReview)
            ?? throw new InvalidOperationException("No version is currently under review.");

        var approval = PromptApproval.Approve(reviewerId, reviewerName, reason);
        versionUnderReview.Approve(approval);
        Status = PromptStatus.Approved;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PromptApprovedEvent(Id, versionUnderReview.Id, reviewerId));
    }

    /// <summary>
    /// Rejects the version currently under review, returning it to Draft status.
    /// </summary>
    public void Reject(Guid reviewerId, string reviewerName, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A rejection reason is required.", nameof(reason));

        var versionUnderReview = _versions.LastOrDefault(v => v.Status == PromptStatus.InReview)
            ?? throw new InvalidOperationException("No version is currently under review.");

        var rejection = PromptApproval.Reject(reviewerId, reviewerName, reason);
        versionUnderReview.Reject(rejection);
        Status = PromptStatus.Draft;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PromptRejectedEvent(Id, versionUnderReview.Id, reviewerId, reason));
    }

    /// <summary>
    /// Activates the most recently approved version, making it the live version.
    /// </summary>
    public void Activate()
    {
        var approvedVersion = _versions.LastOrDefault(v => v.Status == PromptStatus.Approved)
            ?? throw new InvalidOperationException("No approved version found to activate.");

        // Deprecate any currently active version first
        var currentlyActive = _versions.FirstOrDefault(v => v.Status == PromptStatus.Active);
        currentlyActive?.Deprecate();

        approvedVersion.Activate();
        ActiveVersionId = approvedVersion.Id;
        Status = PromptStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Rolls back to a specific previous version by creating a new version with that version's content.
    /// </summary>
    public PromptVersion Rollback(PromptVersionId targetVersionId, Guid authorId, string authorName)
    {
        var targetVersion = _versions.FirstOrDefault(v => v.Id == targetVersionId)
            ?? throw new InvalidOperationException($"Version '{targetVersionId}' not found.");

        var rollbackVersion = CreateNewVersion(
            targetVersion.Content,
            authorId,
            authorName,
            $"Rollback to version {targetVersion.Version}",
            targetVersion.Variables);

        return rollbackVersion;
    }

    #endregion

    #region Lifecycle

    public void Deprecate()
    {
        if (Status != PromptStatus.Active)
            throw new InvalidOperationException("Only an Active prompt can be deprecated.");

        var activeVersion = _versions.FirstOrDefault(v => v.Status == PromptStatus.Active);
        activeVersion?.Deprecate();

        Status = PromptStatus.Deprecated;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PromptDeprecatedEvent(Id));
    }

    public void Archive()
    {
        if (Status == PromptStatus.Active)
            throw new InvalidOperationException("Cannot archive an Active prompt. Deprecate it first.");

        foreach (var version in _versions.Where(v => v.Status != PromptStatus.Archived))
        {
            version.Archive();
        }

        Status = PromptStatus.Archived;
        ActiveVersionId = null;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PromptArchivedEvent(Id));
    }

    public void Restore()
    {
        if (Status != PromptStatus.Archived)
            throw new InvalidOperationException("Only an Archived prompt can be restored.");

        Status = PromptStatus.Draft;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PromptRestoredEvent(Id));
    }

    #endregion

    #region Composition

    public void AddSection(PromptSectionType sectionType, string content, int order)
    {
        var section = PromptSection.Create(sectionType, content, order);
        _sections.Add(section);
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveSection(Guid sectionId)
    {
        var section = _sections.FirstOrDefault(s => s.Id == sectionId)
            ?? throw new InvalidOperationException($"Section '{sectionId}' not found.");
        _sections.Remove(section);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Renders the composed prompt by assembling all enabled sections in order
    /// and injecting the provided variables. Provider-agnostic.
    /// </summary>
    public string RenderComposed(IReadOnlyDictionary<string, string> variables)
    {
        var enabledSections = _sections
            .Where(s => s.IsEnabled)
            .OrderBy(s => s.Order)
            .ToList();

        if (!enabledSections.Any())
            throw new InvalidOperationException("No enabled sections found for composition. Add at least one section.");

        var composed = string.Join("\n\n", enabledSections.Select(s => s.Content));
        return InjectVariables(composed, variables);
    }

    #endregion

    #region Governance

    public void AddPolicy(PromptPolicy policy)
    {
        _policies.Add(policy);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignOwner(PromptOwner newOwner)
    {
        Owner = newOwner;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateMetadata(PromptMetadata metadata)
    {
        Metadata = metadata;
        UpdatedAt = DateTime.UtcNow;
    }

    #endregion

    #region Rendering

    /// <summary>
    /// Renders the active version of the prompt with the provided variables.
    /// Rendering is provider-agnostic; the output is a plain string.
    /// </summary>
    public string Render(IReadOnlyDictionary<string, string> variables)
    {
        if (Status == PromptStatus.Archived)
            throw new InvalidOperationException("Cannot render an archived prompt.");

        var activeVersion = GetActiveVersion()
            ?? throw new InvalidOperationException("No active version found. Activate a version before rendering.");

        var rendered = InjectVariables(activeVersion.Content, variables);

        AddDomainEvent(new PromptRenderedEvent(Id, activeVersion.Id, DateTime.UtcNow));

        return rendered;
    }

    private static string InjectVariables(string content, IReadOnlyDictionary<string, string> variables)
    {
        foreach (var variable in variables)
        {
            content = content.Replace($"{{{{{variable.Key}}}}}", variable.Value);
        }
        return content;
    }

    #endregion

    #region Variants

    public void AddVariant(string name, PromptVersionId versionId, int trafficWeight, string? description = null)
    {
        var totalWeight = _variants.Sum(v => v.TrafficWeight) + trafficWeight;
        if (totalWeight > 100)
            throw new InvalidOperationException($"Total variant traffic weight cannot exceed 100. Current total would be {totalWeight}.");

        var variant = PromptVariant.Create(name, versionId, trafficWeight, description);
        _variants.Add(variant);
        UpdatedAt = DateTime.UtcNow;
    }

    #endregion

    #region Helpers

    public PromptVersion GetLatestVersion()
    {
        return _versions.OrderByDescending(v => v.Id.Value).FirstOrDefault()
            ?? throw new InvalidOperationException("Prompt has no versions.");
    }

    public PromptVersion? GetActiveVersion()
    {
        return _versions.FirstOrDefault(v => v.Status == PromptStatus.Active);
    }

    public PromptVersion? GetVersion(PromptVersionId versionId)
    {
        return _versions.FirstOrDefault(v => v.Id == versionId);
    }

    public IEnumerable<PromptVersion> GetVersionHistory()
    {
        return _versions.OrderByDescending(v => v.CreatedAt);  // CreatedAt from BaseEntity
    }

    #endregion
}
