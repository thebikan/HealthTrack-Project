using System.Security.Claims;
using HealthTrack.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HealthTrack.IntegrationTests;

// Actual Identity managers/events with test stores; no Identity SQL persistence claim.
public sealed class AccountSessionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sign_in_manager_rejects_disabled_accounts(bool active)
    {
        using var users = new TestUsers(new ApplicationUser { IsActive = active });
        var options = Options.Create(new IdentityOptions());
        var manager = new ActiveSignInManager(users, new HttpContextAccessor(),
            new UserClaimsPrincipalFactory<ApplicationUser>(users, options), options,
            NullLogger<SignInManager<ApplicationUser>>.Instance,
            new AuthenticationSchemeProvider(Options.Create(new AuthenticationOptions())),
            new DefaultUserConfirmation<ApplicationUser>());
        Assert.Equal(active, await manager.CanSignInAsync(users.User!));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(null, true)]
    public async Task Cookie_event_checks_stamp_and_rejects_inactive_or_missing_user(bool? active, bool rejected)
    {
        using var users = new TestUsers(active.HasValue ? new ApplicationUser { IsActive = active.Value } : null);
        var stamp = new TestStamp();
        var authentication = new TestAuthentication();
        using var services = new ServiceCollection().AddSingleton<UserManager<ApplicationUser>>(users)
            .AddSingleton<ISecurityStampValidator>(stamp).AddSingleton<IAuthenticationService>(authentication).BuildServiceProvider();
        var context = Context(services);
        await new ActiveAccountCookieEvents().ValidatePrincipal(context);
        Assert.True(stamp.Called);
        Assert.Equal(rejected, context.Principal is null);
        Assert.Equal(rejected, authentication.SignedOut);
    }

    [Fact]
    public async Task Rejected_security_stamp_cannot_be_restored_by_active_user_lookup()
    {
        using var users = new TestUsers(new ApplicationUser());
        using var services = new ServiceCollection().AddSingleton<UserManager<ApplicationUser>>(users)
            .AddSingleton<ISecurityStampValidator>(new TestStamp { Reject = true }).BuildServiceProvider();
        var context = Context(services);
        await new ActiveAccountCookieEvents().ValidatePrincipal(context);
        Assert.Null(context.Principal);
        Assert.Equal(0, users.Lookups);
    }

    private static CookieValidatePrincipalContext Context(IServiceProvider services)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "synthetic")], "test"));
        return new(new DefaultHttpContext { RequestServices = services },
            new AuthenticationScheme(IdentityConstants.ApplicationScheme, null, typeof(CookieAuthenticationHandler)),
            new CookieAuthenticationOptions(), new AuthenticationTicket(principal, IdentityConstants.ApplicationScheme));
    }

    private sealed class TestStamp : ISecurityStampValidator
    {
        public bool Called; public bool Reject;
        public Task ValidateAsync(CookieValidatePrincipalContext context)
        { Called = true; if (Reject) context.RejectPrincipal(); return Task.CompletedTask; }
    }

    private sealed class TestUsers(ApplicationUser? user) : UserManager<ApplicationUser>(new UnusedStore(),
        Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), new PasswordHasher<ApplicationUser>(), [], [],
        new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), new ServiceCollection().BuildServiceProvider(),
        NullLogger<UserManager<ApplicationUser>>.Instance)
    {
        public ApplicationUser? User => user;
        public int Lookups;
        public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal)
        { Lookups++; return Task.FromResult(user); }
    }

    private sealed class UnusedStore : IUserStore<ApplicationUser>
    {
        public void Dispose() { }
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task SetUserNameAsync(ApplicationUser user, string? name, CancellationToken ct) => throw new NotSupportedException();
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? name, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApplicationUser?> FindByNameAsync(string name, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class TestAuthentication : IAuthenticationService
    {
        public bool SignedOut;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        { SignedOut = true; Assert.Equal(IdentityConstants.ApplicationScheme, scheme); return Task.CompletedTask; }
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => throw new NotSupportedException();
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => throw new NotSupportedException();
    }
}
