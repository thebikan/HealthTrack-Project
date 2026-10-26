using HealthTrack.Application.Persistence;
using HealthTrack.Domain.Profiles;

namespace HealthTrack.Application.Security;

public sealed class ClientAccess(ICurrentActor current, IDataSession data, IAccountDirectory accounts)
{
    public async Task<Actor> ActorAsync(CancellationToken ct = default) =>
        await current.GetAsync(ct) ?? throw new RequestFailure("unauthenticated", 401, "Authentication required.");

    public async Task<Actor> AdminAsync(CancellationToken ct = default)
    {
        var actor = await ActorAsync(ct);
        return actor.IsInRole(Roles.Admin) ? actor : throw RequestFailure.Forbidden();
    }

    public async Task<Actor> ClientAsync(CancellationToken ct = default)
    {
        var actor = await ActorAsync(ct);
        return actor.IsInRole(Roles.Client) ? actor : throw RequestFailure.Forbidden();
    }

    public async Task<Actor> ReadAsync(string clientId, CancellationToken ct = default)
    {
        var actor = await ActorAsync(ct);
        if (actor.IsInRole(Roles.Client) && actor.UserId == clientId) return actor;
        if (actor.IsInRole(Roles.Trainer) && await AssignedAsync(actor.UserId, clientId, ct)) return actor;
        // Admin has aggregate/report privileges, not automatic personal-health access.
        throw RequestFailure.Forbidden();
    }

    public async Task<Actor> TrainerWriteAsync(string clientId, CancellationToken ct = default)
    {
        var actor = await ActorAsync(ct);
        if (!actor.IsInRole(Roles.Trainer) || !await AssignedAsync(actor.UserId, clientId, ct))
            throw RequestFailure.Forbidden();
        return actor;
    }

    public async Task<bool> AssignedAsync(string trainerId, string clientId, CancellationToken ct = default) =>
        await accounts.IsActiveInRoleAsync(clientId, Roles.Client, ct) &&
        await accounts.IsActiveInRoleAsync(trainerId, Roles.Trainer, ct) &&
        await data.AnyAsync(data.Query<TrainerClientAssignment>().Where(x =>
            x.ClientUserId == clientId && x.TrainerUserId == trainerId && x.Status == AssignmentStatus.Active), ct);
}
