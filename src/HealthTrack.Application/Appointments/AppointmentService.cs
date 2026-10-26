using HealthTrack.Application.Persistence;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Appointments;
using HealthTrack.Domain.Auditing;
using HealthTrack.Domain.Notifications;
using HealthTrack.Domain.Profiles;

namespace HealthTrack.Application.Appointments;

public sealed record AppointmentRequest(string TrainerUserId, DateTime StartAtUtc, DateTime EndAtUtc);
public sealed record AppointmentChange(AppointmentStatus Status, byte[] RowVersion);
public sealed record AppointmentReschedule(DateTime StartAtUtc, DateTime EndAtUtc, byte[] RowVersion);
public sealed record AppointmentView(int Id, string ClientUserId, string TrainerUserId, DateTime StartAtUtc,
    DateTime EndAtUtc, AppointmentStatus Status, byte[] RowVersion);
public sealed record AppointmentHistoryView(AppointmentStatus? PreviousStatus, AppointmentStatus Status, DateTime ChangedAtUtc);

public sealed class AppointmentService(IDataSession data, ClientAccess access, TimeProvider clock)
{
    public async Task<int> RequestAsync(AppointmentRequest request, CancellationToken ct = default)
    {
        var actor = await access.ClientAsync(ct);
        Input.Text(request.TrainerUserId, 128, "Trainer ID");
        ValidateTimes(request.StartAtUtc, request.EndAtUtc);
        return await data.SerializeAsync(["client:" + actor.UserId, "trainer:" + request.TrainerUserId], async () =>
        {
            await access.ClientAsync(ct);
            if (!await access.AssignedAsync(request.TrainerUserId, actor.UserId, ct)) throw RequestFailure.Forbidden();
            await AvailableAsync(request.TrainerUserId, actor.UserId, request.StartAtUtc, request.EndAtUtc, null, ct);
            var appointment = new Appointment
            {
                ClientUserId = actor.UserId, TrainerUserId = request.TrainerUserId,
                StartAtUtc = request.StartAtUtc, EndAtUtc = request.EndAtUtc
            };
            data.Add(appointment);
            await data.SaveAsync(ct);
            await RecordChangeAsync(appointment, null, actor.UserId, ct);
            return appointment.Id;
        }, ct);
    }

    public async Task ChangeAsync(int id, AppointmentChange request, CancellationToken ct = default)
    {
        var (actor, existing) = await ParticipantAsync(id, ct);
        ValidateVersion(request.RowVersion);
        if (!Enum.IsDefined(request.Status)) throw RequestFailure.Invalid("Invalid appointment status.");
        await data.SerializeAsync(["client:" + existing.ClientUserId, "trainer:" + existing.TrainerUserId], async () =>
        {
            var appointment = await LoadAsync(id, ct);
            await AuthorizeChangeAsync(actor, appointment, request.Status, ct);
            if (!appointment.RowVersion.SequenceEqual(request.RowVersion) || !AppointmentRules.CanTransition(appointment.Status, request.Status))
                throw RequestFailure.Conflict();
            if (request.Status == AppointmentStatus.Completed && appointment.EndAtUtc > clock.GetUtcNow().UtcDateTime)
                throw RequestFailure.Invalid("Appointments cannot be completed before their end time.");
            if (request.Status == AppointmentStatus.Approved)
            {
                if (appointment.StartAtUtc <= clock.GetUtcNow().UtcDateTime) throw RequestFailure.Conflict();
                await AvailableAsync(appointment.TrainerUserId, appointment.ClientUserId, appointment.StartAtUtc, appointment.EndAtUtc, id, ct);
            }
            var previous = appointment.Status;
            appointment.Status = request.Status;
            await RecordChangeAsync(appointment, previous, actor.UserId, ct);
            return 0;
        }, ct);
    }

