using HealthTrack.Application.Security;
using HealthTrack.Application.Workouts;
using HealthTrack.Domain.Profiles;
using HealthTrack.Domain.Workouts;

namespace HealthTrack.Application.Tests;
public sealed class WorkoutExtensionTests
{
    private readonly TestSession data = new();
    private readonly MutableActor actor = new() { Value = new("trainer-a", [Roles.Trainer]) };
    private WorkoutService Service => new(data, new(actor, data, new TestAccounts()), new FixedClock());
    private static WorkoutDraftRequest Draft => new("client-a", "Original", new(2026,10,1), new(2026,11,1), [new("Walk", null, null, 20)]);
    public WorkoutExtensionTests() => data.Add(new TrainerClientAssignment { ClientUserId = "client-a", TrainerUserId = "trainer-a" });
    [Fact]
    public async Task Revision_preserves_published_content_and_log_reference()
    {
        var original = await Service.CreateAsync(Draft); await Service.PublishAsync(original, "client-a");
        actor.Value = new("client-a", [Roles.Client]);
        await Service.LogAsync(new(original, new(2026,10,20,0,0,0,DateTimeKind.Utc)));
        actor.Value = new("trainer-a", [Roles.Trainer]);
        var next = await Service.ReviseAsync(original, "client-a");
        await Service.EditAsync(next, Draft with { Title = "Revision", Exercises = [new("Run", null, null, 30)] });
        Assert.Equal("Original", data.Rows<WorkoutPlan>()[0].Title);
        Assert.Equal(original, Assert.Single(data.Rows<WorkoutLog>()).WorkoutPlanId);
        Assert.Equal(2, data.Rows<WorkoutPlan>()[1].Version);
        Assert.Equal(original, data.Rows<WorkoutPlan>()[1].PreviousPlanId);
        await Assert.ThrowsAsync<RequestFailure>(() => Service.ReviseAsync(original, "client-a"));
    }
    [Fact]
    public async Task Template_clone_is_independent_and_owner_scoped()
    {
        var id = await Service.SaveTemplateAsync(null, new("Reusable", Draft.Exercises));
        await Service.CloneTemplateAsync(id, "client-a", Draft.StartDate!.Value, Draft.EndDate!.Value);
        await Service.SaveTemplateAsync(id, new("Changed", [new("Run", null, null, 40)]));
        Assert.Equal("Walk", Assert.Single(data.Rows<WorkoutPlanExercise>()).ExerciseName);
        actor.Value = new("trainer-b", [Roles.Trainer]);
        Assert.Empty(await Service.TemplatesAsync(1,20));
        await Assert.ThrowsAsync<RequestFailure>(() => Service.SaveTemplateAsync(id, new("Stolen", Draft.Exercises)));
    }
    [Fact]
    public async Task Template_clone_requires_current_assignment_and_unarchived_template()
    {
        var id = await Service.SaveTemplateAsync(null, new("Reusable", Draft.Exercises));
        await Service.ArchiveTemplateAsync(id);
        await Assert.ThrowsAsync<RequestFailure>(() => Service.CloneTemplateAsync(id,"client-a",new(2026,10,1),new(2026,11,1)));
        data.Rows<TrainerClientAssignment>()[0].Status = AssignmentStatus.Revoked;
        await Assert.ThrowsAsync<RequestFailure>(() => Service.CloneTemplateAsync(id,"client-a",new(2026,10,1),new(2026,11,1)));
    }
    [Fact]
    public async Task Scheduled_sessions_are_immutable_after_publish_and_complete_once()
    {
        var id = await Service.CreateAsync(Draft);
        var when = new DateTime(2026,10,20,0,0,0,DateTimeKind.Utc);
        await Service.SetSessionsAsync(id,"client-a",[when]); await Service.PublishAsync(id,"client-a");
        await Assert.ThrowsAsync<RequestFailure>(() => Service.SetSessionsAsync(id,"client-a",[]));
        actor.Value = new("client-a", [Roles.Client]);
        await Service.LogAsync(new(id,when,1));
        await Assert.ThrowsAsync<RequestFailure>(() => Service.LogAsync(new(id,when,1)));
        Assert.Single(data.Rows<WorkoutLog>());
    }
    [Fact]
    public async Task Invalid_schedule_and_draft_revisions_are_rejected()
    {
        var id = await Service.CreateAsync(Draft);
        await Assert.ThrowsAsync<RequestFailure>(() => Service.SetSessionsAsync(id,"client-a",[new(2026,12,1,0,0,0,DateTimeKind.Utc)]));
        await Assert.ThrowsAsync<RequestFailure>(() => Service.ReviseAsync(id,"client-a"));
        await Assert.ThrowsAsync<RequestFailure>(() => Service.ArchiveAsync(id,"client-a"));
        actor.Value = new("client-a",[Roles.Client]);
        Assert.Empty(await Service.PlansAsync("client-a",1,20,status:PlanStatus.Archived));
    }
}
