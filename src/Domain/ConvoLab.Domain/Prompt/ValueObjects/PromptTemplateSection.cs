using ConvoLab.Domain.Common;
using ConvoLab.Domain.Prompt.Enums;

namespace ConvoLab.Domain.Prompt.ValueObjects;

public sealed class PromptTemplateSection : ValueObject
{
    private PromptTemplateSection(
        Guid id,
        PromptSectionType type,
        string name,
        string content,
        int sequence,
        bool required)
    {
        if (id == Guid.Empty) throw new ArgumentException("Section id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Section name is required.", nameof(name));
        if (content is null) throw new ArgumentNullException(nameof(content));
        if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));

        Id = id;
        Type = type;
        Name = name.Trim();
        Content = content;
        Sequence = sequence;
        Required = required;
    }

    public Guid Id { get; }
    public PromptSectionType Type { get; }
    public string Name { get; }
    public string Content { get; }
    public int Sequence { get; }
    public bool Required { get; }

    public static PromptTemplateSection Create(
        PromptSectionType type,
        string name,
        string content,
        int sequence,
        bool required = true,
        Guid? id = null)
        => new(id ?? Guid.NewGuid(), type, name, content, sequence, required);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Id;
        yield return Type;
        yield return Name;
        yield return Content;
        yield return Sequence;
        yield return Required;
    }
}
