namespace ConvoLab.Application.Notifications;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
        Guid? workspaceId,
        bool unreadOnly = false,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        Guid? workspaceId,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAsReadAsync(
        Guid id,
        Guid? workspaceId,
        CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(
        Guid? workspaceId,
        CancellationToken cancellationToken = default);

    Task<bool> DismissAsync(
        Guid id,
        Guid? workspaceId,
        CancellationToken cancellationToken = default);

    Task ClearAllAsync(
        Guid? workspaceId,
        CancellationToken cancellationToken = default);

    Task<NotificationDto> CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default);
}
