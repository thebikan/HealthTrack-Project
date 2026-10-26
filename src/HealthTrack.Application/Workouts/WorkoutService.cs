using HealthTrack.Application.Persistence;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Workouts;

namespace HealthTrack.Application.Workouts;

public sealed record ExerciseInput(string Name, int? Sets, int? Reps, decimal? DurationMinutes);
public sealed record WorkoutDraftRequest(string ClientUserId, string Title, DateOnly? StartDate, DateOnly? EndDate,
    IReadOnlyList<ExerciseInput> Exercises);
public sealed record WorkoutPlanView(int Id, string Title, PlanStatus Status, int Version,
    DateOnly? StartDate, DateOnly? EndDate, IReadOnlyList<ExerciseInput> Exercises, int? PreviousPlanId = null);
public sealed record WorkoutLogRequest(int? PlanId, DateTime CompletedAtUtc, int? SessionId = null);
public sealed record WorkoutLogView(int Id, int? PlanId, DateTime CompletedAtUtc);

public sealed partial class WorkoutService(IDataSession data, ClientAccess access, TimeProvider clock)
{
    public async Task<int> CreateAsync(WorkoutDraftRequest request, CancellationToken ct = default)
    {
        Validate(request);
        return await data.SerializeAsync(["client:" + request.ClientUserId], async () =>
        {
            var actor = await access.TrainerWriteAsync(request.ClientUserId, ct);
            var plan = new WorkoutPlan
            {
                ClientUserId = request.ClientUserId, TrainerUserId = actor.UserId, Title = request.Title.Trim(),
                StartDate = request.StartDate, EndDate = request.EndDate
            };
            data.Add(plan);
            await data.SaveAsync(ct);
            AddExercises(plan.Id, request.Exercises);
            await data.SaveAsync(ct);
            return plan.Id;
        }, ct);
    }

    public async Task EditAsync(int id, WorkoutDraftRequest request, CancellationToken ct = default)
    {
        Validate(request);
        await data.SerializeAsync(["client:" + request.ClientUserId], async () =>
        {
            var actor = await access.TrainerWriteAsync(request.ClientUserId, ct);
            var plan = await OwnedPlanAsync(id, request.ClientUserId, actor.UserId, ct);
            if (plan.Status != PlanStatus.Draft) throw RequestFailure.Conflict();
            foreach (var exercise in await data.ListAsync(data.Query<WorkoutPlanExercise>(true).Where(x => x.WorkoutPlanId == id), ct))
                data.Remove(exercise);
            plan.Title = request.Title.Trim(); plan.StartDate = request.StartDate; plan.EndDate = request.EndDate;
            AddExercises(id, request.Exercises);
            await data.SaveAsync(ct);
            return 0;
        }, ct);
    }

    public async Task PublishAsync(int id, string clientId, CancellationToken ct = default)
    {
        Input.Text(clientId, 128, "Client ID");
        await data.SerializeAsync(["client:" + clientId], async () =>
        {
            var actor = await access.TrainerWriteAsync(clientId, ct);
            var plan = await OwnedPlanAsync(id, clientId, actor.UserId, ct);
            if (plan.Status != PlanStatus.Draft) throw RequestFailure.Conflict();
            if (!plan.StartDate.HasValue || !plan.EndDate.HasValue ||
                !await data.AnyAsync(data.Query<WorkoutPlanExercise>().Where(x => x.WorkoutPlanId == id), ct))
                throw RequestFailure.Invalid("Publishing requires a date range and at least one exercise.");
            var sessions = await data.ListAsync(data.Query<WorkoutSession>().Where(x => x.WorkoutPlanId == id), ct);
            if (sessions.Any(x => DateOnly.FromDateTime(x.ScheduledAtUtc) < plan.StartDate || DateOnly.FromDateTime(x.ScheduledAtUtc) > plan.EndDate))
                throw RequestFailure.Invalid("Scheduled sessions must fall within the plan dates.");
            plan.Status = PlanStatus.Published;
            await data.SaveAsync(ct);
            return 0;
        }, ct);
    }

    public async Task ArchiveAsync(int id, string clientId, CancellationToken ct = default)
    {
        Input.Text(clientId, 128, "Client ID");
        await data.SerializeAsync(["client:" + clientId], async () =>
        {
            var actor = await access.TrainerWriteAsync(clientId, ct);
            var plan = await OwnedPlanAsync(id, clientId, actor.UserId, ct);
            // Archived content is client-readable history, so unpublished drafts must never enter it.
            if (plan.Status != PlanStatus.Published) throw RequestFailure.Conflict();
            plan.Status = PlanStatus.Archived;
            await data.SaveAsync(ct);
            return 0;
        }, ct);
    }

