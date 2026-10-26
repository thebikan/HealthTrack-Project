using HealthTrack.Application.Appointments;
using HealthTrack.Application.Health;
using HealthTrack.Application.Persistence;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Appointments;
using HealthTrack.Domain.Auditing;
using HealthTrack.Domain.Health;
using HealthTrack.Domain.Notifications;
using HealthTrack.Domain.Nutrition;
using HealthTrack.Domain.Profiles;
using HealthTrack.Domain.Workouts;

namespace HealthTrack.Application.Reporting;

public sealed record ClientDashboard(int PublishedPlans, int CurrentDietPlans, int WorkoutsLast30Days,
    int UnreadNotifications, MeasurementView? LatestMeasurement, AppointmentView? NextAppointment);
public sealed record AssignedClientOverview(string ClientUserId, int PublishedPlans, int WorkoutsLast30Days);
public sealed record TrainerDashboard(int ActiveClients, int UpcomingAppointments, IReadOnlyList<AssignedClientOverview> Clients);
public sealed record AdminDashboard(AccountTotals Accounts, int ActiveAssignments, int Workouts, int Meals,
    int Measurements, int PendingAppointments, int ApprovedAppointments, int CompletedAppointments);
public sealed record WorkoutDay(DateTime DayUtc, int CompletedWorkouts);
public sealed record CalorieDay(DateTime DayUtc, decimal RecordedCalories);
public sealed record WeightDay(DateTime DayUtc, decimal AverageWeightKg);
public sealed record ClientProgress(IReadOnlyList<WorkoutDay> Workouts, IReadOnlyList<CalorieDay> Calories,
    IReadOnlyList<WeightDay> Weight, int CurrentPublishedPlans, int PlansWithLogs, decimal? CurrentPlanCoveragePercent);
public sealed record TrainerReport(int ActiveClients, int WorkoutPlans, int DietPlans, int FeedbackEntries, int CompletedAppointments);
public sealed record AuditView(int Id, string ActorUserId, string SubjectUserId, string Action, DateTime CreatedAtUtc);

public sealed partial class ReportingService(IDataSession data, ClientAccess access, IAccountDirectory accounts, TimeProvider clock)
{
    public async Task<ClientDashboard> ClientAsync(CancellationToken ct = default)
    {
        var actor = await access.ClientAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var since = now.AddDays(-30);
        var latest = await data.SingleAsync(data.Query<HealthMeasurement>().Where(x => x.ClientUserId == actor.UserId)
            .OrderByDescending(x => x.RecordedAtUtc).ThenByDescending(x => x.Id).Take(1), ct);
        var next = await data.SingleAsync(data.Query<Appointment>().Where(x => x.ClientUserId == actor.UserId && x.StartAtUtc >= now &&
                (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved))
            .OrderBy(x => x.StartAtUtc).ThenBy(x => x.Id).Take(1)
            .Select(x => new AppointmentView(x.Id, x.ClientUserId, x.TrainerUserId, x.StartAtUtc, x.EndAtUtc, x.Status, x.RowVersion)), ct);
        return new ClientDashboard(
            await data.CountAsync(data.Query<WorkoutPlan>().Where(x => x.ClientUserId == actor.UserId && x.Status == PlanStatus.Published), ct),
            await data.CountAsync(data.Query<DietPlan>().Where(x => x.ClientUserId == actor.UserId && x.StartDate <= today && x.EndDate >= today), ct),
            await data.CountAsync(data.Query<WorkoutLog>().Where(x => x.ClientUserId == actor.UserId && x.CompletedAtUtc >= since && x.CompletedAtUtc <= now), ct),
            await data.CountAsync(data.Query<Notification>().Where(x => x.UserId == actor.UserId && x.ReadAtUtc == null && x.DueAtUtc <= now), ct),
            latest is null ? null : new MeasurementView(latest.Id, latest.WeightKg, latest.HeightCm, latest.RecordedAtUtc,
                HealthCalculations.BodyMassIndex(latest.WeightKg, latest.HeightCm)), next);
    }

