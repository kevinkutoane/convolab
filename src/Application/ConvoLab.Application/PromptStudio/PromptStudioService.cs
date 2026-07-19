using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Common.Persistence;
using ConvoLab.Domain.Prompt.Enums;
using ConvoLab.Domain.Prompt.Policies;
using ConvoLab.Domain.Prompt.Services;
using ConvoLab.Domain.Prompt.ValueObjects;

namespace ConvoLab.Application.PromptStudio;

public sealed class PromptStudioService(
    IPromptStudioRepository repository,
    IUnitOfWork unitOfWork) : IPromptStudioService
{
    public async Task<IReadOnlyList<PromptSummaryDto>> ListAsync(CancellationToken ct = default)
    {
        var prompts = await repository.ListPromptsAsync(ct);
        var result = new List<PromptSummaryDto>(prompts.Count);
        foreach (var prompt in prompts)
        {
            var versions = await repository.ListVersionsAsync(prompt.Id, ct);
            result.Add(MapSummary(prompt, versions));
        }
        return result.OrderByDescending(item => item.UpdatedAt).ToList();
    }

    public async Task<PromptDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var prompt = await repository.GetPromptAsync(id, ct);
        if (prompt is null) return null;
        var versions = await repository.ListVersionsAsync(id, ct);
        return MapDetail(prompt, versions);
    }

    public async Task<PromptDetailDto> CreateAsync(CreatePromptCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            throw new RequestValidationException(
                "prompt.name.required",
                "Prompt name is required.",
                new Dictionary<string, string[]> { ["name"] = ["Prompt name is required."] });

        var now = DateTimeOffset.UtcNow;
        var prompt = new PromptDefinitionState(
            Guid.NewGuid(),
            command.Name.Trim(),
            command.Description?.Trim() ?? string.Empty,
            string.IsNullOrWhiteSpace(command.Owner) ? "Unassigned" : command.Owner.Trim(),
            string.IsNullOrWhiteSpace(command.Category) ? "General" : command.Category.Trim(),
            NormalizeTags(command.Tags),
            PromptStatus.Draft,
            now,
            now,
            1);

        await repository.AddPromptAsync(prompt, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return MapDetail(prompt, []);
    }

    public async Task<PromptDetailDto?> UpdateAsync(Guid id, UpdatePromptCommand command, CancellationToken ct = default)
    {
        var current = await repository.GetPromptAsync(id, ct);
        if (current is null) return null;

        var expectedRevision = command.ExpectedRevision ?? current.Revision;
        var updated = current with
        {
            Name = string.IsNullOrWhiteSpace(command.Name) ? current.Name : command.Name.Trim(),
            Description = command.Description is null ? current.Description : command.Description.Trim(),
            Owner = string.IsNullOrWhiteSpace(command.Owner) ? current.Owner : command.Owner.Trim(),
            Category = string.IsNullOrWhiteSpace(command.Category) ? current.Category : command.Category.Trim(),
            Tags = command.Tags is null ? current.Tags : NormalizeTags(command.Tags),
            UpdatedAt = DateTimeOffset.UtcNow,
            Revision = current.Revision + 1
        };

        await repository.UpdatePromptAsync(updated, expectedRevision, ct);
        await unitOfWork.SaveChangesAsync(ct);
        var versions = await repository.ListVersionsAsync(id, ct);
        return MapDetail(updated, versions);
    }

    public async Task<PromptVersionDto> CreateVersionAsync(
        Guid promptId,
        CreatePromptVersionCommand command,
        CancellationToken ct = default)
    {
        var prompt = await repository.GetPromptAsync(promptId, ct)
            ?? throw new ResourceNotFoundException("prompt.not_found", $"Prompt '{promptId}' was not found.");

        if (command.ExpectedPromptRevision.HasValue && command.ExpectedPromptRevision.Value != prompt.Revision)
            throw new ConcurrencyConflictException("prompt", promptId);

        SemanticVersion semanticVersion;
        try
        {
            semanticVersion = SemanticVersion.Parse(command.Version.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            throw new RequestValidationException(
                "prompt.version.invalid",
                "Version must use MAJOR.MINOR.PATCH semantic version format.",
                new Dictionary<string, string[]> { ["version"] = ["Use a semantic version such as 1.0.0."] });
        }

        if (await repository.VersionExistsAsync(promptId, semanticVersion.ToString(), ct))
            throw new ResourceConflictException(
                "prompt.version.duplicate",
                $"Prompt version '{semanticVersion}' already exists.");

        if (command.Sections.Count == 0)
            throw new RequestValidationException(
                "prompt.sections.required",
                "At least one prompt section is required.",
                new Dictionary<string, string[]> { ["sections"] = ["Add at least one section."] });

        var sections = command.Sections
            .OrderBy(section => section.Sequence)
            .Select(section => PromptTemplateSection.Create(
                MapSectionType(section.Kind),
                string.IsNullOrWhiteSpace(section.Name) ? section.Kind.ToString() : section.Name,
                section.Content ?? string.Empty,
                section.Sequence,
                section.Required))
            .ToList();

        if (sections.Select(section => section.Sequence).Distinct().Count() != sections.Count)
            throw new RequestValidationException(
                "prompt.sections.sequence_duplicate",
                "Prompt section sequence values must be unique.",
                new Dictionary<string, string[]> { ["sections"] = ["Each section must have a unique sequence."] });

        var variables = PromptTemplateEngine.DiscoverVariables(sections);
        var renderedTemplate = PromptTemplateEngine.Render(sections, new Dictionary<string, string>(), false);
        var now = DateTimeOffset.UtcNow;
        var version = new PromptVersionState(
            Guid.NewGuid(),
            promptId,
            semanticVersion.ToString(),
            PromptStatus.Draft,
            command.ChangeSummary?.Trim() ?? string.Empty,
            sections,
            variables,
            PromptTemplateEngine.EstimateTokens(renderedTemplate),
            now,
            now,
            null,
            1);

        await repository.AddVersionAsync(version, ct);
        var updatedPrompt = prompt with { UpdatedAt = now, Revision = prompt.Revision + 1 };
        await repository.UpdatePromptAsync(updatedPrompt, prompt.Revision, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return MapVersion(version);
    }

    public async Task<PromptVersionDto?> TransitionAsync(
        Guid versionId,
        string action,
        PromptLifecycleCommand command,
        CancellationToken ct = default)
    {
        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null) return null;
        var prompt = await repository.GetPromptAsync(version.PromptId, ct)
            ?? throw new ResourceNotFoundException("prompt.not_found", $"Prompt '{version.PromptId}' was not found.");

        var parsedAction = ParseAction(action);
        PromptStatus next;
        try
        {
            next = PromptReleasePolicy.Transition(version.Status, parsedAction);
        }
        catch (InvalidOperationException exception)
        {
            throw new DomainRuleViolationException("prompt.lifecycle.invalid_transition", exception.Message);
        }

        var expectedRevision = command.ExpectedRevision ?? version.Revision;
        var now = DateTimeOffset.UtcNow;

        var aggregateVersions = (await repository.ListVersionsAsync(version.PromptId, ct)).ToList();
        if (next == PromptStatus.Active)
        {
            foreach (var currentPublished in aggregateVersions.Where(item => item.Status == PromptStatus.Active && item.Id != version.Id).ToList())
            {
                var deprecated = currentPublished with
                {
                    Status = PromptStatus.Deprecated,
                    UpdatedAt = now,
                    Revision = currentPublished.Revision + 1
                };
                await repository.UpdateVersionAsync(deprecated, currentPublished.Revision, ct);
                aggregateVersions[aggregateVersions.FindIndex(item => item.Id == deprecated.Id)] = deprecated;
                await repository.AddLifecycleEntryAsync(new PromptLifecycleState(
                    Guid.NewGuid(),
                    deprecated.Id,
                    NormalizeActor(command.Actor),
                    "auto-deprecate",
                    "Superseded by a newly published version.",
                    PromptStatus.Active,
                    PromptStatus.Deprecated,
                    now), ct);
            }
        }

        var updated = version with
        {
            Status = next,
            UpdatedAt = now,
            PublishedAt = next == PromptStatus.Active ? now : version.PublishedAt,
            Revision = version.Revision + 1
        };
        await repository.UpdateVersionAsync(updated, expectedRevision, ct);
        await repository.AddLifecycleEntryAsync(new PromptLifecycleState(
            Guid.NewGuid(),
            version.Id,
            NormalizeActor(command.Actor),
            action.Trim().ToLowerInvariant(),
            command.Reason,
            version.Status,
            next,
            now), ct);

        var versionIndex = aggregateVersions.FindIndex(item => item.Id == updated.Id);
        if (versionIndex >= 0) aggregateVersions[versionIndex] = updated;
        else aggregateVersions.Add(updated);

        var promptStatus = DeterminePromptStatus(aggregateVersions);
        var updatedPrompt = prompt with
        {
            Status = promptStatus,
            UpdatedAt = now,
            Revision = prompt.Revision + 1
        };
        await repository.UpdatePromptAsync(updatedPrompt, prompt.Revision, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return MapVersion(updated);
    }

    public async Task<RenderedPromptDto> RenderAsync(RenderPromptCommand command, CancellationToken ct = default)
    {
        var version = await repository.GetVersionAsync(command.VersionId, ct)
            ?? throw new ResourceNotFoundException("prompt.version.not_found", "Prompt version was not found.");
        var prompt = await repository.GetPromptAsync(version.PromptId, ct)
            ?? throw new ResourceNotFoundException("prompt.not_found", "Prompt was not found.");

        if (version.Status == PromptStatus.Archived)
            throw new DomainRuleViolationException("prompt.version.archived", "Archived prompt versions cannot be rendered.");

        var variables = command.Variables ?? new Dictionary<string, string>();
        var missing = PromptTemplateEngine.FindMissingRequiredVariables(version.Sections, variables);
        var rendered = PromptTemplateEngine.Render(version.Sections, variables, false);
        return new RenderedPromptDto(
            prompt.Id,
            version.Id,
            prompt.Name,
            version.Version,
            rendered,
            missing,
            PromptTemplateEngine.EstimateTokens(rendered));
    }

    public async Task<PromptComparisonDto> CompareAsync(
        Guid leftVersionId,
        Guid rightVersionId,
        CancellationToken ct = default)
    {
        var left = await repository.GetVersionAsync(leftVersionId, ct)
            ?? throw new ResourceNotFoundException("prompt.version.left_not_found", "Left prompt version was not found.");
        var right = await repository.GetVersionAsync(rightVersionId, ct)
            ?? throw new ResourceNotFoundException("prompt.version.right_not_found", "Right prompt version was not found.");

        return new PromptComparisonDto(
            MapVersion(left),
            MapVersion(right),
            right.EstimatedTokens - left.EstimatedTokens,
            right.Variables.Except(left.Variables, StringComparer.OrdinalIgnoreCase).ToList(),
            left.Variables.Except(right.Variables, StringComparer.OrdinalIgnoreCase).ToList());
    }

    public async Task<IReadOnlyList<RuntimePromptTemplate>> ListPublishedAsync(CancellationToken ct = default)
    {
        var versions = await repository.ListPublishedVersionsAsync(ct);
        var result = new List<RuntimePromptTemplate>(versions.Count);
        foreach (var version in versions)
        {
            var prompt = await repository.GetPromptAsync(version.PromptId, ct);
            if (prompt is not null) result.Add(MapRuntime(prompt, version));
        }
        return result.OrderBy(item => item.Name).ThenBy(item => item.Version).ToList();
    }

    public async Task<RuntimePromptTemplate?> ResolvePublishedAsync(string displayName, CancellationToken ct = default)
        => (await ListPublishedAsync(ct))
            .FirstOrDefault(item => item.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));

    public string RenderRuntime(RuntimePromptTemplate template, IReadOnlyDictionary<string, string> variables)
    {
        var sections = template.Sections.Select(MapSection).ToList();
        try
        {
            return PromptTemplateEngine.Render(sections, variables, true);
        }
        catch (InvalidOperationException exception) when (exception.Message.StartsWith("Missing required", StringComparison.Ordinal))
        {
            throw new DomainRuleViolationException("prompt.variables.missing", exception.Message);
        }
    }

    private static PromptStatus DeterminePromptStatus(IEnumerable<PromptVersionState> versions)
    {
        var statuses = versions.Select(item => item.Status).ToHashSet();
        if (statuses.Contains(PromptStatus.Active)) return PromptStatus.Active;
        if (statuses.Contains(PromptStatus.Approved)) return PromptStatus.Approved;
        if (statuses.Contains(PromptStatus.InReview)) return PromptStatus.InReview;
        if (statuses.Contains(PromptStatus.Draft)) return PromptStatus.Draft;
        if (statuses.Contains(PromptStatus.Deprecated)) return PromptStatus.Deprecated;
        return PromptStatus.Archived;
    }

    private static PromptReleaseAction ParseAction(string action)
        => action.Trim().ToLowerInvariant() switch
        {
            "submit" => PromptReleaseAction.Submit,
            "approve" => PromptReleaseAction.Approve,
            "reject" => PromptReleaseAction.Reject,
            "publish" => PromptReleaseAction.Publish,
            "deprecate" => PromptReleaseAction.Deprecate,
            "archive" => PromptReleaseAction.Archive,
            "restore" => PromptReleaseAction.Restore,
            _ => throw new RequestValidationException(
                "prompt.lifecycle.action_invalid",
                $"Unknown prompt lifecycle action '{action}'.")
        };

    private static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? tags)
        => (tags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string NormalizeActor(string actor)
        => string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim();

    private static PromptSectionType MapSectionType(PromptSectionKind kind)
        => kind switch
        {
            PromptSectionKind.System => PromptSectionType.System,
            PromptSectionKind.Developer => PromptSectionType.Role,
            PromptSectionKind.Knowledge => PromptSectionType.Knowledge,
            PromptSectionKind.Conversation => PromptSectionType.ConversationMemory,
            PromptSectionKind.User => PromptSectionType.UserMessage,
            PromptSectionKind.Output => PromptSectionType.Custom,
            _ => PromptSectionType.Custom
        };

    private static PromptSectionKind MapSectionKind(PromptSectionType type)
        => type switch
        {
            PromptSectionType.System => PromptSectionKind.System,
            PromptSectionType.Role => PromptSectionKind.Developer,
            PromptSectionType.Knowledge => PromptSectionKind.Knowledge,
            PromptSectionType.ConversationMemory => PromptSectionKind.Conversation,
            PromptSectionType.UserMessage => PromptSectionKind.User,
            _ => PromptSectionKind.Output
        };

    private static PromptTemplateSection MapSection(PromptSectionDto section)
        => PromptTemplateSection.Create(
            MapSectionType(section.Kind),
            section.Name,
            section.Content,
            section.Sequence,
            section.Required,
            section.Id);

    private static PromptSummaryDto MapSummary(
        PromptDefinitionState prompt,
        IReadOnlyList<PromptVersionState> versions)
    {
        var latest = versions.OrderByDescending(version => SemanticVersion.Parse(version.Version).Major)
            .ThenByDescending(version => SemanticVersion.Parse(version.Version).Minor)
            .ThenByDescending(version => SemanticVersion.Parse(version.Version).Patch)
            .FirstOrDefault();
        return new PromptSummaryDto(
            prompt.Id,
            prompt.Name,
            prompt.Description,
            prompt.Owner,
            prompt.Category,
            prompt.Tags,
            MapStatus(prompt.Status),
            latest?.Version ?? "—",
            versions.Count,
            prompt.UpdatedAt,
            prompt.Revision);
    }

    private static PromptDetailDto MapDetail(
        PromptDefinitionState prompt,
        IReadOnlyList<PromptVersionState> versions)
        => new(
            prompt.Id,
            prompt.Name,
            prompt.Description,
            prompt.Owner,
            prompt.Category,
            prompt.Tags,
            MapStatus(prompt.Status),
            versions.OrderByDescending(version => version.CreatedAt).Select(MapVersion).ToList(),
            prompt.CreatedAt,
            prompt.UpdatedAt,
            prompt.Revision);

    private static PromptVersionDto MapVersion(PromptVersionState version)
        => new(
            version.Id,
            version.PromptId,
            version.Version,
            MapStatus(version.Status),
            version.ChangeSummary,
            version.Sections
                .OrderBy(section => section.Sequence)
                .Select(section => new PromptSectionDto(
                    section.Id,
                    MapSectionKind(section.Type),
                    section.Name,
                    section.Content,
                    section.Sequence,
                    section.Required))
                .ToList(),
            version.Variables,
            version.EstimatedTokens,
            version.CreatedAt,
            version.UpdatedAt,
            version.PublishedAt,
            version.Revision);

    private static RuntimePromptTemplate MapRuntime(
        PromptDefinitionState prompt,
        PromptVersionState version)
        => new(
            prompt.Id,
            version.Id,
            prompt.Name,
            version.Version,
            $"{prompt.Name} v{version.Version}",
            MapVersion(version).Sections);

    private static PromptStudioStatus MapStatus(PromptStatus status)
        => status switch
        {
            PromptStatus.InReview => PromptStudioStatus.PendingApproval,
            PromptStatus.Active => PromptStudioStatus.Published,
            _ => Enum.Parse<PromptStudioStatus>(status.ToString())
        };
}
