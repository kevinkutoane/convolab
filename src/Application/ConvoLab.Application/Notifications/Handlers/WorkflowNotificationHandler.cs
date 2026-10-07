using ConvoLab.Domain.Execution.Events;
using ConvoLab.Domain.Notifications;
using MediatR;

namespace ConvoLab.Application.Notifications.Handlers;

public sealed class WorkflowNotificationHandler :
    INotificationHandler<WorkflowFailed>,
    INotificationHandler<WorkflowCompleted>
{
    private readonly INotificationService _notifications;

    public WorkflowNotificationHandler(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task Handle(WorkflowFailed notification, CancellationToken cancellationToken)
    {
        await _notifications.CreateAsync(new CreateNotificationRequest(
            Title: "Workflow Execution Failed",
            Message: notification.Error,
            Severity: NotificationSeverity.Error,
            Category: NotificationCategory.Execution,
            ActionUrl: "/workflows",
            Metadata: new Dictionary<string, string>
            {
                ["ExecutionId"] = notification.ExecutionId.Value.ToString()
            }), cancellationToken);
    }

    public async Task Handle(WorkflowCompleted notification, CancellationToken cancellationToken)
    {
        await _notifications.CreateAsync(new CreateNotificationRequest(
            Title: "Workflow Completed",
            Message: $"Workflow execution finished with status {notification.Result.Status}.",
            Severity: NotificationSeverity.Success,
            Category: NotificationCategory.Execution,
            ActionUrl: "/workflows",
            Metadata: new Dictionary<string, string>
            {
                ["ExecutionId"] = notification.ExecutionId.Value.ToString(),
                ["Status"] = notification.Result.Status.ToString()
            }), cancellationToken);
    }
}
