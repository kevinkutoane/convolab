using System.Text.Json;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.PromptStudio;
using ConvoLab.Domain.Prompt.Enums;
using ConvoLab.Domain.Prompt.ValueObjects;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.PromptStudio;

public sealed class EfPromptStudioRepository(ApplicationDbContext db) : IPromptStudioRepository
{
    private sealed record StoredPromptSection(
        Guid Id,
        PromptSectionType Type,
        string Name,
        string Content,
        int Sequence,
        bool Required);

    public async Task<IReadOnlyList<PromptDefinitionState>> ListPromptsAsync(CancellationToken ct = default)
        => (await db.Prompts.AsNoTracking().ToListAsync(ct))
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => MapPrompt(item)!)
            .ToList();

    public async Task<PromptDefinitionState?> GetPromptAsync(Guid id, CancellationToken ct = default)
        => MapPrompt(await db.Prompts.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct));

    public async Task<IReadOnlyList<PromptVersionState>> ListVersionsAsync(Guid promptId, CancellationToken ct = default)
        => (await db.PromptVersions.AsNoTracking()
                .Where(item => item.PromptId == promptId && db.Prompts.Any(prompt => prompt.Id == item.PromptId))
                .ToListAsync(ct))
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => MapVersion(item)!)
            .ToList();

    public async Task<PromptVersionState?> GetVersionAsync(Guid id, CancellationToken ct = default)
        => MapVersion(await db.PromptVersions.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && db.Prompts.Any(prompt => prompt.Id == item.PromptId), ct));

    public Task<bool> VersionExistsAsync(Guid promptId, string semanticVersion, CancellationToken ct = default)
        => db.PromptVersions.AsNoTracking()
            .AnyAsync(item => item.PromptId == promptId && item.Version == semanticVersion && db.Prompts.Any(prompt => prompt.Id == item.PromptId), ct);

    public async Task<IReadOnlyList<PromptVersionState>> ListPublishedVersionsAsync(CancellationToken ct = default)
        => (await db.PromptVersions.AsNoTracking()
                .Where(item => item.Status == PromptStatus.Active.ToString() && db.Prompts.Any(prompt => prompt.Id == item.PromptId))
                .OrderBy(item => item.PromptId)
                .ThenBy(item => item.Version)
                .ToListAsync(ct))
            .Select(item => MapVersion(item)!)
            .ToList();

    public Task AddPromptAsync(PromptDefinitionState prompt, CancellationToken ct = default)
    {
        db.Prompts.Add(new PromptRecord
        {
            Id = prompt.Id,
            Name = prompt.Name,
            Description = prompt.Description,
            Owner = prompt.Owner,
            Category = prompt.Category,
            TagsJson = JsonSerializer.Serialize(prompt.Tags),
            Status = ToStoredStatus(prompt.Status),
            CreatedAt = prompt.CreatedAt,
            UpdatedAt = prompt.UpdatedAt,
            Revision = prompt.Revision
        });
        return Task.CompletedTask;
    }

    public async Task UpdatePromptAsync(
        PromptDefinitionState prompt,
        long expectedRevision,
        CancellationToken ct = default)
    {
        var record = await db.Prompts.FirstOrDefaultAsync(item => item.Id == prompt.Id, ct)
            ?? throw new ResourceNotFoundException("prompt.not_found", $"Prompt '{prompt.Id}' was not found.");
        if (record.Revision != expectedRevision) throw new ConcurrencyConflictException("prompt", prompt.Id);

        record.Name = prompt.Name;
        record.Description = prompt.Description;
        record.Owner = prompt.Owner;
        record.Category = prompt.Category;
        record.TagsJson = JsonSerializer.Serialize(prompt.Tags);
        record.Status = ToStoredStatus(prompt.Status);
        record.UpdatedAt = prompt.UpdatedAt;
        record.Revision = prompt.Revision;
    }

    public Task AddVersionAsync(PromptVersionState version, CancellationToken ct = default)
    {
        db.PromptVersions.Add(new PromptVersionRecord
        {
            Id = version.Id,
            PromptId = version.PromptId,
            Version = version.Version,
            Status = ToStoredStatus(version.Status),
            ChangeSummary = version.ChangeSummary,
            SectionsJson = SerializeSections(version.Sections),
            VariablesJson = JsonSerializer.Serialize(version.Variables),
            EstimatedTokens = version.EstimatedTokens,
            CreatedAt = version.CreatedAt,
            UpdatedAt = version.UpdatedAt,
            PublishedAt = version.PublishedAt,
            Revision = version.Revision
        });
        return Task.CompletedTask;
    }

    public async Task UpdateVersionAsync(
        PromptVersionState version,
        long expectedRevision,
        CancellationToken ct = default)
    {
        var record = await db.PromptVersions.FirstOrDefaultAsync(item => item.Id == version.Id && db.Prompts.Any(prompt => prompt.Id == item.PromptId), ct)
            ?? throw new ResourceNotFoundException(
                "prompt.version.not_found",
                $"Prompt version '{version.Id}' was not found.");
        if (record.Revision != expectedRevision)
            throw new ConcurrencyConflictException("prompt version", version.Id);

        record.Status = ToStoredStatus(version.Status);
        record.ChangeSummary = version.ChangeSummary;
        record.SectionsJson = SerializeSections(version.Sections);
        record.VariablesJson = JsonSerializer.Serialize(version.Variables);
        record.EstimatedTokens = version.EstimatedTokens;
        record.UpdatedAt = version.UpdatedAt;
        record.PublishedAt = version.PublishedAt;
        record.Revision = version.Revision;
    }

    public Task AddLifecycleEntryAsync(PromptLifecycleState entry, CancellationToken ct = default)
    {
        db.PromptLifecycle.Add(new PromptLifecycleRecord
        {
            Id = entry.Id,
            PromptVersionId = entry.PromptVersionId,
            Actor = entry.Actor,
            Action = entry.Action,
            Reason = entry.Reason,
            PreviousStatus = ToStoredStatus(entry.PreviousStatus),
            NewStatus = ToStoredStatus(entry.NewStatus),
            CreatedAt = entry.CreatedAt
        });
        return Task.CompletedTask;
    }

    private static PromptDefinitionState? MapPrompt(PromptRecord? record)
        => record is null
            ? null
            : new PromptDefinitionState(
                record.Id,
                record.Name,
                record.Description,
                record.Owner,
                record.Category,
                DeserializeList<string>(record.TagsJson),
                FromStoredStatus(record.Status),
                record.CreatedAt,
                record.UpdatedAt,
                record.Revision);

    private static PromptVersionState? MapVersion(PromptVersionRecord? record)
        => record is null
            ? null
            : new PromptVersionState(
                record.Id,
                record.PromptId,
                record.Version,
                FromStoredStatus(record.Status),
                record.ChangeSummary,
                DeserializeSections(record.SectionsJson),
                DeserializeList<string>(record.VariablesJson),
                record.EstimatedTokens,
                record.CreatedAt,
                record.UpdatedAt,
                record.PublishedAt,
                record.Revision);

    private static string ToStoredStatus(PromptStatus status)
        => status switch
        {
            PromptStatus.InReview => "PendingApproval",
            PromptStatus.Active => "Published",
            _ => status.ToString()
        };

    private static PromptStatus FromStoredStatus(string status)
        => status switch
        {
            "PendingApproval" => PromptStatus.InReview,
            "Published" => PromptStatus.Active,
            _ => Enum.Parse<PromptStatus>(status)
        };

    private static string SerializeSections(IEnumerable<PromptTemplateSection> sections)
        => JsonSerializer.Serialize(sections.Select(section => new StoredPromptSection(
            section.Id,
            section.Type,
            section.Name,
            section.Content,
            section.Sequence,
            section.Required)));

    private static IReadOnlyList<PromptTemplateSection> DeserializeSections(string json)
        => (JsonSerializer.Deserialize<List<StoredPromptSection>>(json) ?? [])
            .Select(section => PromptTemplateSection.Create(
                section.Type,
                section.Name,
                section.Content,
                section.Sequence,
                section.Required,
                section.Id))
            .ToList();

    private static IReadOnlyList<T> DeserializeList<T>(string json)
        => JsonSerializer.Deserialize<List<T>>(json) ?? [];
}
