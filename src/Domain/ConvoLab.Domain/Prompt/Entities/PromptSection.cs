using ConvoLab.Domain.Common;
using ConvoLab.Domain.Prompt.Enums;

namespace ConvoLab.Domain.Prompt.Entities;

/// <summary>
/// Represents a composable section of a prompt (e.g., System, Role, Knowledge, Safety).
/// Sections are assembled in order to produce the final rendered prompt.
/// </summary>
public class PromptSection : BaseEntity<Guid>
{
    public PromptSectionType SectionType { get; private set; }
    public string Content { get; private set; }
    public int Order { get; private set; }
    public bool IsEnabled { get; private set; }

    private PromptSection() { Content = null!; }

    private PromptSection(Guid id, PromptSectionType sectionType, string content, int order) : base(id)
    {
        SectionType = sectionType;
        Content = content;
        Order = order;
        IsEnabled = true;
    }

    public static PromptSection Create(PromptSectionType sectionType, string content, int order)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Section content cannot be empty.", nameof(content));

        return new PromptSection(Guid.NewGuid(), sectionType, content, order);
    }

    public void Disable() => IsEnabled = false;
    public void Enable() => IsEnabled = true;
}
