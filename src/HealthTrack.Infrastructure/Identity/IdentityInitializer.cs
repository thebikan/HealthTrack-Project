using System.Data;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Auditing;
using HealthTrack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HealthTrack.Infrastructure.Identity;

public sealed class IdentityInitializer(RoleManager<IdentityRole> roles, UserManager<ApplicationUser> users,
    HealthTrackDbContext db, IConfiguration configuration, TimeProvider clock)
{
    // Invoked only by the explicit --initialize-identity command after database approval.
    public async Task InitializeAsync(CancellationToken ct)
    {
        var email = configuration["SeedAdmin:Email"];
        var password = configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) != string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Both SeedAdmin:Email and SeedAdmin:Password must be supplied, or neither.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                IdentityAccountGateway.Check(await roles.CreateAsync(new IdentityRole(role)), "Role initialization failed.");
        }
        if (!string.IsNullOrWhiteSpace(email))
        {
            var existing = await users.FindByEmailAsync(email);
            if (existing is not null)
            {
                if (!await users.IsInRoleAsync(existing, Roles.Admin))
                    throw new InvalidOperationException("Admin initialization cannot promote an existing public account.");
            }
            else
            {
                var user = new ApplicationUser
                {
                    Email = email, UserName = email, FullName = "System Administrator",
                    CreatedAtUtc = clock.GetUtcNow().UtcDateTime
                };
                IdentityAccountGateway.Check(await users.CreateAsync(user, password!), "Admin initialization failed.");
                IdentityAccountGateway.Check(await users.AddToRoleAsync(user, Roles.Admin), "Admin initialization failed.");
                db.AuditLogs.Add(new AuditLog { ActorUserId = user.Id, SubjectUserId = user.Id,
                    Action = "AdminProvisioned", CreatedAtUtc = clock.GetUtcNow().UtcDateTime });
                await db.SaveChangesAsync(ct);
            }
        }
        await transaction.CommitAsync(ct);
    }
}