    public async Task RescheduleAsync(int id, AppointmentReschedule request, CancellationToken ct = default)
    {
        var (actor, existing) = await ParticipantAsync(id, ct);
        ValidateVersion(request.RowVersion); ValidateTimes(request.StartAtUtc, request.EndAtUtc);
        await data.SerializeAsync(["client:" + existing.ClientUserId, "trainer:" + existing.TrainerUserId], async () =>
        {
            await ParticipantAsync(id, ct);
            var appointment = await LoadAsync(id, ct);
            if (!await access.AssignedAsync(appointment.TrainerUserId, appointment.ClientUserId, ct)) throw RequestFailure.Forbidden();
            if (!appointment.RowVersion.SequenceEqual(request.RowVersion) || appointment.Status is not (AppointmentStatus.Pending or AppointmentStatus.Approved))
                throw RequestFailure.Conflict();
            await AvailableAsync(appointment.TrainerUserId, appointment.ClientUserId, request.StartAtUtc, request.EndAtUtc, id, ct);
            var previous = appointment.Status;
            appointment.StartAtUtc = request.StartAtUtc; appointment.EndAtUtc = request.EndAtUtc;
            appointment.Status = AppointmentStatus.Pending; // Any reschedule requires renewed approval.
            await RecordChangeAsync(appointment, previous, actor.UserId, ct);
            return 0;
        }, ct);
    }

