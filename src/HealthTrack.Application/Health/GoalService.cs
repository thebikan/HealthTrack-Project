using HealthTrack.Application.Persistence;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Health;
using HealthTrack.Domain.Workouts;

namespace HealthTrack.Application.Health;

public sealed record GoalRequest(GoalType Type, decimal Target, DateOnly StartDate, DateOnly TargetDate);
public sealed record GoalUpdate(decimal Target, DateOnly TargetDate, byte[] RowVersion);
public sealed record GoalView(int Id, GoalType Type, GoalStatus Status, decimal? Baseline, decimal Target,
    DateOnly StartDate, DateOnly TargetDate, decimal? Current, decimal? ProgressPercent, byte[] RowVersion);
public sealed class GoalService(IDataSession data, ClientAccess access, TimeProvider clock)
{
    public async Task<int> CreateAsync(GoalRequest request, CancellationToken ct = default)
    {
        var actor = await access.ClientAsync(ct);
        Validate(request);
        return await data.SerializeAsync(["client:" + actor.UserId], async () =>
        {
            decimal? baseline = null;
            if (request.Type is GoalType.WeightReduction or GoalType.WeightGain)
            {
                var measurement = await data.SingleAsync(data.Query<HealthMeasurement>().Where(x => x.ClientUserId == actor.UserId && x.RecordedAtUtc <= clock.GetUtcNow().UtcDateTime)
                    .OrderByDescending(x => x.RecordedAtUtc).ThenByDescending(x => x.Id).Take(1), ct);
                baseline = measurement?.WeightKg ?? throw RequestFailure.Invalid("Record a measurement before creating a weight goal.");
                Direction(request.Type, baseline.Value, request.Target);
            }
            var goal = new FitnessGoal { ClientUserId = actor.UserId, Type = request.Type, Target = request.Target,
                Baseline = baseline, StartDate = request.StartDate, TargetDate = request.TargetDate, CreatedAtUtc = clock.GetUtcNow().UtcDateTime };
            data.Add(goal); await data.SaveAsync(ct); return goal.Id;
        }, ct);
    }
    public async Task UpdateAsync(int id, GoalUpdate request, CancellationToken ct = default)
    {
        var actor = await access.ClientAsync(ct);
        await data.SerializeAsync(["client:" + actor.UserId], async () =>
        {
            var goal = await Owned(id, actor.UserId, request.RowVersion, ct);
            Validate(new(goal.Type, request.Target, goal.StartDate, request.TargetDate), false);
            if (goal.Baseline.HasValue) Direction(goal.Type, goal.Baseline.Value, request.Target);
            goal.Target = request.Target; goal.TargetDate = request.TargetDate;
            await data.SaveAsync(ct); return 0;
        }, ct);
    }
    public async Task FinishAsync(int id, byte[] version, bool cancel, CancellationToken ct = default)
    {
        var actor = await access.ClientAsync(ct);
        await data.SerializeAsync(["client:" + actor.UserId], async () =>
        {
            var goal = await Owned(id, actor.UserId, version, ct);
            var view = await View(goal, ct);
            if (!cancel && view.ProgressPercent is not >= 100) throw RequestFailure.Conflict();
            goal.Status = cancel ? GoalStatus.Cancelled : GoalStatus.Completed;
            goal.CompletedAtUtc = cancel ? null : clock.GetUtcNow().UtcDateTime;
            goal.CompletedValue = cancel ? null : view.Current;
            await data.SaveAsync(ct); return 0;
        }, ct);
    }
    public async Task<IReadOnlyList<GoalView>> ListAsync(string clientId, int page, int size, CancellationToken ct = default)
    {
        await access.ReadAsync(clientId, ct); Input.Page(page, size);
        var goals = await data.ListAsync(data.Query<FitnessGoal>().Where(x => x.ClientUserId == clientId).OrderByDescending(x => x.Id).Skip((page - 1) * size).Take(size), ct);
        if (goals.Count == 0) return [];
        var pending = goals.Where(x => x.Status != GoalStatus.Completed).ToArray();
        List<DailyWorkouts> days = []; List<DailyWeight> weights = [];
        if (pending.Length > 0)
        {
            var start = pending.Min(x => x.StartDate).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = pending.Max(x => x.TargetDate).AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var now = clock.GetUtcNow().UtcDateTime;
            if (pending.Any(x => x.Type is GoalType.WorkoutConsistency or GoalType.WorkoutCompletion))
                days = await data.ListAsync(data.Query<WorkoutLog>().Where(x => x.ClientUserId == clientId && x.CompletedAtUtc >= start && x.CompletedAtUtc < end && x.CompletedAtUtc <= now)
                    .GroupBy(x => x.CompletedAtUtc.Date).Select(x => new DailyWorkouts(x.Key, x.Count())), ct);
            if (pending.Any(x => x.Type is GoalType.WeightReduction or GoalType.WeightGain))
                weights = await data.ListAsync(data.Query<HealthMeasurement>().Where(x => x.ClientUserId == clientId && x.RecordedAtUtc >= start && x.RecordedAtUtc < end && x.RecordedAtUtc <= now)
                    .GroupBy(x => x.RecordedAtUtc.Date).Select(x => new DailyWeight(x.Key,
                        x.OrderByDescending(m => m.RecordedAtUtc).ThenByDescending(m => m.Id).Select(m => m.WeightKg).First())), ct);
        }
        // At most two aggregate reads for the whole page, rather than one query per goal.
        return goals.Select(goal =>
        {
            ct.ThrowIfCancellationRequested();
            decimal? current = goal.CompletedValue;
            if (goal.Status != GoalStatus.Completed)
            {
                var start = goal.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var end = goal.TargetDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                current = goal.Type switch
                {
                    GoalType.WorkoutCompletion => days.Where(x => x.Day >= start && x.Day < end).Sum(x => x.Count),
                    GoalType.WorkoutConsistency => days.Count(x => x.Day >= start && x.Day < end),
                    _ => weights.Where(x => x.Day >= start && x.Day < end).OrderByDescending(x => x.Day).FirstOrDefault()?.Weight
                };
            }
            return new GoalView(goal.Id, goal.Type, goal.Status, goal.Baseline, goal.Target, goal.StartDate, goal.TargetDate, current,
                GoalCalculations.Percent(goal.Type, goal.Baseline, goal.Target, current), goal.RowVersion);
        }).ToArray();
    }
    private sealed record DailyWorkouts(DateTime Day, int Count);
    private sealed record DailyWeight(DateTime Day, decimal Weight);
    private async Task<GoalView> View(FitnessGoal goal, CancellationToken ct)
    {
        if (goal.Status == GoalStatus.Completed)
            return new(goal.Id, goal.Type, goal.Status, goal.Baseline, goal.Target, goal.StartDate, goal.TargetDate,
                goal.CompletedValue, GoalCalculations.Percent(goal.Type, goal.Baseline, goal.Target, goal.CompletedValue), goal.RowVersion);
        var start = goal.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = goal.TargetDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var now = goal.CompletedAtUtc ?? clock.GetUtcNow().UtcDateTime;
        decimal? current;
        if (goal.Type is GoalType.WeightReduction or GoalType.WeightGain)
        {
            var measurement = await data.SingleAsync(data.Query<HealthMeasurement>().Where(x => x.ClientUserId == goal.ClientUserId && x.RecordedAtUtc >= start && x.RecordedAtUtc < end && x.RecordedAtUtc <= now)
                .OrderByDescending(x => x.RecordedAtUtc).ThenByDescending(x => x.Id).Take(1), ct);
            current = measurement?.WeightKg;
        }
        else
        {
            var logs = data.Query<WorkoutLog>().Where(x => x.ClientUserId == goal.ClientUserId && x.CompletedAtUtc >= start && x.CompletedAtUtc < end && x.CompletedAtUtc <= now);
            current = goal.Type == GoalType.WorkoutConsistency ? await data.CountAsync(logs.Select(x => x.CompletedAtUtc.Date).Distinct(), ct) : await data.CountAsync(logs, ct);
        }
        return new(goal.Id, goal.Type, goal.Status, goal.Baseline, goal.Target, goal.StartDate, goal.TargetDate, current,
            GoalCalculations.Percent(goal.Type, goal.Baseline, goal.Target, current), goal.RowVersion);
    }
    private async Task<FitnessGoal> Owned(int id, string client, byte[] version, CancellationToken ct)
    {
        if (version is not { Length: 8 }) throw RequestFailure.Invalid("Current row version required.");
        var goal = await data.SingleAsync(data.Query<FitnessGoal>(true).Where(x => x.Id == id && x.ClientUserId == client), ct) ?? throw RequestFailure.Missing();
        if (goal.Status != GoalStatus.Active || !goal.RowVersion.SequenceEqual(version)) throw RequestFailure.Conflict();
        return goal;
    }
    private void Validate(GoalRequest request, bool creating = true)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        if (!Enum.IsDefined(request.Type) || request.StartDate.Year < 1970 || request.TargetDate.Year >= 9999 ||
            request.TargetDate < request.StartDate || request.TargetDate < today || (creating && request.StartDate < today) ||
            request.TargetDate.DayNumber - request.StartDate.DayNumber > 3660) throw RequestFailure.Invalid("Use a valid goal type and date range of at most ten years, starting today or later for new goals.");
        Input.Decimal(request.Target, 0.01m, 9999.99m, "Target");
        if (request.Type is GoalType.WorkoutConsistency or GoalType.WorkoutCompletion && decimal.Truncate(request.Target) != request.Target) throw RequestFailure.Invalid("Workout targets must be whole numbers.");
        if (request.Type == GoalType.WorkoutConsistency && request.Target > request.TargetDate.DayNumber - request.StartDate.DayNumber + 1) throw RequestFailure.Invalid("Consistency target exceeds available UTC days.");
    }
    private static void Direction(GoalType type, decimal baseline, decimal target)
    {
        if (type == GoalType.WeightReduction ? target >= baseline : target <= baseline) throw RequestFailure.Invalid("Weight target must follow the goal direction and differ from the baseline.");
    }
}
