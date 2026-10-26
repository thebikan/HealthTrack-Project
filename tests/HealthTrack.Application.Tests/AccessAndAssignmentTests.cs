using HealthTrack.Application.Assignments;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Auditing;
using HealthTrack.Domain.Profiles;

namespace HealthTrack.Application.Tests;

public sealed class AccessAndAssignmentTests
{
    private readonly TestSession data = new();
    private readonly MutableActor actor = new();
    private readonly TestAccounts accounts = new();
    private ClientAccess Access => new(actor, data, accounts);
    private AssignmentService Service => new(data, Access, accounts, new FixedClock());

    [Fact]
    public async Task Client_cannot_read_another_client_and_admin_has_no_health_override()
    {
        actor.Value = new("client-a", [Roles.Client]);
        Assert.Equal("client-a", (await Access.ReadAsync("client-a")).UserId);
        Assert.Equal(403, (await Assert.ThrowsAsync<RequestFailure>(() => Access.ReadAsync("client-b"))).StatusCode);
        actor.Value = new("admin", [Roles.Admin]);
        Assert.Equal(403, (await Assert.ThrowsAsync<RequestFailure>(() => Access.ReadAsync("client-a"))).StatusCode);
    }

    [Fact]
    public async Task Trainer_role_without_assignment_does_not_grant_access()
    {
        actor.Value = new("trainer-a", [Roles.Trainer]);
        Assert.Equal(403, (await Assert.ThrowsAsync<RequestFailure>(() => Access.ReadAsync("client-a"))).StatusCode);
    }

    [Fact]
    public async Task Revocation_is_effective_on_next_service_check()
    {
        actor.Value = new("admin", [Roles.Admin]);
        var id = await Service.AssignAsync(new("trainer-a", "client-a"));
        actor.Value = new("trainer-a", [Roles.Trainer]);
        await Access.TrainerWriteAsync("client-a");
        actor.Value = new("admin", [Roles.Admin]);
        await Service.RevokeAsync(id, new byte[8]);
        actor.Value = new("trainer-a", [Roles.Trainer]);
        Assert.Equal(403, (await Assert.ThrowsAsync<RequestFailure>(() => Access.TrainerWriteAsync("client-a"))).StatusCode);
        Assert.Equal(2, data.Rows<AuditLog>().Count);
    }

    [Fact]
    public async Task Disabled_client_invalidates_trainer_access()
    {
        actor.Value = new("admin", [Roles.Admin]);
        await Service.AssignAsync(new("trainer-a", "client-a"));
        actor.Value = new("trainer-a", [Roles.Trainer]);
        accounts.Active.Remove(("client-a", Roles.Client));
        Assert.Equal(403, (await Assert.ThrowsAsync<RequestFailure>(() => Access.ReadAsync("client-a"))).StatusCode);
    }

    [Theory]
    [InlineData(Roles.Client)]
    [InlineData(Roles.Trainer)]
    public async Task Assignment_requires_admin_in_service_layer(string role)
    {
        actor.Value = new("user", [role]);
        Assert.Equal(403, (await Assert.ThrowsAsync<RequestFailure>(() => Service.AssignAsync(new("trainer-a", "client-a")))).StatusCode);
        Assert.Empty(data.Rows<TrainerClientAssignment>());
    }

    [Fact]
    public async Task Duplicate_active_assignment_conflicts_and_locks_client_resource()
    {
        actor.Value = new("admin", [Roles.Admin]);
        await Service.AssignAsync(new("trainer-a", "client-a"));
        Assert.Equal(409, (await Assert.ThrowsAsync<RequestFailure>(() => Service.AssignAsync(new("trainer-a", "client-a")))).StatusCode);
        Assert.Single(data.Rows<TrainerClientAssignment>());
        Assert.Contains("client:client-a", data.LockedResources);
    }

    [Fact]
    public async Task Stale_row_version_cannot_revoke_assignment()
    {
        actor.Value = new("admin", [Roles.Admin]);
        var id = await Service.AssignAsync(new("trainer-a", "client-a"));
        Assert.Equal(409, (await Assert.ThrowsAsync<RequestFailure>(() => Service.RevokeAsync(id, [1, 0, 0, 0, 0, 0, 0, 0]))).StatusCode);
        Assert.Equal(AssignmentStatus.Active, data.Rows<TrainerClientAssignment>()[0].Status);
    }

    [Fact]
    public async Task Assignment_listing_is_scoped_to_participants()
    {
        actor.Value = new("admin", [Roles.Admin]);
        await Service.AssignAsync(new("trainer-a", "client-a"));
        await Service.AssignAsync(new("trainer-b", "client-b"));
        actor.Value = new("trainer-a", [Roles.Trainer]);
        Assert.Equal("client-a", Assert.Single(await Service.ListAsync(1, 20)).ClientUserId);
    }
}
