using ConvoLab.Domain.Intelligence.Events;
using ConvoLab.Domain.Notifications;
using MediatR;

namespace ConvoLab.Application.Notifications.Handlers;

public sealed class FallbackExecutedNotificationHandler : INotificationHandler<FallbackExecutedEvent>
{
    private readonly INotificationService _notifications;

    public FallbackExecutedNotificationHandler(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task Handle(FallbackExecutedEvent notification, CancellationToken cancellationToken)
    {
        await _notifications.CreateAsync(new CreateNotificationRequest(
            Title: "Model Fallback Engaged",
            Message: $"Primary provider failed. Executing fallback model '{notification.FallbackModelId.Value}' at stage {notification.FallbackPosition}.",
            Severity: NotificationSeverity.Warning,
            Category: NotificationCategory.Execution,
            ActionUrl: "/intelligence",
            Metadata: new Dictionary<string, string>
            {
                ["RequestId"] = notification.RequestId.Value.ToString(),
                ["FallbackModelId"] = notification.FallbackModelId.Value.ToString(),
                ["FallbackPosition"] = notification.FallbackPosition.ToString()
            }), cancellationToken);
    }
}