    public async Task<IReadOnlyList<AppointmentView>> ListAsync(DateTime from, DateTime to, int page, int size, CancellationToken ct = default)
    {
        var actor = await access.ActorAsync(ct);
        Input.DateRange(from, to); Input.Page(page, size);
        var query = data.Query<Appointment>();
        if (actor.IsInRole(Roles.Client)) query = query.Where(x => x.ClientUserId == actor.UserId);
        else if (actor.IsInRole(Roles.Trainer))
        {
            var clients = data.Query<TrainerClientAssignment>().Where(x => x.TrainerUserId == actor.UserId && x.Status == AssignmentStatus.Active).Select(x => x.ClientUserId);
            query = query.Where(x => x.TrainerUserId == actor.UserId && clients.Contains(x.ClientUserId));
        }
        else throw RequestFailure.Forbidden();
        return await data.ListAsync(query.Where(x => x.StartAtUtc >= from && x.StartAtUtc < to)
            .OrderBy(x => x.StartAtUtc).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new AppointmentView(x.Id, x.ClientUserId, x.TrainerUserId, x.StartAtUtc, x.EndAtUtc, x.Status, x.RowVersion)), ct);
    }

    public async Task<IReadOnlyList<AppointmentHistoryView>> HistoryAsync(int id, int page, int size, CancellationToken ct = default)
    {
        await ParticipantAsync(id, ct); Input.Page(page, size);
        return await data.ListAsync(data.Query<AppointmentStatusHistory>().Where(x => x.AppointmentId == id)
            .OrderByDescending(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new AppointmentHistoryView(x.PreviousStatus, x.Status, x.ChangedAtUtc)), ct);
    }

    private async Task<(Actor Actor, Appointment Appointment)> ParticipantAsync(int id, CancellationToken ct)
    {
        var actor = await access.ActorAsync(ct);
        var appointment = await data.SingleAsync(data.Query<Appointment>().Where(x => x.Id == id &&
            (x.ClientUserId == actor.UserId || x.TrainerUserId == actor.UserId)), ct) ?? throw RequestFailure.Missing();
        if (actor.UserId == appointment.ClientUserId && actor.IsInRole(Roles.Client)) return (actor, appointment);
        if (actor.UserId == appointment.TrainerUserId && actor.IsInRole(Roles.Trainer) &&
            await access.AssignedAsync(appointment.TrainerUserId, appointment.ClientUserId, ct)) return (actor, appointment);
        throw RequestFailure.Forbidden();
    }

    private async Task AuthorizeChangeAsync(Actor actor, Appointment appointment, AppointmentStatus next, CancellationToken ct)
    {
        var current = await access.ActorAsync(ct);
        if (current.UserId != actor.UserId) throw RequestFailure.Forbidden();
        if (current.IsInRole(Roles.Client) && current.UserId == appointment.ClientUserId && next == AppointmentStatus.Cancelled) return;
        if (!current.IsInRole(Roles.Trainer) || current.UserId != appointment.TrainerUserId ||
            !await access.AssignedAsync(appointment.TrainerUserId, appointment.ClientUserId, ct)) throw RequestFailure.Forbidden();
    }

    private async Task<Appointment> LoadAsync(int id, CancellationToken ct) =>
        await data.SingleAsync(data.Query<Appointment>(true).Where(x => x.Id == id), ct) ?? throw RequestFailure.Missing();

    private async Task AvailableAsync(string trainerId, string clientId, DateTime start, DateTime end, int? excluding, CancellationToken ct)
    {
        if (await data.AnyAsync(data.Query<Appointment>().Where(x => (x.TrainerUserId == trainerId || x.ClientUserId == clientId) &&
            (!excluding.HasValue || x.Id != excluding.Value) &&
            (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved) &&
            x.StartAtUtc < end && x.EndAtUtc > start), ct)) throw RequestFailure.Conflict();
    }

    private async Task RecordChangeAsync(Appointment appointment, AppointmentStatus? previous, string actorId, CancellationToken ct)
    {
        data.Add(new AuditLog { ActorUserId = actorId, SubjectUserId = appointment.ClientUserId,
            Action = "Appointment" + appointment.Status, CreatedAtUtc = clock.GetUtcNow().UtcDateTime });
        var history = new AppointmentStatusHistory
        {
            AppointmentId = appointment.Id, PreviousStatus = previous, Status = appointment.Status,
            ChangedByUserId = actorId, ChangedAtUtc = clock.GetUtcNow().UtcDateTime
        };
        data.Add(history);
        await data.SaveAsync(ct);
        foreach (var schedule in await data.ListAsync(data.Query<NotificationSchedule>(true).Where(x =>
                     x.AppointmentId == appointment.Id && x.DeliveredAtUtc == null && !x.IsCancelled), ct))
            schedule.IsCancelled = true;
        if (appointment.Status == AppointmentStatus.Approved)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var due = appointment.StartAtUtc.AddHours(-24);
            foreach (var recipient in new[] { appointment.ClientUserId, appointment.TrainerUserId }) data.Add(new NotificationSchedule
            {
                AppointmentId = appointment.Id, UserId = recipient, Kind = ReminderKind.Appointment,
                DueAtUtc = due < now ? now : due,
                DeliveryKey = $"appointment-reminder:{history.Id}:{(recipient == appointment.ClientUserId ? "client" : "trainer")}"
            });
        }
        foreach (var userId in new[] { appointment.ClientUserId, appointment.TrainerUserId }) data.Add(new Notification
        {
            UserId = userId, Title = "Appointment update", Message = "An appointment was requested or updated. Open your appointments to review it.",
            DueAtUtc = clock.GetUtcNow().UtcDateTime,
            DeliveryKey = $"appointment-history:{history.Id}:{(userId == appointment.ClientUserId ? "client" : "trainer")}"
        });
        await data.SaveAsync(ct);
    }

    private void ValidateTimes(DateTime start, DateTime end)
    {
        if (start.Kind != DateTimeKind.Utc || end.Kind != DateTimeKind.Utc || start <= clock.GetUtcNow().UtcDateTime ||
            end <= start || end - start > TimeSpan.FromHours(8))
            throw RequestFailure.Invalid("Appointments require future UTC times and a duration between zero and eight hours.");
    }

    private static void ValidateVersion(byte[] version)
    {
        if (version is not { Length: 8 }) throw RequestFailure.Invalid("The current row version is required.");
    }
}
