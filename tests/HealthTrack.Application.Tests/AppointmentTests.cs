using HealthTrack.Application.Appointments;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Appointments;
using HealthTrack.Domain.Notifications;
using HealthTrack.Domain.Profiles;

namespace HealthTrack.Application.Tests;

public sealed class AppointmentTests
{
    private readonly TestSession data = new();
    private readonly MutableActor actor = new() { Value = new("client-a", [Roles.Client]) };
    private readonly FixedClock clock = new();
    private AppointmentService Service => new(data, new ClientAccess(actor, data, new TestAccounts()), clock);
    private static AppointmentRequest Request => new("trainer-a", new(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc), new(2026, 10, 22, 11, 0, 0, DateTimeKind.Utc));

    public AppointmentTests()
    {
        data.Add(new TrainerClientAssignment { TrainerUserId = "trainer-a", ClientUserId = "client-a" });
        data.Add(new TrainerClientAssignment { TrainerUserId = "trainer-a", ClientUserId = "client-b" });
    }

    [Fact]
    public async Task Pending_requests_reserve_trainer_slot_and_cross_client_overlap_is_rejected()
    {
        await Service.RequestAsync(Request);
        actor.Value = new("client-b", [Roles.Client]);
        Assert.Equal(409, (await Assert.ThrowsAsync<RequestFailure>(() => Service.RequestAsync(Request))).StatusCode);
        Assert.Single(data.Rows<Appointment>());
        Assert.Contains("trainer:trainer-a", data.LockedResources);
        Assert.Contains("client:client-b", data.LockedResources);
    }

    [Fact]
    public async Task Adjacent_requests_are_allowed()
    {
        await Service.RequestAsync(Request);
        await Service.RequestAsync(Request with { StartAtUtc = Request.EndAtUtc, EndAtUtc = Request.EndAtUtc.AddHours(1) });
        Assert.Equal(2, data.Rows<Appointment>().Count);
    }

    [Fact]
    public async Task Client_cannot_double_book_across_different_assigned_trainers()
    {
        data.Add(new TrainerClientAssignment { ClientUserId = "client-a", TrainerUserId = "trainer-b" });
        await Service.RequestAsync(Request);
        var error = await Assert.ThrowsAsync<RequestFailure>(() => Service.RequestAsync(Request with { TrainerUserId = "trainer-b" }));
        Assert.Equal(409, error.StatusCode);
        Assert.Single(data.Rows<Appointment>());
    }

    [Fact]
    public async Task Client_cannot_approve_and_unrelated_client_cannot_read_history()
    {
        var id = await Service.RequestAsync(Request);
        Assert.Equal(403, (await Assert.ThrowsAsync<RequestFailure>(() => Service.ChangeAsync(id, new(AppointmentStatus.Approved, new byte[8])))).StatusCode);
        actor.Value = new("client-b", [Roles.Client]);
        Assert.Equal(404, (await Assert.ThrowsAsync<RequestFailure>(() => Service.HistoryAsync(id, 1, 20))).StatusCode);
    }

    [Fact]
    public async Task Trainer_approval_completion_records_history_and_notifications()
    {
        var id = await Service.RequestAsync(Request);
        actor.Value = new("trainer-a", [Roles.Trainer]);
        await Service.ChangeAsync(id, new(AppointmentStatus.Approved, new byte[8]));
        await Assert.ThrowsAsync<RequestFailure>(() => Service.ChangeAsync(id, new(AppointmentStatus.Completed, new byte[8])));
        clock.Now = new DateTimeOffset(Request.EndAtUtc.AddMinutes(1));
        await Service.ChangeAsync(id, new(AppointmentStatus.Completed, new byte[8]));
        Assert.Equal(AppointmentStatus.Completed, data.Rows<Appointment>()[0].Status);
        Assert.Equal(3, data.Rows<AppointmentStatusHistory>().Count);
        Assert.Equal(3, data.Rows<HealthTrack.Domain.Auditing.AuditLog>().Count);
        Assert.Contains(data.Rows<HealthTrack.Domain.Auditing.AuditLog>(), x => x.Action == "AppointmentCompleted" && x.SubjectUserId == "client-a");
        Assert.Equal(6, data.Rows<Notification>().Count);
        Assert.Equal(6, data.Rows<Notification>().Select(x => x.DeliveryKey).Distinct().Count());
    }

    [Fact]
    public async Task Rescheduling_approved_appointment_requires_renewed_approval()
    {
        var id = await Service.RequestAsync(Request);
        actor.Value = new("trainer-a", [Roles.Trainer]);
        await Service.ChangeAsync(id, new(AppointmentStatus.Approved, new byte[8]));
        await Service.RescheduleAsync(id, new(Request.StartAtUtc.AddDays(1), Request.EndAtUtc.AddDays(1), new byte[8]));
        Assert.Equal(AppointmentStatus.Pending, data.Rows<Appointment>()[0].Status);
    }

    [Fact]
    public async Task Cancelled_appointment_cannot_be_approved()
    {
        var id = await Service.RequestAsync(Request);
        await Service.ChangeAsync(id, new(AppointmentStatus.Cancelled, new byte[8]));
        actor.Value = new("trainer-a", [Roles.Trainer]);
        Assert.Equal(409, (await Assert.ThrowsAsync<RequestFailure>(() => Service.ChangeAsync(id, new(AppointmentStatus.Approved, new byte[8])))).StatusCode);
    }

    [Fact]
    public async Task Invalid_interval_and_stale_version_are_rejected()
    {
        await Assert.ThrowsAsync<RequestFailure>(() => Service.RequestAsync(Request with { EndAtUtc = Request.StartAtUtc }));
        var id = await Service.RequestAsync(Request);
        Assert.Equal(409, (await Assert.ThrowsAsync<RequestFailure>(() => Service.ChangeAsync(id, new(AppointmentStatus.Cancelled, [1, 0, 0, 0, 0, 0, 0, 0])))).StatusCode);
    }

    [Fact]
    public async Task Approval_schedules_both_participants_and_reschedule_cancels_old_reminders()
    {
        var id = await Service.RequestAsync(Request);
        Assert.Empty(data.Rows<NotificationSchedule>());
        actor.Value = new("trainer-a", [Roles.Trainer]);
        await Service.ChangeAsync(id, new(AppointmentStatus.Approved, new byte[8]));
        var reminders = data.Rows<NotificationSchedule>();
        Assert.Equal(2, reminders.Count);
        Assert.All(reminders, x =>
        {
            Assert.Equal(id, x.AppointmentId);
            Assert.Equal(ReminderKind.Appointment, x.Kind);
            Assert.Equal(Request.StartAtUtc.AddHours(-24), x.DueAtUtc);
            Assert.Equal(DateTimeKind.Utc, x.DueAtUtc.Kind);
        });
        Assert.Equal(2, reminders.Select(x => x.DeliveryKey).Distinct().Count());
        await Service.RescheduleAsync(id, new(Request.StartAtUtc.AddDays(1), Request.EndAtUtc.AddDays(1), new byte[8]));
        Assert.All(reminders, x => Assert.True(x.IsCancelled));
        await Service.ChangeAsync(id, new(AppointmentStatus.Approved, new byte[8]));
        Assert.Equal(2, reminders.Count(x => !x.IsCancelled));
        await Service.ChangeAsync(id, new(AppointmentStatus.Cancelled, new byte[8]));
        Assert.All(reminders, x => Assert.True(x.IsCancelled));
    }
}
