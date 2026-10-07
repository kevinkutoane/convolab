using ConvoLab.Domain.Common;
using ConvoLab.Domain.Notifications.Events;

namespace ConvoLab.Domain.Notifications;

public class Notification : BaseAggregateRoot<Guid>
{
    public Guid? WorkspaceId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public NotificationSeverity Severity { get; private set; }
    public NotificationCategory Category { get; private set; }
    public string ActionUrl { get; private set; } = string.Empty;
    public bool IsRead { get; private set; }
    public bool IsDismissed { get; private set; }
    public IReadOnlyDictionary<string, string> Metadata { get; private set; } = new Dictionary<string, string>();

    private Notification() : base() { }

    private Notification(
        Guid id,
        Guid? workspaceId,
        string title,
        string message,
        NotificationSeverity severity,
        NotificationCategory category,
        string actionUrl,
        IReadOnlyDictionary<string, string>? metadata) : base(id)
    {
        WorkspaceId = workspaceId;
        Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("Title is required.", nameof(title)) : title.Trim();
        Message = message?.Trim() ?? string.Empty;
        Severity = severity;
        Category = category;
        ActionUrl = string.IsNullOrWhiteSpace(actionUrl) ? "/" : actionUrl.Trim();
        Metadata = metadata ?? new Dictionary<string, string>();
        CreatedAt = DateTime.UtcNow;

        AddDomainEvent(new NotificationCreatedEvent(this));
    }

    public static Notification Create(
        string title,
        string message,
        NotificationSeverity severity,
        NotificationCategory category,
        string actionUrl = "/",
        Guid? workspaceId = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new Notification(Guid.NewGuid(), workspaceId, title, message, severity, category, actionUrl, metadata);
    }

    public void MarkAsRead()
    {
        if (!IsRead)
        {
            IsRead = true;
            LastModifiedAt = DateTime.UtcNow;
        }
    }

    public void Dismiss()
    {
        if (!IsDismissed)
        {
            IsDismissed = true;
            LastModifiedAt = DateTime.UtcNow;
        }
    }
}
