using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.Events;
using ConvoLab.Domain.Notifications;
using MediatR;

namespace ConvoLab.Application.Notifications.Handlers;

public sealed class ProviderHealthChangedNotificationHandler : INotificationHandler<ProviderHealthChangedEvent>
{
    private readonly INotificationService _notifications;

    public ProviderHealthChangedNotificationHandler(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task Handle(ProviderHealthChangedEvent notification, CancellationToken cancellationToken)
    {
        var isCritical = notification.Availability == ProviderAvailability.Degraded
            || notification.Availability == ProviderAvailability.Unavailable
            || notification.Circuit == CircuitStatus.Open;

        if (!isCritical)
        {
            return;
        }

        var severity = notification.Circuit == CircuitStatus.Open || notification.Availability == ProviderAvailability.Unavailable
            ? NotificationSeverity.Error
            : NotificationSeverity.Warning;

        await _notifications.CreateAsync(new CreateNotificationRequest(
            Title: $"Provider Circuit Alert: {notification.ProviderId.Value}",
            Message: $"Provider health transitioned to {notification.Availability} with circuit state {notification.Circuit}.",
            Severity: severity,
            Category: NotificationCategory.System,
            ActionUrl: "/operations",
            Metadata: new Dictionary<string, string>
            {
                ["ProviderId"] = notification.ProviderId.Value.ToString(),
                ["Availability"] = notification.Availability.ToString(),
                ["Circuit"] = notification.Circuit.ToString()
            }), cancellationToken);
    }
}
