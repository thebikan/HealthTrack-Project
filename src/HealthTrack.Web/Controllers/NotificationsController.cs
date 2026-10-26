using HealthTrack.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Web.Controllers;

[ApiController, Authorize, Route("api/notifications")]
public sealed class NotificationsController(NotificationService notifications) : ControllerBase
{
    [HttpGet("preferences")]
    public Task<NotificationPreferences> Preferences(CancellationToken ct) => notifications.PreferencesAsync(ct);
    [HttpPut("preferences")]
    public async Task<IActionResult> Preferences(NotificationPreferences request, CancellationToken ct)
    { await notifications.UpdatePreferencesAsync(request, ct); return NoContent(); }
    [HttpGet]
    public Task<IReadOnlyList<NotificationView>> List(bool unreadOnly = false, int page = 1, int size = 20, CancellationToken ct = default) => notifications.ListAsync(unreadOnly, page, size, ct);
    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> Read(int id, CancellationToken ct) { await notifications.ReadAsync(id, ct); return NoContent(); }
    [HttpGet("schedules")]
    public Task<IReadOnlyList<ReminderView>> Schedules(int page = 1, int size = 20, CancellationToken ct = default) => notifications.SchedulesAsync(page, size, ct);
    [HttpPost("schedules")]
    public async Task<IActionResult> Schedule(ReminderRequest request, CancellationToken ct) => StatusCode(201, new { id = await notifications.ScheduleAsync(request, ct) });
    [HttpPost("schedules/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, RevokeRequest request, CancellationToken ct)
    { await notifications.CancelAsync(id, request.RowVersion, ct); return NoContent(); }
}
