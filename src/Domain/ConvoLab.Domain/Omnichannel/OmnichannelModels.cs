namespace ConvoLab.Domain.Omnichannel;

public enum OmnichannelProviderKind
{
    Infobip,
    WhatsAppCloud,
    GenesysCloud,
    Mock
}

public enum OmnichannelMessageType
{
    Text,
    InteractiveButton,
    QuickReply,
    Document,
    Media
}

public enum HandoffStatus
{
    NotRequested,
    EscalatedToHuman,
    AgentJoined,
    Resolved
}

public sealed record InboundMessageEnvelope(
    string MessageId,
    string Channel,
    string SenderId,
    string ReceiverId,
    string Text,
    OmnichannelMessageType MessageType,
    IReadOnlyDictionary<string, string> ChannelMetadata,
    DateTimeOffset ReceivedAt);

public sealed record OutboundMessageEnvelope(
    string Channel,
    string RecipientId,
    string Text,
    OmnichannelMessageType MessageType,
    IReadOnlyList<string>? InteractiveOptions = null,
    string? AttachmentUrl = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record HandoffEvaluationResult(
    bool ShouldEscalate,
    string Reason,
    string? TargetDepartment = null,
    double DetectedSentimentScore = 1.0);
