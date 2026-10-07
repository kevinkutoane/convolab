using ConvoLab.Domain.Notifications;

namespace ConvoLab.Application.Notifications;

public sealed class NotificationService : INotificationService
{
    private readonly INotificationStore _store;

    public NotificationService(INotificationStore store)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
        Guid? workspaceId,
        bool unreadOnly = false,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var items = await _store.ListAsync(workspaceId, unreadOnly, limit, cancellationToken);
        return items.Select(MapToDto).ToList();
    }

    public Task<int> GetUnreadCountAsync(Guid? workspaceId, CancellationToken cancellationToken = default)
    {
        return _store.GetUnreadCountAsync(workspaceId, cancellationToken);
    }

    public Task<bool> MarkAsReadAsync(Guid id, Guid? workspaceId, CancellationToken cancellationToken = default)
    {
        return _store.MarkAsReadAsync(id, workspaceId, cancellationToken);
    }

    public Task MarkAllAsReadAsync(Guid? workspaceId, CancellationToken cancellationToken = default)
    {
        return _store.MarkAllAsReadAsync(workspaceId, cancellationToken);
    }

    public Task<bool> DismissAsync(Guid id, Guid? workspaceId, CancellationToken cancellationToken = default)
    {
        return _store.DismissAsync(id, workspaceId, cancellationToken);
    }

    public Task ClearAllAsync(Guid? workspaceId, CancellationToken cancellationToken = default)
    {
        return _store.ClearAllAsync(workspaceId, cancellationToken);
    }

    public async Task<NotificationDto> CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var notification = Notification.Create(
            title: request.Title,
            message: request.Message,
            severity: request.Severity,
            category: request.Category,
            actionUrl: request.ActionUrl,
            workspaceId: request.WorkspaceId,
            metadata: request.Metadata);

        await _store.AddAsync(notification, cancellationToken);
        return MapToDto(notification);
    }

    private static NotificationDto MapToDto(Notification entity)
    {
        return new NotificationDto(
            Id: entity.Id,
            WorkspaceId: entity.WorkspaceId,
            Title: entity.Title,
            Message: entity.Message,
            Severity: entity.Severity.ToString().ToLowerInvariant(),
            Category: entity.Category.ToString().ToLowerInvariant(),
            ActionUrl: entity.ActionUrl,
            CreatedAt: new DateTimeOffset(entity.CreatedAt, TimeSpan.Zero),
            IsRead: entity.IsRead,
            IsDismissed: entity.IsDismissed,
            Metadata: entity.Metadata);
    }
}
