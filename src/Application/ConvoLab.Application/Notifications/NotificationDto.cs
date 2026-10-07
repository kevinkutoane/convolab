namespace ConvoLab.Application.Notifications;

public sealed record NotificationDto(
    Guid Id,
    Guid? WorkspaceId,
    string Title,
    string Message,
    string Severity,
    string Category,
    string ActionUrl,
    DateTimeOffset CreatedAt,
    bool IsRead,
    bool IsDismissed,
    IReadOnlyDictionary<string, string>? Metadata = null);
