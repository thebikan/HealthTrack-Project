using System.Security.Cryptography;
using System.Text;
using HealthTrack.Application.Persistence;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Notifications;

namespace HealthTrack.Application.Notifications;

public sealed record ReminderRequest(ReminderKind Kind, DateTime DueAtUtc, Guid RequestId);
public sealed record NotificationView(int Id, string Title, string Message, DateTime DueAtUtc, DateTime? ReadAtUtc);
public sealed record ReminderView(int Id, ReminderKind Kind, DateTime DueAtUtc, DateTime? DeliveredAtUtc, bool IsCancelled, byte[] RowVersion);
public sealed record NotificationPreferences(bool WorkoutReminders, bool MealReminders, bool AppointmentReminders);

public sealed class NotificationService(IDataSession data, ClientAccess access, TimeProvider clock)
{
    public async Task<IReadOnlyList<NotificationView>> ListAsync(bool unreadOnly, int page, int size, CancellationToken ct = default)
    {
        var actor = await access.ActorAsync(ct); Input.Page(page, size);
        var now = clock.GetUtcNow().UtcDateTime;
        return await data.ListAsync(data.Query<Notification>().Where(x => x.UserId == actor.UserId && x.DueAtUtc <= now && (!unreadOnly || x.ReadAtUtc == null))
            .OrderByDescending(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new NotificationView(x.Id, x.Title, x.Message, x.DueAtUtc, x.ReadAtUtc)), ct);
    }

    public async Task ReadAsync(int id, CancellationToken ct = default)
    {
        var actor = await access.ActorAsync(ct);
        await data.SerializeAsync(["notification:" + id], async () =>
        {
            var notification = await data.SingleAsync(data.Query<Notification>(true).Where(x => x.Id == id && x.UserId == actor.UserId), ct)
                ?? throw RequestFailure.Missing();
            notification.ReadAtUtc ??= clock.GetUtcNow().UtcDateTime;
            await data.SaveAsync(ct);
            return 0;
        }, ct);
    }

    public async Task<int> ScheduleAsync(ReminderRequest request, CancellationToken ct = default)
    {
        var actor = await access.ClientAsync(ct);
        if (request.Kind is not (ReminderKind.Workout or ReminderKind.Meal) || request.RequestId == Guid.Empty ||
            request.DueAtUtc.Kind != DateTimeKind.Utc || request.DueAtUtc <= clock.GetUtcNow().UtcDateTime ||
            request.DueAtUtc > clock.GetUtcNow().UtcDateTime.AddYears(1))
            throw RequestFailure.Invalid("Use a workout/meal reminder, a unique request ID, and a future UTC time within one year.");
        var key = "user-reminder:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(actor.UserId + ":" + request.RequestId)));
        return await data.SerializeAsync(["reminder-user:" + actor.UserId], async () =>
        {
            var preferences = await PreferencesAsync(ct);
            if (request.Kind == ReminderKind.Workout ? !preferences.WorkoutReminders : !preferences.MealReminders)
                throw RequestFailure.Invalid("Enable this reminder type in your preferences before scheduling.");
            var existing = await data.SingleAsync(data.Query<NotificationSchedule>().Where(x => x.DeliveryKey == key), ct);
            if (existing is not null)
            {
                if (existing.UserId != actor.UserId || existing.Kind != request.Kind || existing.DueAtUtc != request.DueAtUtc)
                    throw RequestFailure.Conflict();
                return existing.Id;
            }
            var schedule = new NotificationSchedule { UserId = actor.UserId, Kind = request.Kind, DueAtUtc = request.DueAtUtc, DeliveryKey = key };
            data.Add(schedule);
            await data.SaveAsync(ct);
            return schedule.Id;
        }, ct);
    }

    public async Task<NotificationPreferences> PreferencesAsync(CancellationToken ct = default)
    {
        var actor = await access.ActorAsync(ct);
        var row = await data.SingleAsync(data.Query<NotificationPreference>().Where(x => x.UserId == actor.UserId), ct);
        return row is null ? new(true, true, true) : new(row.WorkoutReminders, row.MealReminders, row.AppointmentReminders);
    }
    public async Task UpdatePreferencesAsync(NotificationPreferences request, CancellationToken ct = default)
    {
        var actor = await access.ActorAsync(ct);
        await data.SerializeAsync(["reminder-user:" + actor.UserId], async () =>
        {
            var row = await data.SingleAsync(data.Query<NotificationPreference>(true).Where(x => x.UserId == actor.UserId), ct);
            if (row is null) { row = new NotificationPreference { UserId = actor.UserId }; data.Add(row); }
            row.WorkoutReminders = request.WorkoutReminders; row.MealReminders = request.MealReminders; row.AppointmentReminders = request.AppointmentReminders;
            var cancelled = await data.ListAsync(data.Query<NotificationSchedule>(true).Where(x => x.UserId == actor.UserId && x.DeliveredAtUtc == null && !x.IsCancelled &&
                ((x.Kind == ReminderKind.Workout && !request.WorkoutReminders) || (x.Kind == ReminderKind.Meal && !request.MealReminders) || (x.Kind == ReminderKind.Appointment && !request.AppointmentReminders))), ct);
            foreach (var schedule in cancelled) schedule.IsCancelled = true;
            await data.SaveAsync(ct); return 0;
        }, ct);
    }

    public async Task<IReadOnlyList<ReminderView>> SchedulesAsync(int page, int size, CancellationToken ct = default)
    {
        var actor = await access.ActorAsync(ct); Input.Page(page, size);
        return await data.ListAsync(data.Query<NotificationSchedule>().Where(x => x.UserId == actor.UserId)
            .OrderByDescending(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new ReminderView(x.Id, x.Kind, x.DueAtUtc, x.DeliveredAtUtc, x.IsCancelled, x.RowVersion)), ct);
    }

    public async Task CancelAsync(int id, byte[] rowVersion, CancellationToken ct = default)
    {
        var actor = await access.ActorAsync(ct);
        if (rowVersion is not { Length: 8 }) throw RequestFailure.Invalid("The current row version is required.");
        await data.SerializeAsync(["schedule:" + id], async () =>
        {
            var schedule = await data.SingleAsync(data.Query<NotificationSchedule>(true).Where(x => x.Id == id && x.UserId == actor.UserId), ct)
                ?? throw RequestFailure.Missing();
            if (!schedule.RowVersion.SequenceEqual(rowVersion) || schedule.DeliveredAtUtc.HasValue) throw RequestFailure.Conflict();
            schedule.IsCancelled = true;
            await data.SaveAsync(ct);
            return 0;
        }, ct);
    }
}
