using System.Net.Mail;
using HealthTrack.Application.Security;

namespace HealthTrack.Application.Accounts;

public sealed record RegisterRequest(string Email, string Password, string FullName, string Role);
public sealed record LoginRequest(string Email, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record ProfileUpdate(string FullName, decimal? HeightCm, string? Specialization);
public sealed record AccountProfile(string Id, string Email, string FullName, bool IsActive,
    IReadOnlyCollection<string> Roles, decimal? HeightCm = null, string? Specialization = null);
public sealed record AccountSummary(string Id, string Email, string FullName, bool IsActive);

public interface IAccountGateway
{
    Task RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<bool> LoginAsync(LoginRequest request, CancellationToken ct);
    Task LogoutAsync();
    Task<AccountProfile?> GetAsync(string userId, CancellationToken ct);
    Task UpdateProfileAsync(string userId, ProfileUpdate request, CancellationToken ct);
    Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken ct);
    Task<IReadOnlyList<AccountSummary>> ListAsync(int page, int size, CancellationToken ct, string? search = null, bool? active = null, string? role = null);
    Task SetActiveAsync(string userId, bool active, string actorId, CancellationToken ct);
}

public sealed class AccountService(IAccountGateway gateway, ICurrentActor currentActor)
{
    public Task RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (request.Role is not (Roles.Client or Roles.Trainer))
            throw RequestFailure.Invalid("Public registration permits Client or Trainer only.");
        var email = Email(request.Email);
        Password(request.Password);
        return gateway.RegisterAsync(request with
        {
            Email = email, FullName = Input.Text(request.FullName, 200, "Full name")
        }, ct);
    }

    public async Task LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = Email(request.Email);
        Password(request.Password);
        if (!await gateway.LoginAsync(request with { Email = email }, ct))
            throw new RequestFailure("invalid_credentials", 401, "Invalid credentials or account unavailable.");
    }

    public Task LogoutAsync() => gateway.LogoutAsync();

    public async Task<AccountProfile> GetCurrentAsync(CancellationToken ct = default)
    {
        var actor = await RequireActorAsync(ct);
        var profile = await gateway.GetAsync(actor.UserId, ct);
        return profile is { IsActive: true } ? profile : throw RequestFailure.Forbidden();
    }

    public async Task UpdateProfileAsync(ProfileUpdate request, CancellationToken ct = default)
    {
        var actor = await RequireActorAsync(ct);
        var name = Input.Text(request.FullName, 200, "Full name");
        if (request.HeightCm is <= 0 or > 9999.99m ||
            (request.HeightCm.HasValue && decimal.Round(request.HeightCm.Value, 2) != request.HeightCm.Value))
            throw RequestFailure.Invalid("Height must be positive with at most two decimal places.");
        if (request.HeightCm.HasValue && !actor.IsInRole(Roles.Client)) throw RequestFailure.Forbidden();
        if (request.Specialization is not null && !actor.IsInRole(Roles.Trainer)) throw RequestFailure.Forbidden();
        if (request.Specialization?.Length > 200) throw RequestFailure.Invalid("Specialization must not exceed 200 characters.");
        await gateway.UpdateProfileAsync(actor.UserId, request with { FullName = name, Specialization = request.Specialization?.Trim() }, ct);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        var actor = await RequireActorAsync(ct);
        Password(request.CurrentPassword);
        Password(request.NewPassword);
        await gateway.ChangePasswordAsync(actor.UserId, request, ct);
    }

    public async Task<IReadOnlyList<AccountSummary>> ListAsync(int page, int size, CancellationToken ct = default, string? search = null, bool? active = null, string? role = null)
    {
        await RequireAdminAsync(ct);
        Input.Page(page, size);
        if (search?.Length > 200 || (role is not null && !Roles.All.Contains(role))) throw RequestFailure.Invalid("Invalid account filter.");
        return await gateway.ListAsync(page, size, ct, search?.Trim(), active, role);
    }

    public async Task SetActiveAsync(string userId, bool active, CancellationToken ct = default)
    {
        var actor = await RequireAdminAsync(ct);
        if (actor.UserId == userId) throw RequestFailure.Invalid("Administrators cannot change their own activation status.");
        await gateway.SetActiveAsync(Input.Text(userId, 128, "User ID"), active, actor.UserId, ct);
    }

    private async Task<Actor> RequireActorAsync(CancellationToken ct) =>
        await currentActor.GetAsync(ct) ?? throw new RequestFailure("unauthenticated", 401, "Authentication required.");

    private async Task<Actor> RequireAdminAsync(CancellationToken ct)
    {
        var actor = await RequireActorAsync(ct);
        return actor.IsInRole(Roles.Admin) ? actor : throw RequestFailure.Forbidden();
    }

    private static string Email(string? value)
    {
        var email = Input.Text(value, 256, "Email");
        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
            throw RequestFailure.Invalid("A valid email address is required.");
        return email;
    }

    private static void Password(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128)
            throw RequestFailure.Invalid("Password is required and must not exceed 128 characters.");
    }
}
