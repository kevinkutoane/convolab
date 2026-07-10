using ConvoLab.Domain.Common;
using ConvoLab.Domain.Prompt.ValueObjects;
using ConvoLab.Domain.Prompt.Entities;
using ConvoLab.Domain.Prompt.Enums;
using ConvoLab.Domain.Prompt.Events;
namespace ConvoLab.Domain.Prompt.Aggregates;
public class PromptTemplate : BaseAggregateRoot<PromptTemplateId> {
    public string Name { get; private set; }
    public string TemplateString { get; private set; }
    public PromptType Type { get; private set; }
    public string Version { get; private set; }
    public bool IsActive { get; private set; }
    private readonly List<PromptParameter> _parameters = new();
    public IReadOnlyCollection<PromptParameter> Parameters => _parameters.AsReadOnly();
    private PromptTemplate() : base() { }
    private PromptTemplate(PromptTemplateId id, string name, string templateString, PromptType type, string version) : base(id) {
        Name = name; TemplateString = templateString; Type = type; Version = version; IsActive = true;
        AddDomainEvent(new PromptTemplateCreatedEvent(id, name, type));
    }
    public static PromptTemplate Create(string name, string templateString, PromptType type, string version) => new PromptTemplate(PromptTemplateId.CreateUnique(), name, templateString, type, version);
}
