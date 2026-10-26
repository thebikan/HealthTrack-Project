using HealthTrack.Application.Appointments;
using HealthTrack.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Web.Controllers;

[ApiController, Authorize, Route("api/appointments")]
public sealed class AppointmentsController(AppointmentService appointments) : ControllerBase
{
    [HttpPost, Authorize(Roles = Roles.Client)]
    public async Task<IActionResult> Create(AppointmentRequest request, CancellationToken ct) => StatusCode(201, new { id = await appointments.RequestAsync(request, ct) });
    [HttpGet]
    public Task<IReadOnlyList<AppointmentView>> List(DateTime from, DateTime to, int page = 1, int size = 20, CancellationToken ct = default) => appointments.ListAsync(from, to, page, size, ct);
    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> Change(int id, AppointmentChange request, CancellationToken ct)
    { await appointments.ChangeAsync(id, request, ct); return NoContent(); }
    [HttpPost("{id:int}/reschedule")]
    public async Task<IActionResult> Reschedule(int id, AppointmentReschedule request, CancellationToken ct)
    { await appointments.RescheduleAsync(id, request, ct); return NoContent(); }
    [HttpGet("{id:int}/history")]
    public Task<IReadOnlyList<AppointmentHistoryView>> History(int id, int page = 1, int size = 20, CancellationToken ct = default) => appointments.HistoryAsync(id, page, size, ct);
}
