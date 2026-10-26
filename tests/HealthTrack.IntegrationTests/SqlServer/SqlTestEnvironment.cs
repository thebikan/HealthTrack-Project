using HealthTrack.Application.Accounts;
using HealthTrack.Application.Security;
using HealthTrack.Infrastructure;
using HealthTrack.Infrastructure.Identity;
using HealthTrack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HealthTrack.IntegrationTests.SqlServer;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("HEALTHTRACK_SQL_TESTS") != "I_APPROVE_SYNTHETIC_WRITES")
            Skip = "Live SQL Server tests require explicit approval and dedicated database configuration.";
    }
}

internal static class SqlTestGuard
{
    public static string Validate(string? connection, string? expectedDatabase, string? approval)
    {
        if (approval != "I_APPROVE_SYNTHETIC_WRITES") throw new InvalidOperationException("SQL test writes are not approved.");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("SqlIntegration connection is missing.");
        SqlConnectionStringBuilder settings;
        try { settings = new(connection); }
        catch (ArgumentException) { throw new InvalidOperationException("Invalid SQL test connection configuration."); }
        if (string.IsNullOrWhiteSpace(expectedDatabase) || !expectedDatabase.StartsWith("HealthTrack_Integration_", StringComparison.Ordinal) ||
            settings.InitialCatalog != expectedDatabase || string.IsNullOrWhiteSpace(settings.DataSource) ||
            !string.IsNullOrEmpty(settings.AttachDBFilename) || !string.IsNullOrEmpty(settings.FailoverPartner))
            throw new InvalidOperationException("Use the explicitly approved dedicated HealthTrack_Integration_ database.");
        if (settings.TrustServerCertificate || settings.Encrypt == SqlConnectionEncryptOption.Optional || settings.PersistSecurityInfo)
            throw new InvalidOperationException("SQL tests require verified TLS and Persist Security Info=False.");
        settings.ConnectTimeout = 10;
        settings.ApplicationName = "HealthTrack.SqlIntegration";
        return settings.ConnectionString;
    }
}

// Per-test users have random IDs/emails. No cleanup, database creation or migrations.
public abstract class SqlTestEnvironment : IAsyncLifetime
{
    private ServiceProvider? provider;
    protected string ClientId { get; private set; } = "";
    protected string OtherClientId { get; private set; } = "";
    protected string TrainerId { get; private set; } = "";
    protected string AdminId { get; private set; } = "";
    protected SqlClock Clock { get; } = new();

    public async Task InitializeAsync()
    {
        var config = new ConfigurationBuilder().AddUserSecrets<SqlTestEnvironment>(optional: true).AddEnvironmentVariables().Build();
        var expected = Environment.GetEnvironmentVariable("HEALTHTRACK_SQL_TEST_DATABASE");
        var connection = SqlTestGuard.Validate(config.GetConnectionString("SqlIntegration"), expected,
            Environment.GetEnvironmentVariable("HEALTHTRACK_SQL_TESTS"));
        var options = new DbContextOptionsBuilder<HealthTrackDbContext>().UseSqlServer(connection).Options;
        await using (var db = new HealthTrackDbContext(options))
        {
            await db.Database.OpenConnectionAsync();
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT DB_NAME()";
            Assert.Equal(expected, await command.ExecuteScalarAsync());
            command.CommandText = "SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME()";
            Assert.True(Convert.ToInt32(await command.ExecuteScalarAsync()) >= 130, "Database compatibility must be at least 130.");
            var applied = (await db.Database.GetAppliedMigrationsAsync()).Order().ToArray();
            Assert.Equal(db.Database.GetMigrations().Order().ToArray(), applied);
        }
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddHealthTrackInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:DefaultConnection"] = connection }).Build());
        services.AddSingleton<TimeProvider>(Clock);
        services.AddScoped<SqlActor>();
        services.AddScoped<ICurrentActor>(sp => sp.GetRequiredService<SqlActor>());
        provider = services.BuildServiceProvider();
        await Use(null, sp => sp.GetRequiredService<IdentityInitializer>().InitializeAsync(default));
        ClientId = await Register(Roles.Client);
        OtherClientId = await Register(Roles.Client);
        TrainerId = await Register(Roles.Trainer);
        AdminId = await Use(null, async sp =>
        {
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = Guid.NewGuid() + "@example.test", FullName = "Synthetic SQL Admin", CreatedAtUtc = Clock.GetUtcNow().UtcDateTime };
            user.Email = user.UserName;
            Assert.True((await users.CreateAsync(user, Password())).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, Roles.Admin)).Succeeded);
            return user.Id;
        });
    }

    protected Task<T> Use<T>(Actor? actor, Func<IServiceProvider, Task<T>> action) => Scoped(actor, action);
    protected async Task Use(Actor? actor, Func<IServiceProvider, Task> action) =>
        await Scoped(actor, async sp => { await action(sp); return 0; });
    private async Task<T> Scoped<T>(Actor? actor, Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = provider!.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SqlActor>().Value = actor;
        return await action(scope.ServiceProvider);
    }
    protected Task<string> Register(string role) => Use(null, async sp =>
    {
        var email = Guid.NewGuid() + "@example.test";
        await sp.GetRequiredService<AccountService>().RegisterAsync(new(email, Password(), "Synthetic SQL User", role));
        return (await sp.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email))!.Id;
    });
    private static string Password() => "Synthetic-aA1!" + Guid.NewGuid().ToString("N");
    protected Actor Client => new(ClientId, [Roles.Client]);
    protected Actor Trainer => new(TrainerId, [Roles.Trainer]);
    protected Actor Admin => new(AdminId, [Roles.Admin]);
    public async Task DisposeAsync() { if (provider is not null) await provider.DisposeAsync(); }
    private sealed class SqlActor : ICurrentActor
    {
        public Actor? Value { get; set; }
        public Task<Actor?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);
    }
    protected sealed class SqlClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
