using HealthTrack.Application.Reporting;
using HealthTrack.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Web.Controllers;

[ApiController, Authorize, Route("api/reports")]
public sealed class ReportsController(ReportingService reports) : ControllerBase
{
    [HttpGet("clients/{clientId}/adherence")]
    public Task<AdherenceReport> Adherence(string clientId, DateTime from, DateTime to, CancellationToken ct) => reports.AdherenceAsync(clientId, from, to, ct);
    [HttpGet("clients/{clientId}/progress.csv")]
    public async Task<IActionResult> ProgressCsv(string clientId, DateTime from, DateTime to, CancellationToken ct)
    { var file = await reports.ProgressCsvAsync(clientId, from, to, ct); return File(file.Content, "text/csv; charset=utf-8", file.FileName); }
    [HttpGet("admin.csv"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> AdminCsv(DateTime from, DateTime to, CancellationToken ct)
    { var file = await reports.AdminCsvAsync(from, to, ct); return File(file.Content, "text/csv; charset=utf-8", file.FileName); }
    [HttpGet("trainer.csv"), Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> TrainerCsv(DateTime from, DateTime to, CancellationToken ct)
    { var file = await reports.TrainerCsvAsync(from, to, ct); return File(file.Content, "text/csv; charset=utf-8", file.FileName); }
    [HttpGet("dashboard/client"), Authorize(Roles = Roles.Client)]
    public Task<ClientDashboard> Client(CancellationToken ct) => reports.ClientAsync(ct);
    [HttpGet("dashboard/trainer"), Authorize(Roles = Roles.Trainer)]
    public Task<TrainerDashboard> Trainer(int page = 1, int size = 20, CancellationToken ct = default) => reports.TrainerAsync(page, size, ct);
    [HttpGet("dashboard/admin"), Authorize(Roles = Roles.Admin)]
    public Task<AdminDashboard> Admin(DateTime from, DateTime to, CancellationToken ct) => reports.AdminAsync(from, to, ct);
    [HttpGet("clients/{clientId}/progress")]
    public Task<ClientProgress> Progress(string clientId, DateTime from, DateTime to, CancellationToken ct) => reports.ProgressAsync(clientId, from, to, ct);
    [HttpGet("trainer"), Authorize(Roles = Roles.Trainer)]
    public Task<TrainerReport> Performance(DateTime from, DateTime to, CancellationToken ct) => reports.TrainerReportAsync(from, to, ct);
    [HttpGet("audit"), Authorize(Roles = Roles.Admin)]
    public Task<IReadOnlyList<AuditView>> Audit(DateTime from, DateTime to, int page = 1, int size = 20, CancellationToken ct = default, string? action = null, string? actorId = null) => reports.AuditAsync(from, to, page, size, ct, action, actorId);
}
