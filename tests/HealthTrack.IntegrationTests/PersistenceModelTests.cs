using HealthTrack.Domain.Appointments;
using HealthTrack.Domain.Health;
using HealthTrack.Domain.Notifications;
using HealthTrack.Domain.Profiles;
using HealthTrack.Domain.Workouts;
using HealthTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HealthTrack.IntegrationTests;

// These tests use SQL Server model/DDL generation only. No database is opened.
public sealed class PersistenceModelTests
{
    [Fact]
    public void Model_uses_sql_server_and_never_cascades_deletions()
    {
        using var db = new HealthTrackDbContextFactory().CreateDbContext([]);
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", db.Database.ProviderName);
        Assert.Null(db.Database.GetConnectionString());
        Assert.Equal(28, db.Model.GetEntityTypes().Count());
        Assert.All(db.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()),
            fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }

    [Fact]
    public void Every_core_string_is_bounded_and_no_accidental_shadow_columns_exist()
    {
        using var db = new HealthTrackDbContextFactory().CreateDbContext([]);
        foreach (var entity in db.Model.GetEntityTypes().Where(t => t.ClrType.Namespace!.StartsWith("HealthTrack.Domain")))
        {
            Assert.DoesNotContain(entity.GetProperties(), p => p.IsShadowProperty());
            Assert.All(entity.GetProperties().Where(p => p.ClrType == typeof(string)),
                p => Assert.InRange(p.GetMaxLength() ?? 0, 1, 2000));
        }
    }

    [Fact]
    public void Workout_log_plan_reference_includes_owner_to_prevent_cross_client_links()
    {
        using var db = new HealthTrackDbContextFactory().CreateDbContext([]);
        var log = db.Model.FindEntityType(typeof(WorkoutLog))!;
        var fk = Assert.Single(log.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(WorkoutPlan));
        Assert.Equal(new[] { "WorkoutPlanId", "ClientUserId" }, fk.Properties.Select(p => p.Name));
        Assert.Equal(new[] { "Id", "ClientUserId" }, fk.PrincipalKey.Properties.Select(p => p.Name));
    }

    [Theory]
    [InlineData(typeof(Appointment))]
    [InlineData(typeof(TrainerClientAssignment))]
    [InlineData(typeof(NotificationSchedule))]
    public void Concurrent_updates_use_database_generated_rowversion(Type type)
    {
        using var db = new HealthTrackDbContextFactory().CreateDbContext([]);
        var property = db.Model.FindEntityType(type)!.FindProperty("RowVersion")!;
        Assert.True(property.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
        Assert.Equal("rowversion", property.GetColumnType());
    }

    [Fact]
    public void Assignment_and_notification_deduplication_have_filtered_unique_indexes()
    {
        using var db = new HealthTrackDbContextFactory().CreateDbContext([]);
        var assignment = db.Model.FindEntityType(typeof(TrainerClientAssignment))!;
        Assert.Contains(assignment.GetIndexes(), i => i.IsUnique && i.GetFilter() == "[Status] = N'Active'");
        var notification = db.Model.FindEntityType(typeof(Notification))!;
        Assert.Contains(notification.GetIndexes(), i => i.IsUnique && i.GetFilter() == "[DeliveryKey] IS NOT NULL");
    }

    [Fact]
    public void Sql_server_ddl_contains_health_booking_and_calorie_constraints()
    {
        using var db = new HealthTrackDbContextFactory().CreateDbContext([]);
        var sql = db.Database.GenerateCreateScript();
        Assert.Contains("CHECK ([WeightKg] > 0)", sql);
        Assert.Contains("CHECK ([Calories] >= 0)", sql);
        Assert.Contains("CHECK ([EndAtUtc] > [StartAtUtc])", sql);
        Assert.Contains("decimal(6,2)", sql);
        Assert.Contains("datetime2", sql);
        Assert.DoesNotContain("ON DELETE CASCADE", sql);
    }

    [Fact]
    public void Measurement_precision_and_index_support_dated_client_history()
    {
        using var db = new HealthTrackDbContextFactory().CreateDbContext([]);
        var entity = db.Model.FindEntityType(typeof(HealthMeasurement))!;
        Assert.Equal(6, entity.FindProperty("WeightKg")!.GetPrecision());
        Assert.Equal(2, entity.FindProperty("WeightKg")!.GetScale());
        Assert.Contains(entity.GetIndexes(),
            i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "ClientUserId", "RecordedAtUtc" }));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public async Task Saving_non_utc_events_is_rejected_before_database_access(DateTimeKind kind)
    {
        using var db = new HealthTrackDbContextFactory().CreateDbContext([]);
        db.HealthMeasurements.Add(new HealthMeasurement
        {
            ClientUserId = "synthetic-client", WeightKg = 70,
            RecordedAtUtc = new DateTime(2026, 1, 1, 12, 0, 0, kind)
        });
        var sync = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        var asyncError = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal("Persisted event timestamps must use UTC.", sync.Message);
        Assert.Equal(sync.Message, asyncError.Message);
    }

    [Fact]
    public void Reading_sql_datetime2_restores_utc_kind_without_changing_ticks()
    {
        using var db = new HealthTrackDbContextFactory().CreateDbContext([]);
        var property = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(HealthMeasurement))!
            .FindProperty("RecordedAtUtc")!;
        var stored = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);
        var converted = (DateTime)property.GetValueConverter()!.ConvertFromProvider(stored)!;
        Assert.Equal(DateTimeKind.Utc, converted.Kind);
        Assert.Equal(stored.Ticks, converted.Ticks);
    }
}