    public async Task<TrainerDashboard> TrainerAsync(int page, int size, CancellationToken ct = default)
    {
        var actor = await TrainerActorAsync(ct); Input.Page(page, size);
        var ids = AssignedClientIds(actor.UserId);
        var since = clock.GetUtcNow().UtcDateTime.AddDays(-30);
        var now = clock.GetUtcNow().UtcDateTime;
        var plans = data.Query<WorkoutPlan>(); var logs = data.Query<WorkoutLog>();
        var clients = await data.ListAsync(data.Query<ClientProfile>().Where(x => ids.Contains(x.UserId))
            .OrderBy(x => x.UserId).Skip((page - 1) * size).Take(size)
            .Select(x => new AssignedClientOverview(x.UserId,
                plans.Count(p => p.ClientUserId == x.UserId && p.Status == PlanStatus.Published),
                logs.Count(l => l.ClientUserId == x.UserId && l.CompletedAtUtc >= since && l.CompletedAtUtc <= now))), ct);
        return new TrainerDashboard(await data.CountAsync(ids, ct),
            await data.CountAsync(data.Query<Appointment>().Where(x => x.TrainerUserId == actor.UserId && ids.Contains(x.ClientUserId) &&
                x.StartAtUtc >= now && (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved)), ct), clients);
    }

