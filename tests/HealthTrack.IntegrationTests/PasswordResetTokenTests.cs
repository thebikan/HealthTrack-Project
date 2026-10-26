using HealthTrack.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HealthTrack.IntegrationTests;

// Real Identity token provider/UserManager; test store only. No SQL claim.
public sealed class PasswordResetTokenTests
{
    private static UserManager<ApplicationUser> Manager(TimeSpan lifetime)
    {
        var manager = new UserManager<ApplicationUser>(new Store(), Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(), [], [], new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(), new ServiceCollection().BuildServiceProvider(), NullLogger<UserManager<ApplicationUser>>.Instance);
        manager.RegisterTokenProvider(TokenOptions.DefaultProvider, new DataProtectorTokenProvider<ApplicationUser>(
            new EphemeralDataProtectionProvider(), Options.Create(new DataProtectionTokenProviderOptions { TokenLifespan = lifetime }),
            NullLogger<DataProtectorTokenProvider<ApplicationUser>>.Instance));
        return manager;
    }
    private static ApplicationUser User() => new() { Id = Guid.NewGuid().ToString(), UserName = "Synthetic", SecurityStamp = Guid.NewGuid().ToString() };
    [Fact]
    public async Task Reset_changes_password_and_invalidates_token_and_prior_stamp()
    {
        using var manager = Manager(TimeSpan.FromHours(1)); var user = User();
        var stamp = user.SecurityStamp;
        var token = await manager.GeneratePasswordResetTokenAsync(user);
        Assert.True((await manager.ResetPasswordAsync(user, token, "Synthetic-New-Password42!")).Succeeded);
        Assert.NotEqual(stamp, user.SecurityStamp);
        Assert.True(await manager.CheckPasswordAsync(user, "Synthetic-New-Password42!"));
        Assert.False((await manager.ResetPasswordAsync(user, token, "Synthetic-Other42!")).Succeeded);
    }
    [Fact]
    public async Task Token_is_bound_to_user_and_purpose_and_rejects_tampering()
    {
        using var manager = Manager(TimeSpan.FromHours(1)); var user = User();
        var token = await manager.GeneratePasswordResetTokenAsync(user);
        Assert.False((await manager.ResetPasswordAsync(User(), token, "Synthetic42!")).Succeeded);
        Assert.False((await manager.ResetPasswordAsync(user, "tampered", "Synthetic42!")).Succeeded);
        Assert.False(await manager.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, "OtherPurpose", token));
    }
    [Fact]
    public async Task Expired_tokens_are_rejected()
    {
        using var manager = Manager(TimeSpan.FromSeconds(-1)); var user = User();
        var token = await manager.GeneratePasswordResetTokenAsync(user);
        Assert.False((await manager.ResetPasswordAsync(user, token, "Synthetic42!")).Succeeded);
    }
    private sealed class Store : IUserPasswordStore<ApplicationUser>, IUserSecurityStampStore<ApplicationUser>
    {
        public void Dispose() { }
        public Task<string> GetUserIdAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.Id);
        public Task<string?> GetUserNameAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.UserName);
        public Task SetUserNameAsync(ApplicationUser u, string? n, CancellationToken ct) { u.UserName = n; return Task.CompletedTask; }
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser u, string? n, CancellationToken ct) { u.NormalizedUserName = n; return Task.CompletedTask; }
        public Task<IdentityResult> CreateAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> UpdateAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(ApplicationUser u, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApplicationUser?> FindByNameAsync(string name, CancellationToken ct) => throw new NotSupportedException();
        public Task SetPasswordHashAsync(ApplicationUser u, string? hash, CancellationToken ct) { u.PasswordHash = hash; return Task.CompletedTask; }
        public Task<string?> GetPasswordHashAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.PasswordHash);
        public Task<bool> HasPasswordAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.PasswordHash is not null);
        public Task SetSecurityStampAsync(ApplicationUser u, string stamp, CancellationToken ct) { u.SecurityStamp = stamp; return Task.CompletedTask; }
        public Task<string?> GetSecurityStampAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.SecurityStamp);
    }
}
