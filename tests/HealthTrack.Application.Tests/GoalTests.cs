using HealthTrack.Application.Health;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Health;
using HealthTrack.Domain.Workouts;
namespace HealthTrack.Application.Tests;
public sealed class GoalTests
{
    [Fact]
    public async Task Goal_page_batches_progress_queries_instead_of_one_query_per_goal()
    {
        for (var i=0;i<20;i++) await Service.CreateAsync(new(GoalType.WorkoutCompletion,5,Start,Start.AddDays(10)));
        var before = data.ListQueries;
        Assert.Equal(20,(await Service.ListAsync("client-a",1,20)).Count);
        Assert.InRange(data.ListQueries-before,1,3);
    }
    private readonly TestSession data = new();
    private readonly MutableActor actor = new() { Value = new("client-a", [Roles.Client]) };
    private readonly FixedClock clock = new();
    private GoalService Service => new(data, new(actor, data, new TestAccounts()), clock);
    private static DateOnly Start => new(2026,10,21);
    [Fact]
    public async Task Weight_goal_uses_recorded_baseline_and_completes_only_at_target()
    {
        data.Add(new HealthMeasurement { ClientUserId = "client-a", WeightKg = 80, RecordedAtUtc = clock.Now.UtcDateTime });
        var id = await Service.CreateAsync(new(GoalType.WeightReduction, 70, Start, Start.AddDays(30)));
        await Assert.ThrowsAsync<RequestFailure>(() => Service.FinishAsync(id,new byte[8],false));
        clock.Now = clock.Now.AddDays(1);
        data.Add(new HealthMeasurement { ClientUserId = "client-a", WeightKg = 70, RecordedAtUtc = clock.Now.UtcDateTime });
        Assert.Equal(100m, Assert.Single(await Service.ListAsync("client-a",1,20)).ProgressPercent);
        await Service.FinishAsync(id,new byte[8],false);
        Assert.Equal(GoalStatus.Completed, data.Rows<FitnessGoal>()[0].Status);
        data.Add(new HealthMeasurement { ClientUserId = "client-a", WeightKg = 90, RecordedAtUtc = clock.Now.UtcDateTime });
        Assert.Equal(70m, Assert.Single(await Service.ListAsync("client-a",1,20)).Current);
        await Assert.ThrowsAsync<RequestFailure>(() => Service.UpdateAsync(id,new(65,Start.AddDays(40),new byte[8])));
    }
    [Fact]
    public async Task Missing_baseline_and_wrong_direction_are_rejected()
    {
        await Assert.ThrowsAsync<RequestFailure>(() => Service.CreateAsync(new(GoalType.WeightReduction,70,Start,Start.AddDays(30))));
        data.Add(new HealthMeasurement { ClientUserId = "client-a", WeightKg = 65, RecordedAtUtc = clock.Now.UtcDateTime });
        await Assert.ThrowsAsync<RequestFailure>(() => Service.CreateAsync(new(GoalType.WeightReduction,70,Start,Start.AddDays(30))));
    }
    [Fact]
    public async Task Consistency_counts_distinct_utc_days_and_not_other_clients_or_future_logs()
    {
        await Service.CreateAsync(new(GoalType.WorkoutConsistency,2,Start,Start.AddDays(1)));
        for(var i=0;i<2;i++) data.Add(new WorkoutLog { ClientUserId = "client-a", CompletedAtUtc = clock.Now.UtcDateTime });
        data.Add(new WorkoutLog { ClientUserId = "client-a", CompletedAtUtc = clock.Now.UtcDateTime.AddDays(2) });
        data.Add(new WorkoutLog { ClientUserId = "client-b", CompletedAtUtc = clock.Now.UtcDateTime });
        Assert.Equal(50m, Assert.Single(await Service.ListAsync("client-a",1,20)).ProgressPercent);
    }
    [Fact]
    public async Task Goal_update_and_cancel_are_owner_only_and_reject_stale_versions()
    {
        var id = await Service.CreateAsync(new(GoalType.WorkoutCompletion,5,Start,Start.AddDays(7)));
        await Assert.ThrowsAsync<RequestFailure>(() => Service.UpdateAsync(id,new(6,Start.AddDays(8),[1,0,0,0,0,0,0,0])));
        actor.Value = new("client-b",[Roles.Client]);
        await Assert.ThrowsAsync<RequestFailure>(() => Service.UpdateAsync(id,new(6,Start.AddDays(8),new byte[8])));
        await Assert.ThrowsAsync<RequestFailure>(() => Service.ListAsync("client-a",1,20));
        actor.Value = new("client-a",[Roles.Client]);
        await Service.UpdateAsync(id,new(6,Start.AddDays(8),new byte[8]));
        await Service.FinishAsync(id,new byte[8],true);
        Assert.Equal(GoalStatus.Cancelled,data.Rows<FitnessGoal>()[0].Status);
    }
    [Theory]
    [InlineData(0)] [InlineData(1.5)] [InlineData(3)]
    public async Task Malformed_or_unreachable_consistency_targets_are_rejected(decimal target) =>
        await Assert.ThrowsAsync<RequestFailure>(() => Service.CreateAsync(new(GoalType.WorkoutConsistency,target,Start,Start.AddDays(1))));
    [Fact]
    public async Task Completion_goal_counts_sessions_through_target_day_but_not_after_deadline()
    {
        await Service.CreateAsync(new(GoalType.WorkoutCompletion,2,Start,Start));
        data.Add(new WorkoutLog { ClientUserId="client-a",CompletedAtUtc=clock.Now.UtcDateTime });
        data.Add(new WorkoutLog { ClientUserId="client-a",CompletedAtUtc=clock.Now.UtcDateTime.AddHours(23) });
        data.Add(new WorkoutLog { ClientUserId="client-a",CompletedAtUtc=clock.Now.UtcDateTime.AddDays(1) });
        clock.Now=clock.Now.AddDays(2);
        var view=Assert.Single(await Service.ListAsync("client-a",1,20));
        Assert.Equal(2m,view.Current); Assert.Equal(100m,view.ProgressPercent);
    }
}
