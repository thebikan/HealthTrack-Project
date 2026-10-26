using HealthTrack.Application.Nutrition;
using HealthTrack.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Web.Controllers;

[ApiController, Authorize, Route("api/nutrition")]
public sealed class NutritionController(NutritionService nutrition) : ControllerBase
{
    [HttpPost("plans"), Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> Assign(DietPlanRequest request, CancellationToken ct) => StatusCode(201, new { id = await nutrition.AssignPlanAsync(request, ct) });
    [HttpPut("plans/{id:int}"), Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> Edit(int id, DietPlanRequest request, CancellationToken ct)
    { await nutrition.EditAsync(id, request, ct); return NoContent(); }
    [HttpGet("clients/{clientId}/plans")]
    public Task<IReadOnlyList<DietPlanView>> Plans(string clientId, int page = 1, int size = 20, CancellationToken ct = default, DateOnly? from = null, DateOnly? to = null) => nutrition.PlansAsync(clientId, page, size, ct, from, to);
    [HttpPost("logs"), Authorize(Roles = Roles.Client)]
    public async Task<IActionResult> Log(MealLogRequest request, CancellationToken ct) => StatusCode(201, new { id = await nutrition.LogAsync(request, ct) });
    [HttpGet("clients/{clientId}/logs")]
    public Task<IReadOnlyList<MealLogView>> Logs(string clientId, DateTime from, DateTime to, int page = 1, int size = 20, CancellationToken ct = default) =>
        nutrition.LogsAsync(clientId, from, to, page, size, ct);
}