    public async Task<AdminDashboard> AdminAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        await access.AdminAsync(ct); Input.DateRange(from, to);
        var appointments = data.Query<Appointment>().Where(x => x.StartAtUtc >= from && x.StartAtUtc < to);
        return new AdminDashboard(await accounts.TotalsAsync(ct),
            await data.CountAsync(data.Query<TrainerClientAssignment>().Where(x => x.Status == AssignmentStatus.Active), ct),
            await data.CountAsync(data.Query<WorkoutLog>().Where(x => x.CompletedAtUtc >= from && x.CompletedAtUtc < to), ct),
            await data.CountAsync(data.Query<MealLog>().Where(x => x.ConsumedAtUtc >= from && x.ConsumedAtUtc < to), ct),
            await data.CountAsync(data.Query<HealthMeasurement>().Where(x => x.RecordedAtUtc >= from && x.RecordedAtUtc < to), ct),
            await data.CountAsync(appointments.Where(x => x.Status == AppointmentStatus.Pending), ct),
            await data.CountAsync(appointments.Where(x => x.Status == AppointmentStatus.Approved), ct),
            await data.CountAsync(appointments.Where(x => x.Status == AppointmentStatus.Completed), ct));
    }

    public async Task<ClientProgress> ProgressAsync(string clientId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        await access.ReadAsync(clientId, ct); Input.DateRange(from, to);
        var logs = data.Query<WorkoutLog>().Where(x => x.ClientUserId == clientId && x.CompletedAtUtc >= from && x.CompletedAtUtc < to);
        var workouts = await data.ListAsync(logs.GroupBy(x => x.CompletedAtUtc.Date).OrderBy(x => x.Key)
            .Select(x => new WorkoutDay(x.Key, x.Count())), ct);
        var calories = await data.ListAsync(data.Query<MealLog>().Where(x => x.ClientUserId == clientId && x.ConsumedAtUtc >= from && x.ConsumedAtUtc < to)
            .GroupBy(x => x.ConsumedAtUtc.Date).OrderBy(x => x.Key).Select(x => new CalorieDay(x.Key, x.Sum(m => m.Calories))), ct);
        var weight = await data.ListAsync(data.Query<HealthMeasurement>().Where(x => x.ClientUserId == clientId && x.RecordedAtUtc >= from && x.RecordedAtUtc < to)
            .GroupBy(x => x.RecordedAtUtc.Date).OrderBy(x => x.Key).Select(x => new WeightDay(x.Key, x.Average(m => m.WeightKg))), ct);
        var firstDate = DateOnly.FromDateTime(from); var lastDate = DateOnly.FromDateTime(to.AddTicks(-1));
        var planIds = data.Query<WorkoutPlan>().Where(x => x.ClientUserId == clientId && x.Status == PlanStatus.Published &&
            x.StartDate <= lastDate && x.EndDate >= firstDate).Select(x => x.Id);
        var count = await data.CountAsync(planIds, ct);
        var logged = await data.CountAsync(logs.Where(x => x.WorkoutPlanId.HasValue && planIds.Contains(x.WorkoutPlanId.Value))
            .Select(x => x.WorkoutPlanId).Distinct(), ct);
        // SQL date projections can lose DateTime.Kind even though stored events are UTC.
        return new ClientProgress(
            workouts.Select(x => x with { DayUtc = DateTime.SpecifyKind(x.DayUtc, DateTimeKind.Utc) }).ToArray(),
            calories.Select(x => x with { DayUtc = DateTime.SpecifyKind(x.DayUtc, DateTimeKind.Utc) }).ToArray(),
            weight.Select(x => x with { DayUtc = DateTime.SpecifyKind(x.DayUtc, DateTimeKind.Utc) }).ToArray(),
            count, logged, ProgressCalculations.PlanCoverage(logged, count));
    }

    public async Task<TrainerReport> TrainerReportAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var actor = await TrainerActorAsync(ct); Input.DateRange(from, to);
        var clients = AssignedClientIds(actor.UserId);
        var start = DateOnly.FromDateTime(from); var end = DateOnly.FromDateTime(to.AddTicks(-1));
        return new TrainerReport(await data.CountAsync(clients, ct),
            await data.CountAsync(data.Query<WorkoutPlan>().Where(x => x.TrainerUserId == actor.UserId && clients.Contains(x.ClientUserId) &&
                x.StartDate <= end && x.EndDate >= start), ct),
            await data.CountAsync(data.Query<DietPlan>().Where(x => x.TrainerUserId == actor.UserId && clients.Contains(x.ClientUserId) &&
                x.StartDate <= end && x.EndDate >= start), ct),
            await data.CountAsync(data.Query<TrainerFeedback>().Where(x => x.TrainerUserId == actor.UserId && clients.Contains(x.ClientUserId) &&
                x.CreatedAtUtc >= from && x.CreatedAtUtc < to), ct),
            await data.CountAsync(data.Query<Appointment>().Where(x => x.TrainerUserId == actor.UserId && clients.Contains(x.ClientUserId) &&
                x.StartAtUtc >= from && x.StartAtUtc < to && x.Status == AppointmentStatus.Completed), ct));
    }

    public async Task<IReadOnlyList<AuditView>> AuditAsync(DateTime from, DateTime to, int page, int size, CancellationToken ct = default, string? action = null, string? actorId = null)
    {
        await access.AdminAsync(ct); Input.DateRange(from, to); Input.Page(page, size);
        if (action?.Length > 80 || actorId?.Length > 128) throw RequestFailure.Invalid("Invalid audit filter.");
        return await data.ListAsync(data.Query<AuditLog>().Where(x => x.CreatedAtUtc >= from && x.CreatedAtUtc < to && (action == null || x.Action == action) && (actorId == null || x.ActorUserId == actorId))
            .OrderByDescending(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new AuditView(x.Id, x.ActorUserId, x.SubjectUserId, x.Action, x.CreatedAtUtc)), ct);
    }

    private IQueryable<string> AssignedClientIds(string trainerId) => data.Query<TrainerClientAssignment>()
        .Where(x => x.TrainerUserId == trainerId && x.Status == AssignmentStatus.Active).Select(x => x.ClientUserId).Distinct();

    private async Task<Actor> TrainerActorAsync(CancellationToken ct)
    {
        var actor = await access.ActorAsync(ct);
        return actor.IsInRole(Roles.Trainer) ? actor : throw RequestFailure.Forbidden();
    }
}
