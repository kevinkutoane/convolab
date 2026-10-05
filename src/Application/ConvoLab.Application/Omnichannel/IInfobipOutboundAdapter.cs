using ConvoLab.Domain.Omnichannel;

namespace ConvoLab.Application.Omnichannel;

public sealed record InfobipOutboundResult(
    bool Success,
    string? MessageId,
    int? StatusCode,
    string? ErrorMessage);

public interface IInfobipOutboundAdapter
{
    Task<InfobipOutboundResult> SendWhatsAppTextMessageAsync(
        OutboundMessageEnvelope envelope,
        CancellationToken cancellationToken = default);
}
