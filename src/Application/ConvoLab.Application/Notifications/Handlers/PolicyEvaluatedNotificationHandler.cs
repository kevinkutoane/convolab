using ConvoLab.Domain.Notifications;
using ConvoLab.Domain.Policy.Enums;
using ConvoLab.Domain.Policy.Events;
using MediatR;

namespace ConvoLab.Application.Notifications.Handlers;

public sealed class PolicyEvaluatedNotificationHandler : INotificationHandler<PolicyEvaluatedEvent>
{
    private readonly INotificationService _notifications;

    public PolicyEvaluatedNotificationHandler(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task Handle(PolicyEvaluatedEvent notification, CancellationToken cancellationToken)
    {
        // Only notify on Deny/Block events
        if (notification.Effect != PolicyEffect.Deny)
        {
            return;
        }

        await _notifications.CreateAsync(new CreateNotificationRequest(
            Title: $"Policy Block Triggered ({notification.Domain})",
            Message: $"Governance policy '{notification.PolicyId.Value}' blocked an outbound conversation action.",
            Severity: NotificationSeverity.Warning,
            Category: NotificationCategory.Policy,
            ActionUrl: "/policies",
            Metadata: new Dictionary<string, string>
            {
                ["PolicyId"] = notification.PolicyId.Value.ToString(),
                ["Domain"] = notification.Domain.ToString(),
                ["Effect"] = notification.Effect.ToString()
            }), cancellationToken);
    }
}
