using HealthTrack.Application.Accounts;
using HealthTrack.Application.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HealthTrack.Web.Controllers;

[ApiController]
[Route("api/account")]
[Authorize]
public sealed class AccountController(AccountService accounts) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        await accounts.RegisterAsync(request, ct);
        return StatusCode(201);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        await accounts.LoginAsync(request, ct);
        return NoContent();
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await accounts.LogoutAsync();
        return NoContent();
    }

    [HttpGet("me")]
    public Task<AccountProfile> Current(CancellationToken ct) => accounts.GetCurrentAsync(ct);

    [HttpPut("me")]
    public async Task<IActionResult> Update(ProfileUpdate request, CancellationToken ct)
    {
        await accounts.UpdateProfileAsync(request, ct);
        return NoContent();
    }

    [HttpPost("password")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Password(ChangePasswordRequest request, CancellationToken ct)
    {
        await accounts.ChangePasswordAsync(request, ct);
        return NoContent();
    }

    [HttpGet("users")]
    [Authorize(Roles = Roles.Admin)]
    public Task<IReadOnlyList<AccountSummary>> Users([FromQuery] int page = 1, [FromQuery] int size = 20,
        CancellationToken ct = default, string? search = null, bool? active = null, string? role = null) => accounts.ListAsync(page, size, ct, search, active, role);

    [HttpPut("users/{id}/activation")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Activate(string id, ActivationRequest request, CancellationToken ct)
    {
        await accounts.SetActiveAsync(id, request.IsActive, ct);
        return NoContent();
    }
}

public sealed record ActivationRequest(bool IsActive);

[ApiController]
[AllowAnonymous]
public sealed class AntiforgeryController : ControllerBase
{
    [HttpGet("api/account/csrf")]
    public IActionResult Csrf([FromServices] IAntiforgery antiforgery)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    }
}
