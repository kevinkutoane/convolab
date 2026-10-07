using ConvoLab.Domain.Intelligence.Events;
using ConvoLab.Domain.Notifications;
using MediatR;

namespace ConvoLab.Application.Notifications.Handlers;

public sealed class ExecutionFailedNotificationHandler : INotificationHandler<ExecutionFailedEvent>
{
    private readonly INotificationService _notifications;

    public ExecutionFailedNotificationHandler(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task Handle(ExecutionFailedEvent notification, CancellationToken cancellationToken)
    {
        await _notifications.CreateAsync(new CreateNotificationRequest(
            Title: $"AI Execution Failed ({notification.Kind})",
            Message: notification.Reason,
            Severity: NotificationSeverity.Error,
            Category: NotificationCategory.Execution,
            ActionUrl: "/intelligence",
            Metadata: new Dictionary<string, string>
            {
                ["RequestId"] = notification.RequestId.Value.ToString(),
                ["FailureKind"] = notification.Kind.ToString()
            }), cancellationToken);
    }
}