    public async Task<IReadOnlyList<WorkoutPlanView>> PlansAsync(string clientId, int page, int size, CancellationToken ct = default,
        PlanStatus? status = null, DateOnly? from = null, DateOnly? to = null)
    {
        var actor = await access.ReadAsync(clientId, ct);
        Input.Page(page, size);
        var query = data.Query<WorkoutPlan>().Where(x => x.ClientUserId == clientId);
        if (status.HasValue && !Enum.IsDefined(status.Value) || from.HasValue != to.HasValue || to < from) throw RequestFailure.Invalid("Invalid plan filter.");
        if (actor.IsInRole(Roles.Client)) query = query.Where(x => x.Status == PlanStatus.Published || (status == PlanStatus.Archived && x.Status == PlanStatus.Archived));
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (from.HasValue) query = query.Where(x => x.StartDate <= to && x.EndDate >= from);
        var plans = await data.ListAsync(query.OrderByDescending(x => x.Id).Skip((page - 1) * size).Take(size), ct);
        var ids = plans.Select(x => x.Id).ToArray();
        var exercises = await data.ListAsync(data.Query<WorkoutPlanExercise>().Where(x => ids.Contains(x.WorkoutPlanId)).OrderBy(x => x.Id), ct);
        var exerciseLookup = exercises.ToLookup(x => x.WorkoutPlanId);
        return plans.Select(p => new WorkoutPlanView(p.Id, p.Title, p.Status, p.Version, p.StartDate, p.EndDate,
            exerciseLookup[p.Id].Select(x => new ExerciseInput(x.ExerciseName, x.Sets, x.Reps, x.DurationMinutes)).ToArray(), p.PreviousPlanId)).ToArray();
    }

    public async Task<int> LogAsync(WorkoutLogRequest request, CancellationToken ct = default)
    {
        var actor = await access.ClientAsync(ct);
        Input.EventTime(request.CompletedAtUtc, clock);
        return await data.SerializeAsync(["client:" + actor.UserId], async () =>
        {
            await access.ClientAsync(ct);
            if (request.PlanId.HasValue && !await data.AnyAsync(data.Query<WorkoutPlan>().Where(x =>
                    x.Id == request.PlanId && x.ClientUserId == actor.UserId && x.Status == PlanStatus.Published), ct))
                throw RequestFailure.Missing();
            if (request.SessionId.HasValue)
            {
                if (!request.PlanId.HasValue || !await data.AnyAsync(data.Query<WorkoutSession>().Where(x => x.Id == request.SessionId &&
                    x.WorkoutPlanId == request.PlanId && x.ClientUserId == actor.UserId && x.ScheduledAtUtc <= request.CompletedAtUtc), ct)) throw RequestFailure.Missing();
                if (await data.AnyAsync(data.Query<WorkoutLog>().Where(x => x.SessionId == request.SessionId), ct)) throw RequestFailure.Conflict();
            }
            var log = new WorkoutLog { ClientUserId = actor.UserId, WorkoutPlanId = request.PlanId, SessionId = request.SessionId, CompletedAtUtc = request.CompletedAtUtc };
            data.Add(log);
            await data.SaveAsync(ct);
            return log.Id;
        }, ct);
    }

    public async Task<IReadOnlyList<WorkoutLogView>> LogsAsync(string clientId, DateTime from, DateTime to, int page, int size,
        CancellationToken ct = default)
    {
        await access.ReadAsync(clientId, ct);
        Input.DateRange(from, to); Input.Page(page, size);
        return await data.ListAsync(data.Query<WorkoutLog>().Where(x => x.ClientUserId == clientId && x.CompletedAtUtc >= from && x.CompletedAtUtc < to)
            .OrderByDescending(x => x.CompletedAtUtc).ThenByDescending(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new WorkoutLogView(x.Id, x.WorkoutPlanId, x.CompletedAtUtc)), ct);
    }

    private async Task<WorkoutPlan> OwnedPlanAsync(int id, string clientId, string trainerId, CancellationToken ct) =>
        await data.SingleAsync(data.Query<WorkoutPlan>(true).Where(x => x.Id == id && x.ClientUserId == clientId && x.TrainerUserId == trainerId), ct)
        ?? throw RequestFailure.Missing();

    private void AddExercises(int planId, IEnumerable<ExerciseInput> exercises)
    {
        foreach (var exercise in exercises) data.Add(new WorkoutPlanExercise
        {
            WorkoutPlanId = planId, ExerciseName = exercise.Name.Trim(), Sets = exercise.Sets,
            Reps = exercise.Reps, DurationMinutes = exercise.DurationMinutes
        });
    }

    private static void Validate(WorkoutDraftRequest request)
    {
        Input.Text(request.ClientUserId, 128, "Client ID"); Input.Text(request.Title, 200, "Title");
        if (request.StartDate.HasValue != request.EndDate.HasValue || request.EndDate < request.StartDate || request.StartDate?.Year < 1970)
            throw RequestFailure.Invalid("Supply both plan dates in increasing order, or neither for a draft.");
        if (request.Exercises is null || request.Exercises.Count > 100) throw RequestFailure.Invalid("At most 100 exercises are allowed.");
        foreach (var exercise in request.Exercises)
        {
            if (exercise is null) throw RequestFailure.Invalid("Exercise is required.");
            Input.Text(exercise.Name, 200, "Exercise name");
            if (exercise.Sets is <= 0 || exercise.Reps is <= 0 || (!exercise.Reps.HasValue && !exercise.DurationMinutes.HasValue))
                throw RequestFailure.Invalid("Exercise targets must be positive; repetitions or duration is required.");
            if (exercise.DurationMinutes.HasValue) Input.Decimal(exercise.DurationMinutes.Value, 0.01m, 999999.99m, "Duration");
        }
    }
}
