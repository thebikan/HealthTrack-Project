using HealthTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace HealthTrack.IntegrationTests;

public sealed class MigrationTests
{
    [Fact]
    public void Migration_snapshot_matches_current_model()
    {
        using var db = new HealthTrackDbContextFactory().CreateDbContext([]);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Initial_migration_only_creates_schema_and_contains_no_seeded_credentials()
    {
        using var db = new HealthTrackDbContextFactory().CreateDbContext([]);
        var assembly = db.GetService<IMigrationsAssembly>();
        var entry = Assert.Single(assembly.Migrations, x => x.Key.EndsWith("_InitialCoreSchema", StringComparison.Ordinal));
        var migration = assembly.CreateMigration(entry.Value, db.Database.ProviderName!);
        Assert.All(migration.UpOperations,
            operation => Assert.True(operation is CreateTableOperation or CreateIndexOperation));
        Assert.Equal(22, migration.UpOperations.OfType<CreateTableOperation>().Count());
        Assert.All(migration.UpOperations.OfType<CreateTableOperation>().SelectMany(t => t.ForeignKeys),
            fk => Assert.Equal(ReferentialAction.Restrict, fk.OnDelete));
    }

    [Fact]
    public void Idempotent_sql_can_be_generated_without_any_connection()
    {
        using var db = new HealthTrackDbContextFactory().CreateDbContext([]);
        var sql = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("IF NOT EXISTS", sql);
        Assert.Contains("__EFMigrationsHistory", sql);
        Assert.Contains("CREATE TABLE [AspNetUsers]", sql);
        Assert.Contains("CREATE TABLE [Appointments]", sql);
        Assert.DoesNotContain("DROP TABLE", sql);
        Assert.Null(db.Database.GetConnectionString());
    }
}
