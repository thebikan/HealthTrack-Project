using HealthTrack.Application.Health;
using HealthTrack.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HealthTrack.Web.Controllers;

[ApiController, Authorize, Route("api/goals")]
public sealed class GoalsController(GoalService goals) : ControllerBase
{
    [HttpPost, Authorize(Roles = Roles.Client)]
    public async Task<IActionResult> Create(GoalRequest request, CancellationToken ct) => StatusCode(201, new { id = await goals.CreateAsync(request, ct) });
    [HttpPut("{id:int}"), Authorize(Roles = Roles.Client)]
    public async Task<IActionResult> Update(int id, GoalUpdate request, CancellationToken ct)
    { await goals.UpdateAsync(id, request, ct); return NoContent(); }
    [HttpPost("{id:int}/finish"), Authorize(Roles = Roles.Client)]
    public async Task<IActionResult> Finish(int id, FinishGoalRequest request, CancellationToken ct)
    { await goals.FinishAsync(id, request.RowVersion, request.Cancel, ct); return NoContent(); }
    [HttpGet("clients/{clientId}")]
    public Task<IReadOnlyList<GoalView>> List(string clientId, int page = 1, int size = 20, CancellationToken ct = default) => goals.ListAsync(clientId, page, size, ct);
}
public sealed record FinishGoalRequest(byte[] RowVersion, bool Cancel);
