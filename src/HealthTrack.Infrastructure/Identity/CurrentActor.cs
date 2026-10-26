using HealthTrack.Application.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Infrastructure.Identity;

public sealed class CurrentActor(IHttpContextAccessor accessor, UserManager<ApplicationUser> users) : ICurrentActor
{
    public async Task<Actor?> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var principal = accessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true) return null;
        var id = users.GetUserId(principal);
        var user = await users.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);
        if (user is not { IsActive: true }) return null;
        return new Actor(user.Id, (await users.GetRolesAsync(user)).ToArray());
    }
}
