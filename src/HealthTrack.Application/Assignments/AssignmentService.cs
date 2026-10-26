using HealthTrack.Application.Persistence;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Auditing;
using HealthTrack.Domain.Profiles;

namespace HealthTrack.Application.Assignments;

public sealed record AssignRequest(string TrainerUserId, string ClientUserId);
public sealed record AssignmentView(int Id, string TrainerUserId, string ClientUserId, AssignmentStatus Status,
    DateTime AssignedAtUtc, DateTime? RevokedAtUtc, byte[] RowVersion);

public sealed class AssignmentService(IDataSession data, ClientAccess access, IAccountDirectory accounts, TimeProvider clock)
{
    public async Task<int> AssignAsync(AssignRequest request, CancellationToken ct = default)
    {
        await access.AdminAsync(ct);
        var trainer = Input.Text(request.TrainerUserId, 128, "Trainer ID");
        var client = Input.Text(request.ClientUserId, 128, "Client ID");
        if (trainer == client) throw RequestFailure.Invalid("Trainer and Client must be different accounts.");
        return await data.SerializeAsync(["client:" + client], async () =>
        {
            var actor = await access.AdminAsync(ct);
            if (!await accounts.IsActiveInRoleAsync(trainer, Roles.Trainer, ct) ||
                !await accounts.IsActiveInRoleAsync(client, Roles.Client, ct))
                throw RequestFailure.Invalid("Both participants must be active accounts in the required roles.");
            if (await data.AnyAsync(data.Query<TrainerClientAssignment>().Where(x =>
                    x.TrainerUserId == trainer && x.ClientUserId == client && x.Status == AssignmentStatus.Active), ct))
                throw RequestFailure.Conflict();
            var assignment = new TrainerClientAssignment
            {
                TrainerUserId = trainer, ClientUserId = client, AssignedAtUtc = clock.GetUtcNow().UtcDateTime
            };
            data.Add(assignment);
            Audit(actor.UserId, client, "TrainerAssigned");
            await data.SaveAsync(ct);
            return assignment.Id;
        }, ct);
    }

    public async Task RevokeAsync(int id, byte[] rowVersion, CancellationToken ct = default)
    {
        await access.AdminAsync(ct);
        if (rowVersion is not { Length: 8 }) throw RequestFailure.Invalid("The current row version is required.");
        var existing = await data.SingleAsync(data.Query<TrainerClientAssignment>().Where(x => x.Id == id), ct)
            ?? throw RequestFailure.Missing();
        await data.SerializeAsync(["client:" + existing.ClientUserId], async () =>
        {
            var actor = await access.AdminAsync(ct);
            var assignment = await data.SingleAsync(data.Query<TrainerClientAssignment>(true).Where(x => x.Id == id), ct)
                ?? throw RequestFailure.Missing();
            if (!assignment.RowVersion.SequenceEqual(rowVersion) || assignment.Status != AssignmentStatus.Active)
                throw RequestFailure.Conflict();
            assignment.Status = AssignmentStatus.Revoked;
            assignment.RevokedAtUtc = clock.GetUtcNow().UtcDateTime;
            Audit(actor.UserId, assignment.ClientUserId, "TrainerUnassigned");
            await data.SaveAsync(ct);
            return 0;
        }, ct);
    }

    public async Task<IReadOnlyList<AssignmentView>> ListAsync(int page, int size, CancellationToken ct = default)
    {
        var actor = await access.ActorAsync(ct);
        Input.Page(page, size);
        var query = data.Query<TrainerClientAssignment>();
        if (!actor.IsInRole(Roles.Admin))
            query = query.Where(x => x.ClientUserId == actor.UserId || x.TrainerUserId == actor.UserId);
        return await data.ListAsync(query.OrderByDescending(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new AssignmentView(x.Id, x.TrainerUserId, x.ClientUserId, x.Status,
                x.AssignedAtUtc, x.RevokedAtUtc, x.RowVersion)), ct);
    }

    private void Audit(string actor, string client, string action) => data.Add(new AuditLog
    {
        ActorUserId = actor, SubjectUserId = client, Action = action, CreatedAtUtc = clock.GetUtcNow().UtcDateTime
    });
}
