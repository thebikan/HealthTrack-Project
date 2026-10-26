using HealthTrack.Application.Security;
using HealthTrack.Application.Workouts;
using HealthTrack.Domain.Profiles;
using HealthTrack.Domain.Workouts;

namespace HealthTrack.Application.Tests;

public sealed class WorkoutTests
{
    private readonly TestSession data = new();
    private readonly MutableActor actor = new() { Value = new("trainer-a", [Roles.Trainer]) };
    private WorkoutService Service => new(data, new ClientAccess(actor, data, new TestAccounts()), new FixedClock());
    private static WorkoutDraftRequest Draft => new("client-a", "Strength", new(2026, 10, 1), new(2026, 11, 1), [new("Squat", 3, 10, null)]);

    public WorkoutTests() => data.Add(new TrainerClientAssignment { TrainerUserId = "trainer-a", ClientUserId = "client-a" });

    [Fact]
    public async Task Client_sees_only_published_plans()
    {
        var id = await Service.CreateAsync(Draft);
        actor.Value = new("client-a", [Roles.Client]);
        Assert.Empty(await Service.PlansAsync("client-a", 1, 20));
        actor.Value = new("trainer-a", [Roles.Trainer]);
        await Service.PublishAsync(id, "client-a");
        actor.Value = new("client-a", [Roles.Client]);
        Assert.Equal("Squat", Assert.Single(Assert.Single(await Service.PlansAsync("client-a", 1, 20)).Exercises).Name);
    }

    [Fact]
    public async Task Published_plan_cannot_be_edited_so_logged_history_is_stable()
    {
        var id = await Service.CreateAsync(Draft);
        await Service.PublishAsync(id, "client-a");
        Assert.Equal(409, (await Assert.ThrowsAsync<RequestFailure>(() => Service.EditAsync(id, Draft with { Title = "Changed" }))).StatusCode);
        Assert.Equal("Strength", data.Rows<WorkoutPlan>()[0].Title);
    }

    [Fact]
    public async Task Revoked_assignment_blocks_plan_update()
    {
        var id = await Service.CreateAsync(Draft);
        data.Rows<TrainerClientAssignment>()[0].Status = AssignmentStatus.Revoked;
        Assert.Equal(403, (await Assert.ThrowsAsync<RequestFailure>(() => Service.EditAsync(id, Draft))).StatusCode);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(3, -1)]
    public async Task Invalid_exercise_values_are_rejected(int sets, int reps)
    {
        await Assert.ThrowsAsync<RequestFailure>(() => Service.CreateAsync(Draft with { Exercises = [new("Squat", sets, reps, null)] }));
        Assert.Empty(data.Rows<WorkoutPlan>());
    }

    [Fact]
    public async Task Workout_log_uses_current_client_and_rejects_other_clients_plan()
    {
        var id = await Service.CreateAsync(Draft);
        await Service.PublishAsync(id, "client-a");
        actor.Value = new("client-b", [Roles.Client]);
        var request = new WorkoutLogRequest(id, new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc));
        Assert.Equal(404, (await Assert.ThrowsAsync<RequestFailure>(() => Service.LogAsync(request))).StatusCode);
        actor.Value = new("client-a", [Roles.Client]);
        await Service.LogAsync(request);
        Assert.Equal("client-a", Assert.Single(data.Rows<WorkoutLog>()).ClientUserId);
    }

    [Fact]
    public async Task Unassigned_trainer_cannot_list_client_plans()
    {
        actor.Value = new("trainer-b", [Roles.Trainer]);
        Assert.Equal(403, (await Assert.ThrowsAsync<RequestFailure>(() => Service.PlansAsync("client-a", 1, 20))).StatusCode);
    }
}
