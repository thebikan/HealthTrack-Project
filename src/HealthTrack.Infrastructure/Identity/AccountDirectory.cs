using HealthTrack.Application.Persistence;
using Microsoft.AspNetCore.Identity;
using HealthTrack.Application.Security;
using HealthTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Infrastructure.Identity;

public sealed class AccountDirectory(HealthTrackDbContext db) : IAccountDirectory
{
    public async Task<AccountTotals> TotalsAsync(CancellationToken ct = default)
    {
        var clients = db.UserRoles.Join(db.Roles.Where(x => x.Name == Roles.Client), ur => ur.RoleId, r => r.Id, (ur, r) => ur.UserId);
        var trainers = db.UserRoles.Join(db.Roles.Where(x => x.Name == Roles.Trainer), ur => ur.RoleId, r => r.Id, (ur, r) => ur.UserId);
        return new AccountTotals(await db.Users.CountAsync(ct),
            await db.Users.CountAsync(x => x.IsActive && clients.Contains(x.Id), ct),
            await db.Users.CountAsync(x => x.IsActive && trainers.Contains(x.Id), ct),
            await db.Users.CountAsync(x => !x.IsActive, ct));
    }
    public Task<bool> IsActiveInRoleAsync(string userId, string role, CancellationToken ct = default) =>
        db.Users.AnyAsync(user => user.Id == userId && user.IsActive && db.UserRoles.Any(ur => ur.UserId == user.Id &&
            db.Roles.Any(r => r.Id == ur.RoleId && r.Name == role)), ct);
}
