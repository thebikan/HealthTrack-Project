using System.Text.Json;
using HealthTrack.Application.Reporting;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Health;
using HealthTrack.Domain.Nutrition;
using HealthTrack.Domain.Profiles;
using HealthTrack.Domain.Workouts;

namespace HealthTrack.Application.Tests;

public sealed class ReportingTests
{
    private readonly TestSession data = new();
    private readonly MutableActor actor = new() { Value = new("client-a", [Roles.Client]) };
    private readonly TestAccounts accounts = new();
    private ReportingService Service => new(data, new ClientAccess(actor, data, accounts), accounts, new FixedClock());
    private static DateTime From => new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);
    private static DateTime To => From.AddDays(1);

    [Fact]
    public async Task Empty_client_dashboard_has_no_invented_metrics()
    {
        var result = await Service.ClientAsync();
        Assert.Null(result.LatestMeasurement);
        Assert.Null(result.NextAppointment);
        Assert.Equal(0, result.WorkoutsLast30Days);
    }

    [Fact]
    public async Task Progress_aggregates_only_owned_records_with_exclusive_end_date()
    {
        data.Add(new MealLog { ClientUserId = "client-a", Calories = 300, ConsumedAtUtc = From.AddHours(1) });
        data.Add(new MealLog { ClientUserId = "client-a", Calories = 400, ConsumedAtUtc = From.AddHours(2) });
        data.Add(new MealLog { ClientUserId = "client-a", Calories = 999, ConsumedAtUtc = To });
        data.Add(new MealLog { ClientUserId = "client-b", Calories = 999, ConsumedAtUtc = From });
        data.Add(new HealthMeasurement { ClientUserId = "client-a", WeightKg = 70, RecordedAtUtc = From });
        data.Add(new HealthMeasurement { ClientUserId = "client-a", WeightKg = 72, RecordedAtUtc = From.AddHours(1) });
        var result = await Service.ProgressAsync("client-a", From, To);
        Assert.Equal(700m, Assert.Single(result.Calories).RecordedCalories);
        Assert.Equal(71m, Assert.Single(result.Weight).AverageWeightKg);
        Assert.Null(result.CurrentPlanCoveragePercent);
    }

    [Fact]
    public async Task Coverage_counts_distinct_logged_plans_not_duplicate_logs()
    {
        for (var i = 0; i < 2; i++) data.Add(new WorkoutPlan
        {
            ClientUserId = "client-a", TrainerUserId = "trainer-a", Title = "Synthetic", Status = PlanStatus.Published,
            StartDate = new(2026, 10, 1), EndDate = new(2026, 10, 31)
        });
        for (var i = 0; i < 3; i++) data.Add(new WorkoutLog { ClientUserId = "client-a", WorkoutPlanId = 1, CompletedAtUtc = From.AddHours(i) });
        var result = await Service.ProgressAsync("client-a", From, To);
        Assert.Equal(3, Assert.Single(result.Workouts).CompletedWorkouts);
        Assert.Equal(1, result.PlansWithLogs);
        Assert.Equal(50m, result.CurrentPlanCoveragePercent);
    }

    [Fact]
    public async Task Trainer_dashboard_excludes_unassigned_clients()
    {
        data.Add(new ClientProfile { UserId = "client-a" });
        data.Add(new ClientProfile { UserId = "client-b" });
        data.Add(new TrainerClientAssignment { ClientUserId = "client-a", TrainerUserId = "trainer-a" });
        actor.Value = new("trainer-a", [Roles.Trainer]);
        Assert.Equal("client-a", Assert.Single((await Service.TrainerAsync(1, 20)).Clients).ClientUserId);
        data.Rows<TrainerClientAssignment>()[0].Status = AssignmentStatus.Revoked;
        Assert.Empty((await Service.TrainerAsync(1, 20)).Clients);
    }

    [Fact]
    public async Task Admin_gets_counts_but_cannot_read_personal_progress()
    {
        data.Add(new HealthMeasurement { ClientUserId = "client-a", WeightKg = 73.45m, RecordedAtUtc = From });
        actor.Value = new("admin", [Roles.Admin]);
        var result = await Service.AdminAsync(From, To);
        Assert.Equal(1, result.Measurements);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("73.45", json);
        Assert.DoesNotContain("client-a", json);
        Assert.Equal(403, (await Assert.ThrowsAsync<RequestFailure>(() => Service.ProgressAsync("client-a", From, To))).StatusCode);
    }
}
