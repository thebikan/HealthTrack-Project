using HealthTrack.Application.Health;
using HealthTrack.Application.Nutrition;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Health;
using HealthTrack.Domain.Nutrition;
using HealthTrack.Domain.Profiles;

namespace HealthTrack.Application.Tests;

public sealed class NutritionAndHealthTests
{
    [Fact]
    public async Task Diet_edit_updates_plan_but_not_consumed_meals_and_requires_author()
    {
        await Nutrition.LogAsync(new("Original consumed meal", 400, When));
        actor.Value = new("trainer-a", [Roles.Trainer]);
        var request = new DietPlanRequest("client-a","Plan",new(2026,10,1),new(2026,10,31),[new("Lunch","Lunch",500,new TimeOnly(12,0))]);
        var id = await Nutrition.AssignPlanAsync(request);
        await Nutrition.EditAsync(id,request with { Meals = [new("Dinner","Dinner",600,new TimeOnly(18,0))] });
        Assert.Equal(400m, Assert.Single(data.Rows<MealLog>()).Calories);
        Assert.Equal(new TimeOnly(18,0),Assert.Single(data.Rows<DietPlanMeal>()).PlannedTimeUtc);
        data.Add(new TrainerClientAssignment { ClientUserId = "client-a", TrainerUserId = "trainer-b" });
        actor.Value = new("trainer-b",[Roles.Trainer]);
        await Assert.ThrowsAsync<RequestFailure>(() => Nutrition.EditAsync(id,request));
        actor.Value = new("trainer-a",[Roles.Trainer]);
        data.Rows<TrainerClientAssignment>()[0].Status = AssignmentStatus.Revoked;
        await Assert.ThrowsAsync<RequestFailure>(() => Nutrition.EditAsync(id,request));
    }
    private readonly TestSession data = new();
    private readonly MutableActor actor = new() { Value = new("client-a", [Roles.Client]) };
    private ClientAccess Access => new(actor, data, new TestAccounts());
    private NutritionService Nutrition => new(data, Access, new FixedClock());
    private HealthService Health => new(data, Access, new FixedClock());
    private static DateTime When => new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    public NutritionAndHealthTests()
    {
        data.Add(new ClientProfile { UserId = "client-a", HeightCm = 175 });
        data.Add(new ClientProfile { UserId = "client-b" });
        data.Add(new TrainerClientAssignment { ClientUserId = "client-a", TrainerUserId = "trainer-a" });
    }

    [Fact]
    public async Task Planned_meals_do_not_create_consumption_logs()
    {
        actor.Value = new("trainer-a", [Roles.Trainer]);
        await Nutrition.AssignPlanAsync(new("client-a", "Balanced", new(2026, 10, 1), new(2026, 10, 31), [new("Lunch", "Lunch", 500)]));
        Assert.Single(data.Rows<DietPlanMeal>());
        Assert.Empty(data.Rows<MealLog>());
        actor.Value = new("client-a", [Roles.Client]);
        Assert.Equal(500m, Assert.Single(Assert.Single(await Nutrition.PlansAsync("client-a", 1, 20)).Meals).Calories);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1.234)]
    [InlineData(100000000)]
    public async Task Invalid_calories_cannot_be_logged(double calories)
    {
        await Assert.ThrowsAsync<RequestFailure>(() => Nutrition.LogAsync(new("Meal", (decimal)calories, When)));
        Assert.Empty(data.Rows<MealLog>());
    }

    [Fact]
    public async Task Meal_owner_is_current_client_and_history_is_private()
    {
        await Nutrition.LogAsync(new("Meal", 400, When));
        Assert.Equal("client-a", Assert.Single(data.Rows<MealLog>()).ClientUserId);
        actor.Value = new("client-b", [Roles.Client]);
        Assert.Equal(403, (await Assert.ThrowsAsync<RequestFailure>(() => Nutrition.LogsAsync("client-a", When.AddDays(-1), When.AddDays(1), 1, 20))).StatusCode);
    }

    [Fact]
    public async Task Height_is_captured_at_measurement_time_for_stable_bmi()
    {
        await Health.RecordAsync(new(70, null, When));
        data.Rows<ClientProfile>()[0].HeightCm = 200;
        var result = Assert.Single(await Health.HistoryAsync("client-a", When.AddDays(-1), When.AddDays(1), 1, 20));
        Assert.Equal(175m, result.HeightCm);
        Assert.Equal(22.86m, result.Bmi);
    }

    [Fact]
    public async Task No_height_means_no_bmi()
    {
        actor.Value = new("client-b", [Roles.Client]);
        await Health.RecordAsync(new(70, null, When));
        Assert.Null(Assert.Single(await Health.HistoryAsync("client-b", When.AddDays(-1), When.AddDays(1), 1, 20)).Bmi);
    }

    [Theory]
    [InlineData(0, 175)]
    [InlineData(70, 0)]
    [InlineData(-1, 175)]
    public async Task Invalid_measurements_fail_before_save(double weight, double height)
    {
        await Assert.ThrowsAsync<RequestFailure>(() => Health.RecordAsync(new((decimal)weight, (decimal)height, When)));
        Assert.Empty(data.Rows<HealthMeasurement>());
    }

    [Fact]
    public async Task Trainer_feedback_requires_current_assignment()
    {
        actor.Value = new("trainer-a", [Roles.Trainer]);
        await Health.AddFeedbackAsync(new("client-a", "Synthetic feedback"));
        data.Rows<TrainerClientAssignment>()[0].Status = AssignmentStatus.Revoked;
        Assert.Equal(403, (await Assert.ThrowsAsync<RequestFailure>(() => Health.AddFeedbackAsync(new("client-a", "Rejected")))).StatusCode);
        Assert.Single(data.Rows<TrainerFeedback>());
    }
}
