using ConvoLab.Domain.Intelligence.Events;
using ConvoLab.Domain.Notifications;
using MediatR;

namespace ConvoLab.Application.Notifications.Handlers;

public sealed class BudgetExhaustedNotificationHandler : INotificationHandler<BudgetExhaustedEvent>
{
    private readonly INotificationService _notifications;

    public BudgetExhaustedNotificationHandler(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task Handle(BudgetExhaustedEvent notification, CancellationToken cancellationToken)
    {
        await _notifications.CreateAsync(new CreateNotificationRequest(
            Title: "AI Budget Exhausted",
            Message: $"Execution budget '{notification.BudgetId.Value}' has reached its allocation limit of {notification.Limit.Amount} {notification.Limit.Currency}.",
            Severity: NotificationSeverity.Warning,
            Category: NotificationCategory.Budget,
            ActionUrl: "/intelligence",
            Metadata: new Dictionary<string, string>
            {
                ["BudgetId"] = notification.BudgetId.Value.ToString(),
                ["Limit"] = $"{notification.Limit.Amount} {notification.Limit.Currency}"
            }), cancellationToken);
    }
}
