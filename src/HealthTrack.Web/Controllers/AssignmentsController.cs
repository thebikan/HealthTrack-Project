using HealthTrack.Application.Assignments;
using HealthTrack.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Web.Controllers;

[ApiController, Authorize, Route("api/assignments")]
public sealed class AssignmentsController(AssignmentService assignments) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<AssignmentView>> List(int page = 1, int size = 20, CancellationToken ct = default) =>
        assignments.ListAsync(page, size, ct);

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Assign(AssignRequest request, CancellationToken ct) =>
        StatusCode(201, new { id = await assignments.AssignAsync(request, ct) });

    [HttpPost("{id:int}/revoke"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Revoke(int id, RevokeRequest request, CancellationToken ct)
    {
        await assignments.RevokeAsync(id, request.RowVersion, ct);
        return NoContent();
    }
}

public sealed record RevokeRequest(byte[] RowVersion);
