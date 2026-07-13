using ConvoLab.Domain.Common;
using ConvoLab.Domain.AI.ValueObjects;
using ConvoLab.Domain.AI.Enums;
namespace ConvoLab.Domain.AI.Entities;
public class AIModel : BaseEntity<AIModelId> {
    public string Name { get; private set; }
    public string Vendor { get; private set; }
    public AIModelType Type { get; private set; }
    public string Version { get; private set; }
    public bool IsActive { get; private set; }
    private AIModel() : base() { }
    private AIModel(AIModelId id, string name, string vendor, AIModelType type, string version, bool isActive) : base(id) {
        Name = name; Vendor = vendor; Type = type; Version = version; IsActive = isActive;
    }
    public static AIModel Create(string name, string vendor, AIModelType type, string version) => new AIModel(AIModelId.CreateUnique(), name, vendor, type, version, true);
}
