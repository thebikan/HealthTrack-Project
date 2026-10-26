using HealthTrack.Application.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HealthTrack.Web.Controllers;

[ApiController, AllowAnonymous, Route("api/account/recovery"), EnableRateLimiting("account")]
public sealed class PasswordRecoveryController(PasswordRecoveryService recovery) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Begin(RecoveryRequest request, CancellationToken ct)
    { await recovery.RequestAsync(request, ct); return Accepted(new { message = "If the account is eligible, recovery instructions will be sent." }); }
    [HttpPost("reset")]
    public async Task<IActionResult> Reset(ResetPasswordRequest request, CancellationToken ct)
    { await recovery.ResetAsync(request, ct); return NoContent(); }
}
