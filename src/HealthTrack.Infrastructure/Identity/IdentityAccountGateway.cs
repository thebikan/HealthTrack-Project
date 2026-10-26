using System.Data;
using HealthTrack.Application.Accounts;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Profiles;
using HealthTrack.Domain.Auditing;
using HealthTrack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Infrastructure.Identity;

public sealed class IdentityAccountGateway(
    UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
    HealthTrackDbContext db, TimeProvider clock) : IAccountGateway
{
    public async Task RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (request.Role is not (Roles.Client or Roles.Trainer)) throw RequestFailure.Forbidden();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = new ApplicationUser
        {
            UserName = request.Email, Email = request.Email, FullName = request.FullName,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        };
        Check(await users.CreateAsync(user, request.Password), "Account could not be registered. Check the details and password requirements.");
        Check(await users.AddToRoleAsync(user, request.Role), "Account role could not be assigned.");
        if (request.Role == Roles.Client) db.ClientProfiles.Add(new ClientProfile { UserId = user.Id });
        else db.TrainerProfiles.Add(new TrainerProfile { UserId = user.Id });
        Audit(user.Id, "AccountRegistered");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<bool> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result = await signIn.PasswordSignInAsync(request.Email, request.Password, false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            try
            {
                var user = await users.FindByEmailAsync(request.Email) ?? throw RequestFailure.Forbidden();
                Audit(user.Id, "LoginSucceeded");
                await db.SaveChangesAsync(ct);
            }
            catch { await signIn.SignOutAsync(); throw; }
        }
        return result.Succeeded;
    }

    public async Task LogoutAsync()
    {
        var id = users.GetUserId(signIn.Context.User);
        await signIn.SignOutAsync();
        if (id is not null)
        {
            Audit(id, "Logout");
            await db.SaveChangesAsync(signIn.Context.RequestAborted);
        }
    }

    public async Task<AccountProfile?> GetAsync(string userId, CancellationToken ct)
    {
        var user = await users.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return null;
        var roles = await users.GetRolesAsync(user);
        var client = await db.ClientProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);
        var trainer = await db.TrainerProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);
        return new AccountProfile(user.Id, user.Email!, user.FullName, user.IsActive,
            roles.ToArray(), client?.HeightCm, trainer?.Specialization);
    }

    public async Task UpdateProfileAsync(string userId, ProfileUpdate request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await ActiveUserAsync(userId, ct);
        user.FullName = request.FullName;
        Check(await users.UpdateAsync(user), "Profile could not be updated.");
        var client = await db.ClientProfiles.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        var trainer = await db.TrainerProfiles.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (client is not null) client.HeightCm = request.HeightCm;
        if (trainer is not null && request.Specialization is not null) trainer.Specialization = request.Specialization;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await ActiveUserAsync(userId, ct);
        Check(await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword),
            "Password could not be changed. Check the current password and new password requirements.");
        Audit(user.Id, "PasswordChanged");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        // All other cookies are rejected by the security-stamp validation on their next request.
        await signIn.RefreshSignInAsync(user);
    }

    public async Task<IReadOnlyList<AccountSummary>> ListAsync(int page, int size, CancellationToken ct, string? search = null, bool? active = null, string? role = null)
    {
        var query = users.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.FullName.Contains(search) || (x.Email != null && x.Email.Contains(search)));
        if (active.HasValue) query = query.Where(x => x.IsActive == active);
        if (role is not null) query = query.Where(x => db.UserRoles.Any(ur => ur.UserId == x.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == role)));
        return await query.OrderBy(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new AccountSummary(x.Id, x.Email!, x.FullName, x.IsActive)).ToListAsync(ct);
    }

    public async Task SetActiveAsync(string userId, bool active, string actorId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await users.FindByIdAsync(userId) ?? throw RequestFailure.Missing();
        // This workflow is explicitly for Client/Trainer accounts, not Admin removal.
        if (await users.IsInRoleAsync(user, Roles.Admin)) throw RequestFailure.Forbidden();
        user.IsActive = active;
        Check(await users.UpdateSecurityStampAsync(user), "Account status could not be updated.");
        if (!active)
        {
            foreach (var assignment in await db.TrainerClientAssignments.Where(x => x.Status == AssignmentStatus.Active &&
                         (x.ClientUserId == userId || x.TrainerUserId == userId)).ToListAsync(ct))
            {
                assignment.Status = AssignmentStatus.Revoked;
                assignment.RevokedAtUtc = clock.GetUtcNow().UtcDateTime;
            }
        }
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId, SubjectUserId = userId,
            Action = active ? "AccountActivated" : "AccountDeactivated",
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private void Audit(string userId, string action) => db.AuditLogs.Add(new AuditLog
    { ActorUserId = userId, SubjectUserId = userId, Action = action, CreatedAtUtc = clock.GetUtcNow().UtcDateTime });

    private async Task<ApplicationUser> ActiveUserAsync(string id, CancellationToken ct) =>
        await users.Users.SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct) ?? throw RequestFailure.Forbidden();

    internal static void Check(IdentityResult result, string message)
    {
        if (result.Errors.Any(x => x.Code == "ConcurrencyFailure")) throw RequestFailure.Conflict();
        if (!result.Succeeded) throw RequestFailure.Invalid(message);
    }
}
