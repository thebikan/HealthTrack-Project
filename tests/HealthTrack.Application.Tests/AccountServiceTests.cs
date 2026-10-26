using HealthTrack.Application.Accounts;
using HealthTrack.Application.Security;

namespace HealthTrack.Application.Tests;

public sealed class AccountServiceTests
{
    [Fact]
    public async Task Account_filters_require_admin_and_reject_unknown_roles_or_unbounded_search()
    {
        var client = new AccountService(new StubAccountGateway(),new StubActor(new("client-a",[Roles.Client])));
        await Assert.ThrowsAsync<RequestFailure>(()=>client.ListAsync(1,20,search:"test"));
        var admin = new AccountService(new StubAccountGateway(),new StubActor(new("admin",[Roles.Admin])));
        await Assert.ThrowsAsync<RequestFailure>(()=>admin.ListAsync(1,20,role:"SuperAdmin"));
        await Assert.ThrowsAsync<RequestFailure>(()=>admin.ListAsync(1,20,search:new string('a',201)));
        Assert.Empty(await admin.ListAsync(1,20,role:Roles.Trainer,active:true));
    }
    [Theory]
    [InlineData("Admin")]
    [InlineData("Administrator")]
    [InlineData("client")]
    [InlineData("")]
    public async Task Public_registration_rejects_privilege_escalation_before_store_access(string role)
    {
        var gateway = new StubAccountGateway();
        var service = new AccountService(gateway, new StubActor());
        var error = await Assert.ThrowsAsync<RequestFailure>(() => service.RegisterAsync(
            new RegisterRequest("synthetic@example.test", "Synthetic-Password42!", "Synthetic", role)));
        Assert.Equal(400, error.StatusCode);
        Assert.Null(gateway.Registered);
    }

    [Theory]
    [InlineData("Client")]
    [InlineData("Trainer")]
    public async Task Registration_accepts_only_public_roles_and_normalizes_names(string role)
    {
        var gateway = new StubAccountGateway();
        var service = new AccountService(gateway, new StubActor());
        await service.RegisterAsync(new RegisterRequest(" synthetic@example.test ", "Synthetic-Password42!", " Synthetic ", role));
        Assert.Equal("synthetic@example.test", gateway.Registered!.Email);
        Assert.Equal("Synthetic", gateway.Registered.FullName);
        Assert.Equal(role, gateway.Registered.Role);
    }

    [Theory]
    [InlineData("invalid", "Valid", "Password1!")]
    [InlineData("valid@example.test", " ", "Password1!")]
    [InlineData("valid@example.test", "Valid", "")]
    public async Task Invalid_registration_fields_are_rejected(string email, string name, string password)
    {
        var service = new AccountService(new StubAccountGateway(), new StubActor());
        await Assert.ThrowsAsync<RequestFailure>(() => service.RegisterAsync(new(email, password, name, Roles.Client)));
    }

    [Fact]
    public async Task Invalid_login_has_generic_failure()
    {
        var service = new AccountService(new StubAccountGateway(), new StubActor());
        var error = await Assert.ThrowsAsync<RequestFailure>(() => service.LoginAsync(new("unknown@example.test", "wrong")));
        Assert.Equal(401, error.StatusCode);
        Assert.Equal("Invalid credentials or account unavailable.", error.Message);
    }

    [Fact]
    public async Task Profile_target_is_derived_from_actor_not_request()
    {
        var gateway = new StubAccountGateway();
        var service = new AccountService(gateway, new StubActor(new("client-a", [Roles.Client])));
        await service.UpdateProfileAsync(new("Updated", 180m, null));
        Assert.Equal("client-a", gateway.UpdatedUserId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10000)]
    [InlineData(180.123)]
    public async Task Invalid_height_is_rejected(double height)
    {
        var service = new AccountService(new StubAccountGateway(), new StubActor(new("client", [Roles.Client])));
        await Assert.ThrowsAsync<RequestFailure>(() => service.UpdateProfileAsync(new("Valid", (decimal)height, null)));
    }

    [Fact]
    public async Task Client_cannot_write_trainer_fields()
    {
        var service = new AccountService(new StubAccountGateway(), new StubActor(new("client", [Roles.Client])));
        var error = await Assert.ThrowsAsync<RequestFailure>(() => service.UpdateProfileAsync(new("Valid", null, "Trainer")));
        Assert.Equal(403, error.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Client)]
    [InlineData(Roles.Trainer)]
    public async Task Non_admin_cannot_list_or_disable_accounts(string role)
    {
        var service = new AccountService(new StubAccountGateway(), new StubActor(new("user", [role])));
        Assert.Equal(403, (await Assert.ThrowsAsync<RequestFailure>(() => service.ListAsync(1, 20))).StatusCode);
        Assert.Equal(403, (await Assert.ThrowsAsync<RequestFailure>(() => service.SetActiveAsync("victim", false))).StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_deactivate_self()
    {
        var service = new AccountService(new StubAccountGateway(), new StubActor(new("admin", [Roles.Admin])));
        Assert.Equal(400, (await Assert.ThrowsAsync<RequestFailure>(() => service.SetActiveAsync("admin", false))).StatusCode);
    }

    [Fact]
    public async Task Anonymous_actor_cannot_read_profile()
    {
        var service = new AccountService(new StubAccountGateway(), new StubActor());
        Assert.Equal(401, (await Assert.ThrowsAsync<RequestFailure>(() => service.GetCurrentAsync())).StatusCode);
    }
}

internal sealed class StubActor(Actor? actor = null) : ICurrentActor
{
    public Task<Actor?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(actor);
}

internal sealed class StubAccountGateway : IAccountGateway
{
    public RegisterRequest? Registered { get; private set; }
    public string? UpdatedUserId { get; private set; }
    public Task RegisterAsync(RegisterRequest request, CancellationToken ct) { Registered = request; return Task.CompletedTask; }
    public Task<bool> LoginAsync(LoginRequest request, CancellationToken ct) => Task.FromResult(false);
    public Task LogoutAsync() => Task.CompletedTask;
    public Task<AccountProfile?> GetAsync(string userId, CancellationToken ct) => Task.FromResult<AccountProfile?>(null);
    public Task UpdateProfileAsync(string userId, ProfileUpdate request, CancellationToken ct) { UpdatedUserId = userId; return Task.CompletedTask; }
    public Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<AccountSummary>> ListAsync(int page, int size, CancellationToken ct, string? search = null, bool? active = null, string? role = null) => Task.FromResult<IReadOnlyList<AccountSummary>>([]);
    public Task SetActiveAsync(string userId, bool active, string actorId, CancellationToken ct) => Task.CompletedTask;
}
