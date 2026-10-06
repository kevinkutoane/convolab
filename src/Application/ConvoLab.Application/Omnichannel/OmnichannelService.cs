using System.Collections.Concurrent;
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
        var cacheKey = $"omnichannel_processed_msg_{envelope.MessageId}";
        if (_memoryCache.TryGetValue(cacheKey, out _))
        {
            return new ProcessInboundMessageResult(
                Handled: true,
                ReplyText: string.Empty, // Idempotent silent drop
                EscalatedToHuman: false,
                EscalationReason: null,
                SessionId: $"{envelope.Channel}:{envelope.SenderId}");
        }

        _memoryCache.Set(cacheKey, true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
            Size = 1
        });

        var sessionKey = $"{envelope.Channel}:{envelope.SenderId}";

        // 1. Evaluate for immediate human handoff / escalation
        var handoff = _handoffEvaluator.Evaluate(envelope.Text);
        if (handoff.ShouldEscalate)
        {
            var escalationNotice = $"[ESCALATION] A transfer to a human specialist ({handoff.TargetDepartment ?? "Support"}) has been initiated. Reason: {handoff.Reason}";
            return new ProcessInboundMessageResult(
                Handled: true,
                ReplyText: "I understand this requires personal attention. I have routed your conversation to a live consultant who will join shortly.",
                EscalatedToHuman: true,
                EscalationReason: handoff.Reason,
                SessionId: sessionKey,
                QuickReplies: ["Check Agent Status", "Cancel Transfer"]);
        }

        // 2. Resolve or initialize persistent simulation session
        var title = $"WhatsApp: {envelope.SenderId}";
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

        // 3. Send message through ConvoLab simulation pipeline
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

        return new ProcessInboundMessageResult(
            Handled: true,
            ReplyText: lastMessage,
            EscalatedToHuman: false,
            EscalationReason: null,
            SessionId: simulationId.ToString());
    }

    public async Task<bool> DispatchOutboundAsync(
        OutboundMessageEnvelope message,
        CancellationToken cancellationToken = default)
    {
        var result = await _outboundAdapter.SendWhatsAppTextMessageAsync(message, cancellationToken);
        return result.Success;
    }
}
