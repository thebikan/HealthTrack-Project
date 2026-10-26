using HealthTrack.Application.Health;
using HealthTrack.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Web.Controllers;

[ApiController, Authorize, Route("api/health")]
public sealed class HealthController(HealthService health) : ControllerBase
{
    [HttpPost("measurements"), Authorize(Roles = Roles.Client)]
    public async Task<IActionResult> Record(MeasurementRequest request, CancellationToken ct) => StatusCode(201, new { id = await health.RecordAsync(request, ct) });
    [HttpGet("clients/{clientId}/measurements")]
    public Task<IReadOnlyList<MeasurementView>> History(string clientId, DateTime from, DateTime to, int page = 1, int size = 20, CancellationToken ct = default) => health.HistoryAsync(clientId, from, to, page, size, ct);
    [HttpPost("feedback"), Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> AddFeedback(FeedbackRequest request, CancellationToken ct) => StatusCode(201, new { id = await health.AddFeedbackAsync(request, ct) });
    [HttpGet("clients/{clientId}/feedback")]
    public Task<IReadOnlyList<FeedbackView>> Feedback(string clientId, int page = 1, int size = 20, CancellationToken ct = default) => health.FeedbackAsync(clientId, page, size, ct);
}
