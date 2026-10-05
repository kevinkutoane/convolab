using ConvoLab.Domain.Omnichannel;

namespace ConvoLab.Application.Omnichannel;

public sealed record ProcessInboundMessageResult(
    bool Handled,
    string? ReplyText,
    bool EscalatedToHuman,
    string? EscalationReason,
    string? SessionId,
    IReadOnlyList<string>? QuickReplies = null);

public interface IOmnichannelService
{
    Task<ProcessInboundMessageResult> ProcessInboundAsync(
        InboundMessageEnvelope envelope,
        CancellationToken cancellationToken = default);

    Task<bool> DispatchOutboundAsync(
        OutboundMessageEnvelope message,
        CancellationToken cancellationToken = default);
}
