using ConvoLab.Application.Omnichannel;
using ConvoLab.Application.Simulation;
using ConvoLab.Domain.Omnichannel;
using Microsoft.Extensions.Caching.Memory;

namespace ConvoLab.Application.Tests.Omnichannel;

public sealed class OmnichannelServiceTests
{
    [Fact]
    public async Task Failed_processing_does_not_poison_the_message_cache()
    {
        using var cache = CreateCache();
        var simulations = new FakeConversationSimulationService { FailNextList = true };
        var service = new OmnichannelService(simulations, new FakeOutboundAdapter(), cache);
        var envelope = CreateEnvelope("retryable-message", "workspace-a");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessInboundAsync(envelope));

        var result = await service.ProcessInboundAsync(envelope);

        Assert.True(result.Handled);
        Assert.Equal(2, simulations.ListCalls);
        Assert.Equal(1, simulations.SendMessageCalls);
    }

    [Fact]
    public async Task Message_deduplication_and_sessions_are_scoped_to_workspace()
    {
        using var cache = CreateCache();
        var simulations = new FakeConversationSimulationService();
        var service = new OmnichannelService(simulations, new FakeOutboundAdapter(), cache);

        var first = await service.ProcessInboundAsync(CreateEnvelope("same-message-id", "workspace-a"));
        var secondWorkspace = await service.ProcessInboundAsync(CreateEnvelope("same-message-id", "workspace-b"));
        await service.ProcessInboundAsync(CreateEnvelope("same-message-id", "workspace-a"));

        Assert.True(first.Handled);
        Assert.True(secondWorkspace.Handled);
        Assert.Equal(2, simulations.CreatedTitles.Count);
        Assert.Contains(simulations.CreatedTitles, title => title.Contains("[workspace-a]", StringComparison.Ordinal));
        Assert.Contains(simulations.CreatedTitles, title => title.Contains("[workspace-b]", StringComparison.Ordinal));
        Assert.Equal(2, simulations.ListCalls);
        Assert.Equal(2, simulations.SendMessageCalls);
    }

    private static MemoryCache CreateCache() => new(new MemoryCacheOptions { SizeLimit = 128 });

    private static InboundMessageEnvelope CreateEnvelope(string messageId, string workspaceId)
        => new(
            MessageId: messageId,
            Channel: "WhatsApp",
            SenderId: "27821234567",
            ReceiverId: "27829999999",
            Text: "How do I report a claim?",
            MessageType: OmnichannelMessageType.Text,
            ChannelMetadata: new Dictionary<string, string>
            {
                ["provider"] = "Infobip",
                ["workspaceId"] = workspaceId
            },
            ReceivedAt: DateTimeOffset.UtcNow);

    private sealed class FakeOutboundAdapter : IInfobipOutboundAdapter
    {
        public Task<InfobipOutboundResult> SendWhatsAppTextMessageAsync(
            OutboundMessageEnvelope envelope,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new InfobipOutboundResult(true, "outbound-message", 200, null));
    }

    private sealed class FakeConversationSimulationService : IConversationSimulationService
    {
        private readonly Dictionary<Guid, SimulationConversation> _conversations = [];

        public bool FailNextList { get; set; }
        public int ListCalls { get; private set; }
        public int SendMessageCalls { get; private set; }
        public List<string> CreatedTitles { get; } = [];

        public Task<SimulationOptions> GetOptionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new SimulationOptions([], [], [], [], []));

        public Task<IReadOnlyList<SimulationSummary>> ListAsync(CancellationToken cancellationToken = default)
        {
            ListCalls++;
            if (FailNextList)
            {
                FailNextList = false;
                throw new InvalidOperationException("Simulated transient failure.");
            }

            IReadOnlyList<SimulationSummary> summaries = _conversations.Values
                .Select(conversation => new SimulationSummary(
                    conversation.Id,
                    conversation.Title,
                    conversation.Status,
                    conversation.Workflow,
                    conversation.PromptVersion,
                    conversation.KnowledgeCollection,
                    conversation.Messages.Count,
                    conversation.Runs.Count,
                    conversation.Messages.LastOrDefault()?.Content,
                    conversation.CreatedAt,
                    conversation.UpdatedAt))
                .ToArray();

            return Task.FromResult(summaries);
        }

        public Task<SimulationConversation?> GetAsync(Guid simulationId, CancellationToken cancellationToken = default)
            => Task.FromResult(_conversations.TryGetValue(simulationId, out var conversation) ? conversation : null);

        public Task<SimulationConversation> CreateAsync(CreateSimulationCommand command, CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.UtcNow;
            var conversation = new SimulationConversation(
                Guid.NewGuid(),
                command.Title,
                "Active",
                command.Workflow,
                command.PromptVersion,
                command.KnowledgeCollection,
                Array.Empty<SimulationMessage>(),
                Array.Empty<SimulationRun>(),
                now,
                now);

            _conversations.Add(conversation.Id, conversation);
            CreatedTitles.Add(command.Title);
            return Task.FromResult(conversation);
        }

        public Task<SimulationConversation?> SendMessageAsync(
            Guid simulationId,
            SendSimulationMessageCommand command,
            CancellationToken cancellationToken = default)
        {
            SendMessageCalls++;
            if (!_conversations.TryGetValue(simulationId, out var conversation))
                throw new InvalidOperationException("Simulation was not created.");

            var assistantMessage = new SimulationMessage(
                Guid.NewGuid(),
                "assistant",
                $"Reply to: {command.Content}",
                false,
                DateTimeOffset.UtcNow);

            var updated = conversation with
            {
                Messages = conversation.Messages.Append(assistantMessage).ToArray(),
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _conversations[simulationId] = updated;
            return Task.FromResult<SimulationConversation?>(updated);
        }

        public Task<SimulationConversation?> ReplayAsync(Guid simulationId, ReplaySimulationCommand command, CancellationToken cancellationToken = default)
            => Task.FromResult<SimulationConversation?>(null);
    }
}
