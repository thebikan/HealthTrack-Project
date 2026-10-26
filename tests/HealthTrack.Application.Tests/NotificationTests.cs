using HealthTrack.Application.Notifications;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Appointments;
using HealthTrack.Domain.Notifications;

namespace HealthTrack.Application.Tests;

public sealed class NotificationTests
{
    [Fact]
    public async Task Preferences_cancel_pending_kind_and_reenable_does_not_resurrect_it()
    {
        await Service.ScheduleAsync(Request);
        await Service.UpdatePreferencesAsync(new(false,true,true));
        Assert.True(data.Rows<NotificationSchedule>()[0].IsCancelled);
        await Assert.ThrowsAsync<RequestFailure>(() => Service.ScheduleAsync(Request with { RequestId = Guid.NewGuid() }));
        await Service.UpdatePreferencesAsync(new(true,true,true));
        clock.Now = clock.Now.AddMinutes(11);
        Assert.Equal(0,await Processor.ProcessAsync());
    }
    [Fact]
    public async Task Preference_changes_are_scoped_to_current_user()
    {
        await Service.UpdatePreferencesAsync(new(false,false,false));
        actor.Value = new("client-b",[Roles.Client]);
        Assert.Equal(new NotificationPreferences(true,true,true),await Service.PreferencesAsync());
        Assert.Equal("client-a",Assert.Single(data.Rows<NotificationPreference>()).UserId);
    }
    [Fact]
    public async Task Delivery_rechecks_preferences_and_honors_cancellation_token()
    {
        await Service.ScheduleAsync(Request);
        data.Add(new NotificationPreference { UserId = "client-a", WorkoutReminders = false });
        clock.Now = clock.Now.AddMinutes(11);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Processor.ProcessAsync(cancelled.Token));
        Assert.Equal(0,await Processor.ProcessAsync());
        Assert.Empty(data.Rows<Notification>());
    }
    private readonly TestSession data = new();
    private readonly MutableActor actor = new() { Value = new("client-a", [Roles.Client]) };
    private readonly TestAccounts accounts = new();
    private readonly FixedClock clock = new();
    private NotificationService Service => new(data, new ClientAccess(actor, data, accounts), clock);
    private ReminderProcessor Processor => new(data, accounts, clock);
    private ReminderRequest Request => new(ReminderKind.Workout, clock.Now.UtcDateTime.AddMinutes(10), Guid.Parse("11111111-1111-1111-1111-111111111111"));

    [Fact]
    public async Task Scheduling_is_idempotent_and_rejects_reused_key_with_different_payload()
    {
        var request = Request;
        Assert.Equal(await Service.ScheduleAsync(request), await Service.ScheduleAsync(request));
        Assert.Single(data.Rows<NotificationSchedule>());
        Assert.Equal(409, (await Assert.ThrowsAsync<RequestFailure>(() => Service.ScheduleAsync(request with { Kind = ReminderKind.Meal }))).StatusCode);
    }

    [Fact]
    public async Task Reprocessing_delivers_once_and_persists_delivery_state()
    {
        await Service.ScheduleAsync(Request);
        clock.Now = clock.Now.AddMinutes(11);
        Assert.Equal(1, await Processor.ProcessAsync());
        Assert.Equal(0, await Processor.ProcessAsync());
        Assert.Single(data.Rows<Notification>());
        Assert.NotNull(data.Rows<NotificationSchedule>()[0].DeliveredAtUtc);
        Assert.Equal(1, data.Rows<NotificationSchedule>()[0].Attempts);
    }

    [Fact]
    public async Task Failed_delivery_preserves_backoff_and_retries_without_duplicates()
    {
        await Service.ScheduleAsync(Request);
        clock.Now = clock.Now.AddMinutes(11);
        data.FailNextNotificationAdd = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Processor.ProcessAsync());
        Assert.Equal(1, data.Rows<NotificationSchedule>()[0].Attempts);
        Assert.NotNull(data.Rows<NotificationSchedule>()[0].NextAttemptAtUtc);
        Assert.Equal(0, await Processor.ProcessAsync());
        clock.Now = clock.Now.AddMinutes(2);
        Assert.Equal(1, await Processor.ProcessAsync());
        Assert.Single(data.Rows<Notification>());
        Assert.Equal(2, data.Rows<NotificationSchedule>()[0].Attempts);
    }

    [Fact]
    public async Task Disabled_recipient_does_not_receive_reminder()
    {
        await Service.ScheduleAsync(Request);
        accounts.Active.Remove(("client-a", Roles.Client));
        clock.Now = clock.Now.AddMinutes(11);
        Assert.Equal(0, await Processor.ProcessAsync());
        Assert.True(data.Rows<NotificationSchedule>()[0].IsCancelled);
    }

    [Fact]
    public async Task Another_user_cannot_read_or_cancel_notifications()
    {
        var id = await Service.ScheduleAsync(Request);
        actor.Value = new("client-b", [Roles.Client]);
        Assert.Empty(await Service.SchedulesAsync(1, 20));
        Assert.Equal(404, (await Assert.ThrowsAsync<RequestFailure>(() => Service.CancelAsync(id, new byte[8]))).StatusCode);
        data.Add(new Notification { UserId = "client-a", Title = "Private", Message = "Synthetic", DueAtUtc = clock.Now.UtcDateTime });
        Assert.Equal(404, (await Assert.ThrowsAsync<RequestFailure>(() => Service.ReadAsync(1))).StatusCode);
        Assert.Empty(await Service.ListAsync(false, 1, 20));
    }

    [Fact]
    public async Task Cancelled_appointment_never_generates_a_reminder()
    {
        data.Add(new Appointment { ClientUserId = "client-a", TrainerUserId = "trainer-a", Status = AppointmentStatus.Cancelled,
            StartAtUtc = clock.Now.UtcDateTime.AddHours(1), EndAtUtc = clock.Now.UtcDateTime.AddHours(2) });
        data.Add(new NotificationSchedule { UserId = "client-a", Kind = ReminderKind.Appointment, AppointmentId = 1,
            DueAtUtc = clock.Now.UtcDateTime, DeliveryKey = "synthetic-appointment" });
        Assert.Equal(0, await Processor.ProcessAsync());
        Assert.Empty(data.Rows<Notification>());
        Assert.True(data.Rows<NotificationSchedule>()[0].IsCancelled);
    }

    [Theory]
    [InlineData(ReminderKind.Workout)]
    [InlineData(ReminderKind.Meal)]
    public async Task Future_reminders_wait_until_due_and_can_be_marked_read(ReminderKind kind)
    {
        await Service.ScheduleAsync(Request with { Kind = kind });
        Assert.Equal(0, await Processor.ProcessAsync());
        clock.Now = clock.Now.AddMinutes(10);
        Assert.Equal(1, await Processor.ProcessAsync());
        var notification = Assert.Single(await Service.ListAsync(true, 1, 20));
        Assert.Equal(DateTimeKind.Utc, notification.DueAtUtc.Kind);
        await Service.ReadAsync(notification.Id);
        var firstRead = Assert.Single(await Service.ListAsync(false, 1, 20)).ReadAtUtc;
        clock.Now = clock.Now.AddMinutes(1);
        await Service.ReadAsync(notification.Id);
        Assert.Equal(firstRead, Assert.Single(await Service.ListAsync(false, 1, 20)).ReadAtUtc);
        Assert.Empty(await Service.ListAsync(true, 1, 20));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public async Task Scheduling_rejects_non_utc_dates(DateTimeKind kind)
    {
        await Assert.ThrowsAsync<RequestFailure>(() => Service.ScheduleAsync(Request with
        { DueAtUtc = DateTime.SpecifyKind(Request.DueAtUtc, kind) }));
        Assert.Empty(data.Rows<NotificationSchedule>());
    }

    [Fact]
    public async Task Cancelled_schedule_is_not_delivered_and_stale_cancel_is_rejected()
    {
        var id = await Service.ScheduleAsync(Request);
        Assert.Equal(409, (await Assert.ThrowsAsync<RequestFailure>(() => Service.CancelAsync(id, [1,0,0,0,0,0,0,0]))).StatusCode);
        await Service.CancelAsync(id, new byte[8]);
        clock.Now = clock.Now.AddMinutes(11);
        Assert.Equal(0, await Processor.ProcessAsync());
        Assert.Empty(data.Rows<Notification>());
    }

    [Fact]
    public async Task Existing_delivery_key_is_reconciled_without_a_second_notification()
    {
        await Service.ScheduleAsync(Request);
        var schedule = data.Rows<NotificationSchedule>()[0];
        data.Add(new Notification { UserId = schedule.UserId, DeliveryKey = schedule.DeliveryKey,
            Title = "Synthetic", Message = "Existing delivery", DueAtUtc = schedule.DueAtUtc });
        clock.Now = clock.Now.AddMinutes(11);
        Assert.Equal(0, await Processor.ProcessAsync());
        Assert.Single(data.Rows<Notification>());
        Assert.NotNull(schedule.DeliveredAtUtc);
    }

    [Fact]
    public async Task Invalid_reminder_requests_and_trainer_creation_are_rejected()
    {
        foreach (var request in new[] { Request with { Kind = ReminderKind.Appointment },
            Request with { RequestId = Guid.Empty }, Request with { DueAtUtc = clock.Now.UtcDateTime },
            Request with { DueAtUtc = clock.Now.UtcDateTime.AddYears(2) } })
            Assert.Equal(400, (await Assert.ThrowsAsync<RequestFailure>(() => Service.ScheduleAsync(request))).StatusCode);
        actor.Value = new("trainer-a", [Roles.Trainer]);
        Assert.Equal(403, (await Assert.ThrowsAsync<RequestFailure>(() => Service.ScheduleAsync(Request))).StatusCode);
    }
}
