using System.Collections.Concurrent;
using ConvoLab.Domain.Notifications;

namespace ConvoLab.Application.Notifications;

public sealed class RuntimeNotificationStore : INotificationStore
{
    private const int MaxNotificationsPerWorkspace = 200;
    private readonly ConcurrentDictionary<Guid, Notification> _notifications = new();
    private readonly object _lock = new();

    public RuntimeNotificationStore()
    {
        SeedDefaultNotifications();
    }

    public Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        _notifications[notification.Id] = notification;

        // Bound storage per workspace
        TrimExcess(notification.WorkspaceId);
        return Task.CompletedTask;
    }

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _notifications.TryGetValue(id, out var item);
        return Task.FromResult(item);
    }

    public Task<IReadOnlyList<Notification>> ListAsync(
        Guid? workspaceId,
        bool unreadOnly = false,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var items = _notifications.Values
            .Where(n => !n.IsDismissed)
            .Where(n => workspaceId == null || n.WorkspaceId == null || n.WorkspaceId == workspaceId)
            .Where(n => !unreadOnly || !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<Notification>>(items);
    }

    public Task<int> GetUnreadCountAsync(Guid? workspaceId, CancellationToken cancellationToken = default)
    {
        var count = _notifications.Values
            .Count(n => !n.IsDismissed
                        && !n.IsRead
                        && (workspaceId == null || n.WorkspaceId == null || n.WorkspaceId == workspaceId));

        return Task.FromResult(count);
    }

    public Task<bool> MarkAsReadAsync(Guid id, Guid? workspaceId, CancellationToken cancellationToken = default)
    {
        if (_notifications.TryGetValue(id, out var item))
        {
            if (workspaceId != null && item.WorkspaceId != null && item.WorkspaceId != workspaceId)
            {
                return Task.FromResult(false);
            }

            item.MarkAsRead();
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task MarkAllAsReadAsync(Guid? workspaceId, CancellationToken cancellationToken = default)
    {
        foreach (var item in _notifications.Values)
        {
            if (!item.IsDismissed && (workspaceId == null || item.WorkspaceId == null || item.WorkspaceId == workspaceId))
            {
                item.MarkAsRead();
            }
        }

        return Task.CompletedTask;
    }

    public Task<bool> DismissAsync(Guid id, Guid? workspaceId, CancellationToken cancellationToken = default)
    {
        if (_notifications.TryGetValue(id, out var item))
        {
            if (workspaceId != null && item.WorkspaceId != null && item.WorkspaceId != workspaceId)
            {
                return Task.FromResult(false);
            }

            item.Dismiss();
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task ClearAllAsync(Guid? workspaceId, CancellationToken cancellationToken = default)
    {
        foreach (var item in _notifications.Values)
        {
            if (workspaceId == null || item.WorkspaceId == null || item.WorkspaceId == workspaceId)
            {
                item.Dismiss();
            }
        }

        return Task.CompletedTask;
    }

    private void TrimExcess(Guid? workspaceId)
    {
        lock (_lock)
        {
            var workspaceItems = _notifications.Values
                .Where(n => n.WorkspaceId == workspaceId)
                .OrderByDescending(n => n.CreatedAt)
                .ToList();

            if (workspaceItems.Count > MaxNotificationsPerWorkspace)
            {
                var excess = workspaceItems.Skip(MaxNotificationsPerWorkspace);
                foreach (var oldItem in excess)
                {
                    _notifications.TryRemove(oldItem.Id, out _);
                }
            }
        }
    }

    private void SeedDefaultNotifications()
    {
        var ready = Notification.Create(
            title: "Platform Status: Healthy",
            message: "ConvoLab Platform services and model catalogue are fully operational.",
            severity: NotificationSeverity.Info,
            category: NotificationCategory.System,
            actionUrl: "/operations");

        _notifications[ready.Id] = ready;
    }
}
