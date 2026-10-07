using ConvoLab.Domain.Notifications;

namespace ConvoLab.Application.Notifications;

public sealed record CreateNotificationRequest(
    string Title,
    string Message,
    NotificationSeverity Severity,
    NotificationCategory Category,
    string ActionUrl = "/",
    Guid? WorkspaceId = null,
    IReadOnlyDictionary<string, string>? Metadata = null);
