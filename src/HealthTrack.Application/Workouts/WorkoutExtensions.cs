using HealthTrack.Application.Security;
using HealthTrack.Domain.Workouts;

namespace HealthTrack.Application.Workouts;

public sealed record TemplateRequest(string Title, IReadOnlyList<ExerciseInput> Exercises);
public sealed record TemplateView(int Id, string Title, bool IsArchived, IReadOnlyList<ExerciseInput> Exercises);
public sealed record SessionView(int Id, DateTime ScheduledAtUtc);
public sealed partial class WorkoutService
{
    private async Task<Actor> TrainerAsync(CancellationToken ct)
    { var actor = await access.ActorAsync(ct); return actor.IsInRole(Roles.Trainer) ? actor : throw RequestFailure.Forbidden(); }

    public async Task<int> SaveTemplateAsync(int? id, TemplateRequest request, CancellationToken ct = default)
    {
        var actor = await TrainerAsync(ct);
        Validate(new("template", request.Title, null, null, request.Exercises));
        if (request.Exercises.Count == 0) throw RequestFailure.Invalid("A template requires exercises.");
        return await data.SerializeAsync(["templates:" + actor.UserId], async () =>
        {
            var template = id.HasValue ? await TemplateAsync(id.Value, actor.UserId, true, ct) : new WorkoutTemplate { TrainerUserId = actor.UserId };
            if (template.IsArchived) throw RequestFailure.Conflict();
            template.Title = request.Title.Trim();
            if (!id.HasValue) data.Add(template);
            await data.SaveAsync(ct);
            foreach (var row in await data.ListAsync(data.Query<WorkoutTemplateExercise>(true).Where(x => x.WorkoutTemplateId == template.Id), ct)) data.Remove(row);
            foreach (var x in request.Exercises) data.Add(new WorkoutTemplateExercise { WorkoutTemplateId = template.Id, ExerciseName = x.Name.Trim(), Sets = x.Sets, Reps = x.Reps, DurationMinutes = x.DurationMinutes });
            await data.SaveAsync(ct);
            return template.Id;
        }, ct);
    }
    public async Task<IReadOnlyList<TemplateView>> TemplatesAsync(int page, int size, bool archived = false, CancellationToken ct = default)
    {
        var actor = await TrainerAsync(ct); Input.Page(page, size);
        var rows = await data.ListAsync(data.Query<WorkoutTemplate>().Where(x => x.TrainerUserId == actor.UserId && x.IsArchived == archived).OrderBy(x => x.Id).Skip((page - 1) * size).Take(size), ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var children = await data.ListAsync(data.Query<WorkoutTemplateExercise>().Where(x => ids.Contains(x.WorkoutTemplateId)).OrderBy(x => x.Id), ct);
        var lookup = children.ToLookup(x => x.WorkoutTemplateId);
        return rows.Select(x => new TemplateView(x.Id, x.Title, x.IsArchived, lookup[x.Id].Select(e => new ExerciseInput(e.ExerciseName, e.Sets, e.Reps, e.DurationMinutes)).ToArray())).ToArray();
    }
    public async Task ArchiveTemplateAsync(int id, CancellationToken ct = default)
    {
        var actor = await TrainerAsync(ct);
        await data.SerializeAsync(["templates:" + actor.UserId], async () =>
        { (await TemplateAsync(id, actor.UserId, true, ct)).IsArchived = true; await data.SaveAsync(ct); return 0; }, ct);
    }
    public async Task<int> CloneTemplateAsync(int id, string clientId, DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        var actor = await access.TrainerWriteAsync(clientId, ct);
        return await data.SerializeAsync(["client:" + clientId, "templates:" + actor.UserId], async () =>
        {
            await access.TrainerWriteAsync(clientId, ct);
            var template = await TemplateAsync(id, actor.UserId, false, ct);
            if (template.IsArchived) throw RequestFailure.Conflict();
            var exercises = await data.ListAsync(data.Query<WorkoutTemplateExercise>().Where(x => x.WorkoutTemplateId == id).OrderBy(x => x.Id), ct);
            var inputs = exercises.Select(x => new ExerciseInput(x.ExerciseName, x.Sets, x.Reps, x.DurationMinutes)).ToArray();
            Validate(new(clientId, template.Title, start, end, inputs));
            var plan = new WorkoutPlan { ClientUserId = clientId, TrainerUserId = actor.UserId, Title = template.Title, StartDate = start, EndDate = end };
            data.Add(plan); await data.SaveAsync(ct); AddExercises(plan.Id, inputs); await data.SaveAsync(ct); return plan.Id;
        }, ct);
    }
    public async Task<int> ReviseAsync(int id, string clientId, CancellationToken ct = default)
    {
        Input.Text(clientId, 128, "Client ID");
        return await data.SerializeAsync(["client:" + clientId], async () =>
        {
            var actor = await access.TrainerWriteAsync(clientId, ct);
            var old = await OwnedPlanAsync(id, clientId, actor.UserId, ct);
            if (old.Status == PlanStatus.Draft || await data.AnyAsync(data.Query<WorkoutPlan>().Where(x => x.PreviousPlanId == id), ct)) throw RequestFailure.Conflict();
            var exercises = await data.ListAsync(data.Query<WorkoutPlanExercise>().Where(x => x.WorkoutPlanId == id).OrderBy(x => x.Id), ct);
            var plan = new WorkoutPlan { ClientUserId = clientId, TrainerUserId = actor.UserId, Title = old.Title,
                StartDate = old.StartDate, EndDate = old.EndDate, PreviousPlanId = id, Version = checked(old.Version + 1) };
            data.Add(plan); await data.SaveAsync(ct);
            AddExercises(plan.Id, exercises.Select(x => new ExerciseInput(x.ExerciseName, x.Sets, x.Reps, x.DurationMinutes)));
            await data.SaveAsync(ct); return plan.Id;
        }, ct);
    }
    public async Task SetSessionsAsync(int id, string clientId, IReadOnlyList<DateTime> times, CancellationToken ct = default)
    {
        Input.Text(clientId, 128, "Client ID");
        if (times is null || times.Count > 366 || times.Distinct().Count() != times.Count || times.Any(x => x.Kind != DateTimeKind.Utc)) throw RequestFailure.Invalid("Supply up to 366 unique UTC session times.");
        await data.SerializeAsync(["client:" + clientId], async () =>
        {
            var actor = await access.TrainerWriteAsync(clientId, ct);
            var plan = await OwnedPlanAsync(id, clientId, actor.UserId, ct);
            if (plan.Status != PlanStatus.Draft) throw RequestFailure.Conflict();
            if (!plan.StartDate.HasValue || !plan.EndDate.HasValue || times.Any(x => DateOnly.FromDateTime(x) < plan.StartDate || DateOnly.FromDateTime(x) > plan.EndDate)) throw RequestFailure.Invalid("Sessions require plan dates and must fall within them.");
            foreach (var old in await data.ListAsync(data.Query<WorkoutSession>(true).Where(x => x.WorkoutPlanId == id), ct)) data.Remove(old);
            foreach (var time in times) data.Add(new WorkoutSession { WorkoutPlanId = id, ClientUserId = clientId, ScheduledAtUtc = time });
            await data.SaveAsync(ct); return 0;
        }, ct);
    }
    public async Task<IReadOnlyList<SessionView>> SessionsAsync(int id, string clientId, CancellationToken ct = default)
    {
        var actor = await access.ReadAsync(clientId, ct);
        if (!await data.AnyAsync(data.Query<WorkoutPlan>().Where(x => x.Id == id && x.ClientUserId == clientId &&
            (actor.IsInRole(Roles.Trainer) || x.Status != PlanStatus.Draft)), ct)) throw RequestFailure.Missing();
        return await data.ListAsync(data.Query<WorkoutSession>().Where(x => x.WorkoutPlanId == id).OrderBy(x => x.ScheduledAtUtc).Select(x => new SessionView(x.Id, x.ScheduledAtUtc)), ct);
    }
    private async Task<WorkoutTemplate> TemplateAsync(int id, string trainer, bool tracking, CancellationToken ct) =>
        await data.SingleAsync(data.Query<WorkoutTemplate>(tracking).Where(x => x.Id == id && x.TrainerUserId == trainer), ct) ?? throw RequestFailure.Missing();
}
