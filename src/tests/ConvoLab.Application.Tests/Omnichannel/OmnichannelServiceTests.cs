using ConvoLab.Application.Omnichannel;
using ConvoLab.Application.Simulation;
using ConvoLab.Domain.Omnichannel;

namespace ConvoLab.Application.Tests.Omnichannel;

public sealed class OmnichannelServiceTests
{
    [Fact]
    public async Task ProcessInbound_Uses_Workspace_Scope_To_Isolate_Simulation_Sessions()
    {
        var simulation = new RecordingSimulationService();
        var service = new OmnichannelService(simulation);
        var sender = "2782" + Guid.NewGuid().ToString("N")[..8];
        var workspaceA = Guid.NewGuid().ToString("N");
        var workspaceB = Guid.NewGuid().ToString("N");

        var first = await service.ProcessInboundAsync(CreateEnvelope(workspaceA, sender, "First"));
        var second = await service.ProcessInboundAsync(CreateEnvelope(workspaceB, sender, "Second"));
        var third = await service.ProcessInboundAsync(CreateEnvelope(workspaceA, sender, "Third"));

        Assert.NotEqual(first.SessionId, second.SessionId);
        Assert.Equal(first.SessionId, third.SessionId);
        Assert.Equal(2, simulation.CreatedIds.Count);
    }

    private static InboundMessageEnvelope CreateEnvelope(string workspaceId, string sender, string text)
        => new(Guid.NewGuid().ToString("N"), "WhatsApp", sender, "27829999999", text,
            OmnichannelMessageType.Text,
            new Dictionary<string, string> { ["provider"] = "Infobip", ["workspaceId"] = workspaceId },
            DateTimeOffset.UtcNow);

    private sealed class RecordingSimulationService : IConversationSimulationService
    {
        public List<Guid> CreatedIds { get; } = [];

        public Task<SimulationOptions> GetOptionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SimulationSummary>> ListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SimulationConversation?> GetAsync(Guid simulationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SimulationConversation> CreateAsync(CreateSimulationCommand command, CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid();
            CreatedIds.Add(id);
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new SimulationConversation(id, command.Title, "Draft", command.Workflow, command.PromptVersion, command.KnowledgeCollection, [], [], now, now));
        }

        public Task<SimulationConversation?> SendMessageAsync(Guid simulationId, SendSimulationMessageCommand command, CancellationToken cancellationToken = default) => Task.FromResult<SimulationConversation?>(null);
        public Task<SimulationConversation?> ReplayAsync(Guid simulationId, ReplaySimulationCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
