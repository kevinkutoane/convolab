using ConvoLab.Domain.Common;
namespace ConvoLab.Domain.Prompt.Entities;
public class PromptParameter : BaseEntity<Guid> {
    public string Name { get; private set; } = null!;
    public string Type { get; private set; } = null!;
    public bool IsRequired { get; private set; }
    public string? DefaultValue { get; private set; }
    private PromptParameter() { }
    private PromptParameter(Guid id, string name, string type, bool isRequired, string? defaultValue) : base(id) {
        Name = name; Type = type; IsRequired = isRequired; DefaultValue = defaultValue;
    }
    public static PromptParameter Create(string name, string type, bool isRequired, string? defaultValue = null) => new PromptParameter(Guid.NewGuid(), name, type, isRequired, defaultValue);
}
