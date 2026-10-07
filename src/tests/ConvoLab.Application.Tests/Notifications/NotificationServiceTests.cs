using ConvoLab.Application.Notifications;
using ConvoLab.Application.Notifications.Handlers;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.Events;
using ConvoLab.Domain.Intelligence.ValueObjects;
using ConvoLab.Domain.Notifications;
using ConvoLab.Domain.Policy.Enums;
using ConvoLab.Domain.Policy.Events;
using ConvoLab.Domain.Policy.ValueObjects;
using Xunit;

namespace ConvoLab.Application.Tests.Notifications;

public sealed class NotificationServiceTests
{
    private readonly RuntimeNotificationStore _store;
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _store = new RuntimeNotificationStore();
        _service = new NotificationService(_store);
    }

    [Fact]
    public async Task CreateAsync_AddsNotificationToStore()
    {
        var workspaceId = Guid.NewGuid();
        var request = new CreateNotificationRequest(
            Title: "Budget Exhausted",
            Message: "AI spend exceeded limit.",
            Severity: NotificationSeverity.Warning,
            Category: NotificationCategory.Budget,
            ActionUrl: "/intelligence",
            WorkspaceId: workspaceId);

        var result = await _service.CreateAsync(request);

        Assert.NotNull(result);
        Assert.Equal("Budget Exhausted", result.Title);
        Assert.Equal("warning", result.Severity);
        Assert.Equal("budget", result.Category);
        Assert.False(result.IsRead);

        var list = await _service.GetNotificationsAsync(workspaceId);
        Assert.Contains(list, n => n.Id == result.Id);
    }

    [Fact]
    public async Task MarkAsReadAsync_UpdatesNotificationStatus()
    {
        var workspaceId = Guid.NewGuid();
        var created = await _service.CreateAsync(new CreateNotificationRequest(
            Title: "Test",
            Message: "Message",
            Severity: NotificationSeverity.Info,
            Category: NotificationCategory.System,
            WorkspaceId: workspaceId));

        var updated = await _service.MarkAsReadAsync(created.Id, workspaceId);
        Assert.True(updated);

        var unread = await _service.GetNotificationsAsync(workspaceId, unreadOnly: true);
        Assert.DoesNotContain(unread, n => n.Id == created.Id);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_MarksAllWorkspaceNotificationsAsRead()
    {
        var workspaceId = Guid.NewGuid();
        await _service.CreateAsync(new CreateNotificationRequest("Test 1", "Msg 1", NotificationSeverity.Info, NotificationCategory.System, WorkspaceId: workspaceId));
        await _service.CreateAsync(new CreateNotificationRequest("Test 2", "Msg 2", NotificationSeverity.Warning, NotificationCategory.Budget, WorkspaceId: workspaceId));

        await _service.MarkAllAsReadAsync(workspaceId);

        var unreadCount = await _service.GetUnreadCountAsync(workspaceId);
        Assert.Equal(0, unreadCount);
    }

    [Fact]
    public async Task DismissAsync_HidesNotificationFromList()
    {
        var workspaceId = Guid.NewGuid();
        var created = await _service.CreateAsync(new CreateNotificationRequest(
            Title: "Dismiss Me",
            Message: "Msg",
            Severity: NotificationSeverity.Info,
            Category: NotificationCategory.System,
            WorkspaceId: workspaceId));

        var dismissed = await _service.DismissAsync(created.Id, workspaceId);
        Assert.True(dismissed);

        var list = await _service.GetNotificationsAsync(workspaceId);
        Assert.DoesNotContain(list, n => n.Id == created.Id);
    }

    [Fact]
    public async Task BudgetExhaustedHandler_CreatesNotification()
    {
        var handler = new BudgetExhaustedNotificationHandler(_service);
        var budgetId = ExecutionBudgetId.CreateUnique();
        var limit = ExecutionCost.Create(500m, "ZAR");

        await handler.Handle(new BudgetExhaustedEvent(budgetId, limit), CancellationToken.None);

        var list = await _service.GetNotificationsAsync(null);
        Assert.Contains(list, n => n.Category == "budget" && n.Title.Contains("Budget Exhausted"));
    }

    [Fact]
    public async Task ExecutionFailedHandler_CreatesNotification()
    {
        var handler = new ExecutionFailedNotificationHandler(_service);
        var requestId = ExecutionRequestId.CreateUnique();

        await handler.Handle(new ExecutionFailedEvent(requestId, FailureKind.Timeout, "Provider connection timed out."), CancellationToken.None);

        var list = await _service.GetNotificationsAsync(null);
        Assert.Contains(list, n => n.Category == "execution" && n.Severity == "error");
    }

    [Fact]
    public async Task PolicyEvaluatedHandler_NotifiesOnlyOnDeny()
    {
        var handler = new PolicyEvaluatedNotificationHandler(_service);
        var policyId = PolicyDefinitionId.CreateUnique();

        // Allow should not trigger notification
        await handler.Handle(new PolicyEvaluatedEvent(policyId, PolicyDomain.Safety, PolicyEffect.Allow), CancellationToken.None);
        var listAfterAllow = await _service.GetNotificationsAsync(null);
        Assert.DoesNotContain(listAfterAllow, n => n.Category == "policy");

        // Deny should trigger notification
        await handler.Handle(new PolicyEvaluatedEvent(policyId, PolicyDomain.Safety, PolicyEffect.Deny), CancellationToken.None);
        var listAfterDeny = await _service.GetNotificationsAsync(null);
        Assert.Contains(listAfterDeny, n => n.Category == "policy" && n.Severity == "warning");
    }
}
