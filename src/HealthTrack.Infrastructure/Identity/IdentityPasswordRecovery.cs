using HealthTrack.Application.Accounts;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Auditing;
using HealthTrack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Infrastructure.Identity;

public sealed class UnconfiguredRecoveryDelivery : IPasswordRecoveryDelivery
{
    public bool IsConfigured => false;
    public Task SendAsync(string email, string token, CancellationToken ct) => throw new InvalidOperationException("Recovery delivery is not configured.");
}
public sealed class IdentityPasswordRecovery(UserManager<ApplicationUser> users, IPasswordRecoveryDelivery delivery,
    HealthTrackDbContext db, TimeProvider clock) : IPasswordRecoveryGateway
{
    public async Task RequestAsync(string email, CancellationToken ct)
    {
        // Same response for all addresses when delivery is unavailable; no token sink masquerades as delivery.
        if (!delivery.IsConfigured) throw new RequestFailure("recovery_unavailable", 503, "Password recovery delivery is not configured.");
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByEmailAsync(email);
        if (user is not { IsActive: true }) return;
        var token = await users.GeneratePasswordResetTokenAsync(user);
        await delivery.SendAsync(user.Email!, token, ct);
    }
    public async Task<bool> ResetAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await users.FindByEmailAsync(request.Email);
        if (user is not { IsActive: true }) return false;
        var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded) return false;
        // Identity rotates the security stamp: old reset tokens and login cookies become invalid.
        db.AuditLogs.Add(new AuditLog { ActorUserId = user.Id, SubjectUserId = user.Id,
            Action = "PasswordReset", CreatedAtUtc = clock.GetUtcNow().UtcDateTime });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }
}
