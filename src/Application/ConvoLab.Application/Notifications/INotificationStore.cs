using ConvoLab.Domain.Notifications;

namespace ConvoLab.Application.Notifications;

public interface INotificationStore
{
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> ListAsync(Guid? workspaceId, bool unreadOnly = false, int limit = 50, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid? workspaceId, CancellationToken cancellationToken = default);
    Task<bool> MarkAsReadAsync(Guid id, Guid? workspaceId, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(Guid? workspaceId, CancellationToken cancellationToken = default);
    Task<bool> DismissAsync(Guid id, Guid? workspaceId, CancellationToken cancellationToken = default);
    Task ClearAllAsync(Guid? workspaceId, CancellationToken cancellationToken = default);
}
