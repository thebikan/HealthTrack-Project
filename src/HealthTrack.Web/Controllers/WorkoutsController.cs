using HealthTrack.Application.Security;
using HealthTrack.Application.Workouts;
using HealthTrack.Domain.Workouts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Web.Controllers;

[ApiController, Authorize, Route("api/workouts")]
public sealed class WorkoutsController(WorkoutService workouts) : ControllerBase
{
    [HttpPost("plans"), Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> Create(WorkoutDraftRequest request, CancellationToken ct) => StatusCode(201, new { id = await workouts.CreateAsync(request, ct) });
    [HttpPut("plans/{id:int}"), Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> Edit(int id, WorkoutDraftRequest request, CancellationToken ct)
    { await workouts.EditAsync(id, request, ct); return NoContent(); }
    [HttpPost("plans/{id:int}/publish"), Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> Publish(int id, ClientTarget request, CancellationToken ct)
    { await workouts.PublishAsync(id, request.ClientUserId, ct); return NoContent(); }
    [HttpPost("plans/{id:int}/archive"), Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> Archive(int id, ClientTarget request, CancellationToken ct)
    { await workouts.ArchiveAsync(id, request.ClientUserId, ct); return NoContent(); }
    [HttpGet("clients/{clientId}/plans")]
    public Task<IReadOnlyList<WorkoutPlanView>> Plans(string clientId, int page = 1, int size = 20, CancellationToken ct = default,
        PlanStatus? status = null, DateOnly? from = null, DateOnly? to = null) => workouts.PlansAsync(clientId, page, size, ct, status, from, to);
    [HttpPost("plans/{id:int}/revisions"), Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> Revise(int id, ClientTarget request, CancellationToken ct) => StatusCode(201, new { id = await workouts.ReviseAsync(id, request.ClientUserId, ct) });
    [HttpPut("plans/{id:int}/sessions"), Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> Schedule(int id, SessionRequest request, CancellationToken ct)
    { await workouts.SetSessionsAsync(id, request.ClientUserId, request.Times, ct); return NoContent(); }
    [HttpGet("clients/{clientId}/plans/{id:int}/sessions")]
    public Task<IReadOnlyList<SessionView>> Sessions(int id, string clientId, CancellationToken ct) => workouts.SessionsAsync(id, clientId, ct);
    [HttpGet("templates"), Authorize(Roles = Roles.Trainer)]
    public Task<IReadOnlyList<TemplateView>> Templates(int page = 1, int size = 20, bool archived = false, CancellationToken ct = default) => workouts.TemplatesAsync(page, size, archived, ct);
    [HttpPost("templates"), Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> Template(TemplateRequest request, CancellationToken ct) => StatusCode(201, new { id = await workouts.SaveTemplateAsync(null, request, ct) });
    [HttpPut("templates/{id:int}"), Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> UpdateTemplate(int id, TemplateRequest request, CancellationToken ct)
    { await workouts.SaveTemplateAsync(id, request, ct); return NoContent(); }
    [HttpPost("templates/{id:int}/archive"), Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> ArchiveTemplate(int id, CancellationToken ct)
    { await workouts.ArchiveTemplateAsync(id, ct); return NoContent(); }
    [HttpPost("templates/{id:int}/clone"), Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> Clone(int id, CloneTemplateRequest request, CancellationToken ct) => StatusCode(201, new { id = await workouts.CloneTemplateAsync(id, request.ClientUserId, request.StartDate, request.EndDate, ct) });
    [HttpPost("logs"), Authorize(Roles = Roles.Client)]
    public async Task<IActionResult> Log(WorkoutLogRequest request, CancellationToken ct) => StatusCode(201, new { id = await workouts.LogAsync(request, ct) });
    [HttpGet("clients/{clientId}/logs")]
    public Task<IReadOnlyList<WorkoutLogView>> Logs(string clientId, DateTime from, DateTime to, int page = 1, int size = 20, CancellationToken ct = default) =>
        workouts.LogsAsync(clientId, from, to, page, size, ct);
}

public sealed record ClientTarget(string ClientUserId);
public sealed record SessionRequest(string ClientUserId, IReadOnlyList<DateTime> Times);
public sealed record CloneTemplateRequest(string ClientUserId, DateOnly StartDate, DateOnly EndDate);
