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

    Task<InfobipOutboundResult> SendWhatsAppButtonsMessageAsync(
        OutboundMessageEnvelope envelope,
        CancellationToken cancellationToken = default);

    Task<InfobipOutboundResult> SendWhatsAppListMessageAsync(
        OutboundMessageEnvelope envelope,
        CancellationToken cancellationToken = default);

    Task<InfobipOutboundResult> SendWhatsAppDocumentMessageAsync(
        OutboundMessageEnvelope envelope,
        CancellationToken cancellationToken = default);
}
