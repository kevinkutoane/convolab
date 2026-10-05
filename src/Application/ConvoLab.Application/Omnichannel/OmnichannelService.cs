using System.Collections.Concurrent;
using ConvoLab.Application.Simulation;
using ConvoLab.Domain.Omnichannel;

namespace ConvoLab.Application.Omnichannel;

public sealed class OmnichannelService : IOmnichannelService
{
    private readonly IConversationSimulationService _simulationService;
    private readonly IHumanHandoffEvaluator _handoffEvaluator;

    // Maps sender phone/channel ID to ConvoLab simulation conversation ID
    private static readonly ConcurrentDictionary<string, Guid> ActiveSessions = new();

    public OmnichannelService(
        IConversationSimulationService simulationService,
        IHumanHandoffEvaluator? handoffEvaluator = null)
    {
        _simulationService = simulationService;
        _handoffEvaluator = handoffEvaluator ?? new KeywordHumanHandoffEvaluator();
    }

    public async Task<ProcessInboundMessageResult> ProcessInboundAsync(
        InboundMessageEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
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
        Guid simulationId;
        if (!ActiveSessions.TryGetValue(sessionKey, out simulationId))
        {
            var created = await _simulationService.CreateAsync(new CreateSimulationCommand(
                Title: $"WhatsApp: {envelope.SenderId}",
                Workflow: "Demo Claims Intake v1.0",
                PromptVersion: "Demo Claims Assistant v1.0",
                KnowledgeCollection: "Demo Claims Knowledge"), cancellationToken);

            simulationId = created.Id;
            ActiveSessions[sessionKey] = simulationId;
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

    public Task<bool> DispatchOutboundAsync(
        OutboundMessageEnvelope message,
        CancellationToken cancellationToken = default)
    {
        // Outbound connector interface (dispatches via Infobip WhatsApp API or provider client)
        return Task.FromResult(true);
    }
}
