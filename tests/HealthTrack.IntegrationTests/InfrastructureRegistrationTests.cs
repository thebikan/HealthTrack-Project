using HealthTrack.Infrastructure;
using HealthTrack.Infrastructure.Identity;
using HealthTrack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HealthTrack.IntegrationTests;

public sealed class InfrastructureRegistrationTests
{
    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthTrackInfrastructure(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Missing_database_configuration_fails_without_fallback_or_credentials()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var error = Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<HealthTrackDbContext>());
        Assert.Equal(
            "Configure ConnectionStrings:DefaultConnection using environment variables or development user-secrets.",
            error.Message);
    }

    [Fact]
    public void Identity_password_lockout_and_cookie_options_are_secure()
    {
        using var provider = CreateProvider();
        var identity = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;
        Assert.True(identity.User.RequireUniqueEmail);
        Assert.Equal(12, identity.Password.RequiredLength);
        Assert.True(identity.Password.RequireDigit);
        Assert.True(identity.Password.RequireUppercase);
        Assert.True(identity.Password.RequireLowercase);
        Assert.True(identity.Password.RequireNonAlphanumeric);
        Assert.Equal(5, identity.Lockout.MaxFailedAccessAttempts);
        var cookie = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        Assert.Equal(CookieSecurePolicy.Always, cookie.Cookie.SecurePolicy);
        Assert.True(cookie.Cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Lax, cookie.Cookie.SameSite);
        Assert.Equal("__Host-HealthTrack", cookie.Cookie.Name);
        Assert.Null(cookie.Cookie.Domain);
        Assert.Equal("/", cookie.Cookie.Path);
        Assert.Equal(typeof(ActiveAccountCookieEvents), cookie.EventsType);
        Assert.Equal(TimeSpan.Zero, provider.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value.ValidationInterval);
    }

    [Fact]
    public void Identity_uses_salted_hashes_and_rejects_incorrect_passwords()
    {
        using var provider = CreateProvider();
        var hasher = provider.GetRequiredService<IPasswordHasher<ApplicationUser>>();
        var user = new ApplicationUser { FullName = "Synthetic Test User", CreatedAtUtc = DateTime.UtcNow };
        const string password = "Synthetic-Test-Password-42!";
        var first = hasher.HashPassword(user, password);
        var second = hasher.HashPassword(user, password);
        Assert.NotEqual(first, second);
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, first, password));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, first, "incorrect"));
    }
}
