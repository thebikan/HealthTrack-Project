using HealthTrack.Application.Persistence;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Appointments;
using HealthTrack.Domain.Notifications;
using HealthTrack.Domain.Profiles;

namespace HealthTrack.Application.Notifications;

public sealed class ReminderProcessor(IDataSession data, IAccountDirectory accounts, TimeProvider clock)
{
    public async Task<int> ProcessAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var ids = await data.ListAsync(data.Query<NotificationSchedule>().Where(x => !x.IsCancelled && x.DeliveredAtUtc == null &&
                x.DueAtUtc <= now && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now))
            .OrderBy(x => x.DueAtUtc).ThenBy(x => x.Id).Take(50).Select(x => x.Id), ct);
        var delivered = 0;
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            // Persist the attempt/backoff before delivery so a crash leaves a retryable schedule.
            var claimed = await data.SerializeAsync(["schedule:" + id], async () =>
            {
                var schedule = await LoadAsync(id, ct);
                if (schedule.IsCancelled || schedule.DeliveredAtUtc.HasValue || schedule.NextAttemptAtUtc > now) return false;
                schedule.Attempts = checked(schedule.Attempts + 1);
                schedule.NextAttemptAtUtc = now.AddSeconds(Math.Min(3600, 60 * Math.Pow(2, Math.Min(schedule.Attempts - 1, 6))));
                await data.SaveAsync(ct);
                return true;
            }, ct);
            if (!claimed) continue;
            delivered += await data.SerializeAsync(["schedule:" + id], async () =>
            {
                var schedule = await LoadAsync(id, ct);
                if (schedule.IsCancelled || schedule.DeliveredAtUtc.HasValue) return 0;
                if (!await RecipientActiveAsync(schedule.UserId, ct) || !await PreferenceAllowsAsync(schedule, ct) || !await AppointmentStillValidAsync(schedule, ct))
                {
                    schedule.IsCancelled = true;
                    await data.SaveAsync(ct);
                    return 0;
                }
                var existing = await data.SingleAsync(data.Query<Notification>().Where(x => x.DeliveryKey == schedule.DeliveryKey), ct);
                if (existing is not null && existing.UserId != schedule.UserId) throw new InvalidOperationException("Invalid reminder ownership.");
                if (existing is null) data.Add(new Notification
                {
                    UserId = schedule.UserId, Title = schedule.Kind + " reminder",
                    Message = "You have a scheduled activity. Open HealthTrack to review it.",
                    DueAtUtc = schedule.DueAtUtc, DeliveryKey = schedule.DeliveryKey
                });
                schedule.DeliveredAtUtc = clock.GetUtcNow().UtcDateTime;
                schedule.NextAttemptAtUtc = null;
                await data.SaveAsync(ct);
                return existing is null ? 1 : 0;
            }, ct);
        }
        return delivered;
    }

    private async Task<NotificationSchedule> LoadAsync(int id, CancellationToken ct) =>
        await data.SingleAsync(data.Query<NotificationSchedule>(true).Where(x => x.Id == id), ct)
        ?? throw RequestFailure.Missing();

    private async Task<bool> RecipientActiveAsync(string userId, CancellationToken ct)
    {
        foreach (var role in Roles.All) if (await accounts.IsActiveInRoleAsync(userId, role, ct)) return true;
        return false;
    }

    private async Task<bool> PreferenceAllowsAsync(NotificationSchedule schedule, CancellationToken ct)
    {
        var preference = await data.SingleAsync(data.Query<NotificationPreference>().Where(x => x.UserId == schedule.UserId), ct);
        return preference is null || schedule.Kind switch
        {
            ReminderKind.Workout => preference.WorkoutReminders,
            ReminderKind.Meal => preference.MealReminders,
            ReminderKind.Appointment => preference.AppointmentReminders,
            _ => false
        };
    }

    private async Task<bool> AppointmentStillValidAsync(NotificationSchedule schedule, CancellationToken ct)
    {
        if (!schedule.AppointmentId.HasValue) return schedule.Kind != ReminderKind.Appointment;
        var appointment = await data.SingleAsync(data.Query<Appointment>().Where(x => x.Id == schedule.AppointmentId &&
            x.Status == AppointmentStatus.Approved && x.StartAtUtc > clock.GetUtcNow().UtcDateTime), ct);
        return appointment is not null && (appointment.ClientUserId == schedule.UserId || appointment.TrainerUserId == schedule.UserId) &&
            await data.AnyAsync(data.Query<TrainerClientAssignment>().Where(x => x.ClientUserId == appointment.ClientUserId &&
                x.TrainerUserId == appointment.TrainerUserId && x.Status == AssignmentStatus.Active), ct);
    }
}
