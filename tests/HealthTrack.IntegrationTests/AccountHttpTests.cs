using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HealthTrack.Application.Accounts;
using HealthTrack.Application.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace HealthTrack.IntegrationTests;

// Real HTTP middleware, antiforgery and application service; gateway is a test double.
// Actual Identity persistence/transactions still require SQL Server verification.
public sealed class AccountHttpTests
{
    [Fact]
    public async Task Unexpected_errors_do_not_disclose_exception_details()
    {
        using var factory = new AccountFactory(); factory.Gateway.ThrowOnRegister = true;
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await CsrfAsync(client);
        using var response = await client.PostAsJsonAsync("/api/account/register",ValidRegistration());
        Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);
        Assert.DoesNotContain("private-diagnostic",await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.CacheControl!.NoStore);
    }
    [Fact]
    public async Task Password_recovery_requires_csrf_and_never_returns_a_token()
    {
        using var factory = new AccountFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var denied = await client.PostAsJsonAsync("/api/account/recovery",new RecoveryRequest("synthetic@example.test"));
        Assert.Equal(HttpStatusCode.BadRequest,denied.StatusCode);
        await CsrfAsync(client);
        using var accepted = await client.PostAsJsonAsync("/api/account/recovery",new RecoveryRequest("synthetic@example.test"));
        Assert.Equal(HttpStatusCode.Accepted,accepted.StatusCode);
        var body=await accepted.Content.ReadAsStringAsync();
        Assert.DoesNotContain("token",body,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("synthetic@example.test",body);
        using var reset = await client.PostAsJsonAsync("/api/account/recovery/reset",new ResetPasswordRequest("synthetic@example.test","bad-token","Synthetic42!"));
        Assert.Equal(HttpStatusCode.BadRequest,reset.StatusCode);
    }
    [Fact]
    public async Task Registration_without_csrf_is_rejected_before_gateway()
    {
        using var factory = new AccountFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsJsonAsync("/api/account/register", ValidRegistration());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Gateway.Registrations);
    }

    [Theory]
    [InlineData("Admin", HttpStatusCode.BadRequest, 0)]
    [InlineData("Administrator", HttpStatusCode.BadRequest, 0)]
    [InlineData("Client", HttpStatusCode.Created, 1)]
    [InlineData("Trainer", HttpStatusCode.Created, 1)]
    public async Task Registration_enforces_role_allowlist_with_valid_csrf(string role, HttpStatusCode expected, int calls)
    {
        using var factory = new AccountFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await CsrfAsync(client);
        using var response = await client.PostAsJsonAsync("/api/account/register", ValidRegistration() with { Role = role });
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(calls, factory.Gateway.Registrations);
    }

    [Theory]
    [InlineData("/api/account/me")]
    [InlineData("/api/account/users")]
    [InlineData("/api/notifications")]
    public async Task Anonymous_reads_return_401_without_redirect(string route)
    {
        using var factory = new AccountFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task Rejected_login_returns_generic_401()
    {
        using var factory = new AccountFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await CsrfAsync(client);
        using var response = await client.PostAsJsonAsync("/api/account/login", new LoginRequest("synthetic@example.test", "wrong"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("synthetic@example.test", await response.Content.ReadAsStringAsync());
    }

    private static RegisterRequest ValidRegistration() => new("synthetic@example.test", "Synthetic-Password42!", "Synthetic", "Client");

    private static async Task CsrfAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/account/csrf");
        response.EnsureSuccessStatusCode();
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value =>
            value.Contains("__Host-HealthTrack.Antiforgery") && value.Contains("secure", StringComparison.OrdinalIgnoreCase));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", document.RootElement.GetProperty("token").GetString());
    }

    private sealed class AccountFactory : WebApplicationFactory<Program>
    {
        public RecordingGateway Gateway { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.AddSingleton<IAccountGateway>(Gateway);
            services.AddSingleton<IPasswordRecoveryGateway, RecoveryGateway>();
            services.AddSingleton<ICurrentActor, AnonymousActor>();
        });
    }

    private sealed class AnonymousActor : ICurrentActor
    {
        public Task<Actor?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<Actor?>(null);
    }
    private sealed class RecoveryGateway : IPasswordRecoveryGateway
    {
        public Task RequestAsync(string email,CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ResetAsync(ResetPasswordRequest request,CancellationToken ct) => Task.FromResult(false);
    }

    private sealed class RecordingGateway : IAccountGateway
    {
        public bool ThrowOnRegister { get; set; }
        public int Registrations { get; private set; }
        public Task RegisterAsync(RegisterRequest request, CancellationToken ct)
        { if (ThrowOnRegister) throw new InvalidOperationException("private-diagnostic"); Registrations++; return Task.CompletedTask; }
        public Task<bool> LoginAsync(LoginRequest request, CancellationToken ct) => Task.FromResult(false);
        public Task LogoutAsync() => throw new NotSupportedException();
        public Task<AccountProfile?> GetAsync(string userId, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateProfileAsync(string userId, ProfileUpdate request, CancellationToken ct) => throw new NotSupportedException();
        public Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<AccountSummary>> ListAsync(int page, int size, CancellationToken ct, string? search = null, bool? active = null, string? role = null) => throw new NotSupportedException();
        public Task SetActiveAsync(string userId, bool active, string actorId, CancellationToken ct) => throw new NotSupportedException();
    }
}
