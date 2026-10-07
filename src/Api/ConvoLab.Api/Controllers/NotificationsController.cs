using ConvoLab.Application.Notifications;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.AspNetCore.Mvc;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;
    private readonly WorkspaceRequestContext _workspace;

    public NotificationsController(INotificationService notifications, WorkspaceRequestContext workspace)
    {
        _notifications = notifications;
        _workspace = workspace;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NotificationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var items = await _notifications.GetNotificationsAsync(_workspace.WorkspaceId, unreadOnly, limit, cancellationToken);
        return Ok(items);
    }

    [HttpGet("unread-count")]
    [ProducesResponseType<UnreadCountResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var count = await _notifications.GetUnreadCountAsync(_workspace.WorkspaceId, cancellationToken);
        return Ok(new UnreadCountResponse(count));
    }

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var success = await _notifications.MarkAsReadAsync(id, _workspace.WorkspaceId, cancellationToken);
        return success ? NoContent() : NotFound();
    }

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        await _notifications.MarkAllAsReadAsync(_workspace.WorkspaceId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken cancellationToken)
    {
        var success = await _notifications.DismissAsync(id, _workspace.WorkspaceId, cancellationToken);
        return success ? NoContent() : NotFound();
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ClearAll(CancellationToken cancellationToken)
    {
        await _notifications.ClearAllAsync(_workspace.WorkspaceId, cancellationToken);
        return NoContent();
    }

    [HttpPost]
    [ProducesResponseType<NotificationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NotificationDto>> Create(
        [FromBody] CreateNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var payload = request with { WorkspaceId = request.WorkspaceId ?? _workspace.WorkspaceId };
        var created = await _notifications.CreateAsync(payload, cancellationToken);
        return Created($"/api/notifications/{created.Id}", created);
    }
}

public sealed record UnreadCountResponse(int Count);
