using ConvoLab.Application.Simulation;
using ConvoLab.Domain.Omnichannel;
using Microsoft.Extensions.Caching.Memory;

namespace ConvoLab.Application.Omnichannel;

public sealed class OmnichannelService : IOmnichannelService
{
    private readonly IConversationSimulationService _simulationService;
    private readonly IHumanHandoffEvaluator _handoffEvaluator;
    private readonly IInfobipOutboundAdapter _outboundAdapter;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _memoryCache;

    public OmnichannelService(
        IConversationSimulationService simulationService,
        IInfobipOutboundAdapter outboundAdapter,
        Microsoft.Extensions.Caching.Memory.IMemoryCache memoryCache,
        IHumanHandoffEvaluator? handoffEvaluator = null)
    {
        _simulationService = simulationService;
        _outboundAdapter = outboundAdapter;
        _memoryCache = memoryCache;
        _handoffEvaluator = handoffEvaluator ?? new KeywordHumanHandoffEvaluator();
    }

    public async Task<ProcessInboundMessageResult> ProcessInboundAsync(
        InboundMessageEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        var workspaceScope = ResolveWorkspaceScope(envelope);
        var cacheKey = $"omnichannel_processed_msg_{workspaceScope}_{envelope.MessageId}";
        var sessionKey = $"{workspaceScope}:{envelope.Channel}:{envelope.SenderId}";

        if (_memoryCache.TryGetValue(cacheKey, out _))
        {
            return new ProcessInboundMessageResult(
                Handled: true,
                ReplyText: string.Empty,
                EscalatedToHuman: false,
                EscalationReason: null,
                SessionId: sessionKey);
        }

        // Do not cache the message before downstream work succeeds: thrown failures must be retryable.
        var handoff = _handoffEvaluator.Evaluate(envelope.Text);
        if (handoff.ShouldEscalate)
        {
            var handoffResult = new ProcessInboundMessageResult(
                Handled: true,
                ReplyText: "I understand this requires personal attention. I have routed your conversation to a live consultant who will join shortly.",
                EscalatedToHuman: true,
                EscalationReason: handoff.Reason,
                SessionId: sessionKey,
                QuickReplies: ["Check Agent Status", "Cancel Transfer"]);

            CacheProcessedMessage(cacheKey);
            return handoffResult;
        }

        // Workspace is part of the persistent title so one tenant cannot reuse another tenant's session.
        var title = $"WhatsApp: {envelope.SenderId} [{workspaceScope}]";
        var existingSimulations = await _simulationService.ListAsync(cancellationToken);
        var existing = existingSimulations.FirstOrDefault(s => s.Title == title);

        Guid simulationId;
        if (existing == null)
        {
            var created = await _simulationService.CreateAsync(new CreateSimulationCommand(
                Title: title,
                Workflow: "Demo Claims Intake v1.0",
                PromptVersion: "Demo Claims Assistant v1.0",
                KnowledgeCollection: "Demo Claims Knowledge"), cancellationToken);

            simulationId = created.Id;
        }
        else
        {
            simulationId = existing.Id;
        }

        var conversation = await _simulationService.SendMessageAsync(
            simulationId,
            new SendSimulationMessageCommand(
                Content: envelope.Text,
                Mode: SimulationMode.Normal,
                Provider: "Deterministic",
                Model: "convolab-deterministic-primary"),
            cancellationToken);

        var lastMessage = conversation?.Messages.LastOrDefault(m => m.Role == "assistant")?.Content
            ?? "Thank you for your message. An agent is reviewing your query.";

        var result = new ProcessInboundMessageResult(
            Handled: true,
            ReplyText: lastMessage,
            EscalatedToHuman: false,
            EscalationReason: null,
            SessionId: simulationId.ToString());

        CacheProcessedMessage(cacheKey);
        return result;
    }

    private static string ResolveWorkspaceScope(InboundMessageEnvelope envelope)
    {
        if (envelope.ChannelMetadata.TryGetValue("workspaceId", out var workspaceId)
            && !string.IsNullOrWhiteSpace(workspaceId))
        {
            return Guid.TryParse(workspaceId, out var parsedWorkspaceId)
                ? parsedWorkspaceId.ToString("N")
                : workspaceId.Trim().ToLowerInvariant();
        }

        // Non-workspace-aware callers are at least isolated by recipient/channel destination.
        return envelope.ReceiverId.Trim().ToLowerInvariant();
    }

    private void CacheProcessedMessage(string cacheKey)
    {
        _memoryCache.Set(cacheKey, true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
            Size = 1
        });
    }

    public async Task<bool> DispatchOutboundAsync(
        OutboundMessageEnvelope message,
        CancellationToken cancellationToken = default)
    {
        var result = await _outboundAdapter.SendWhatsAppTextMessageAsync(message, cancellationToken);
        return result.Success;
    }
}
