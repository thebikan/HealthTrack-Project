using HealthTrack.Application.Appointments;
using HealthTrack.Application.Assignments;
using HealthTrack.Application.Health;
using HealthTrack.Application.Notifications;
using HealthTrack.Application.Nutrition;
using HealthTrack.Application.Reporting;
using HealthTrack.Application.Security;
using HealthTrack.Application.Workouts;
using HealthTrack.Application.Persistence;
using HealthTrack.Application.Accounts;
using HealthTrack.Domain.Nutrition;
using HealthTrack.Domain.Health;
using HealthTrack.Domain.Notifications;
using HealthTrack.Infrastructure.Identity;
using HealthTrack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HealthTrack.IntegrationTests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class SqlServerModuleTests : SqlTestEnvironment
{
    [SqlServerFact]
    public async Task Concurrent_booking_for_same_client_with_different_trainers_allows_one()
    {
        await Assign();
        var secondTrainer = await Register(Roles.Trainer);
        await Use(Admin, sp => sp.GetRequiredService<AssignmentService>().AssignAsync(new(secondTrainer, ClientId)));
        var start = Clock.Now.UtcDateTime.AddDays(2);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Book(string trainer)
        {
            await gate.Task;
            try
            {
                await Use(Client, sp => sp.GetRequiredService<AppointmentService>().RequestAsync(new(trainer, start, start.AddHours(1))));
                return true;
            }
            catch (RequestFailure error) when (error.StatusCode == 409) { return false; }
        }
        var first = Book(TrainerId); var second = Book(secondTrainer); gate.SetResult();
        Assert.Single(await Task.WhenAll(first, second), succeeded => succeeded);
        Assert.Equal(1, await Use(null, sp => sp.GetRequiredService<HealthTrackDbContext>().Appointments.CountAsync(x => x.ClientUserId == ClientId)));
    }
    [SqlServerFact]
    public async Task Transaction_failure_rolls_back_previously_saved_rows()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Use(Client, async sp =>
        {
            var data = sp.GetRequiredService<IDataSession>();
            await data.SerializeAsync<int>(["client:"+ClientId],async () =>
            {
                data.Add(new MealLog { ClientUserId=ClientId,MealName="Synthetic rollback",Calories=1,ConsumedAtUtc=Clock.Now.UtcDateTime });
                await data.SaveAsync();
                throw new InvalidOperationException("Synthetic rollback trigger");
            });
        }));
        Assert.Equal(0,await Use(null,sp=>sp.GetRequiredService<HealthTrackDbContext>().MealLogs.CountAsync(x=>x.ClientUserId==ClientId)));
    }

    [SqlServerFact]
    public async Task Goal_preferences_and_template_revision_round_trip()
    {
        await Assign();
        var today=DateOnly.FromDateTime(Clock.Now.UtcDateTime);
        var template=await Use(Trainer,sp=>sp.GetRequiredService<WorkoutService>().SaveTemplateAsync(null,new("SQL template",[new("Walk",null,null,20)])));
        var plan=await Use(Trainer,sp=>sp.GetRequiredService<WorkoutService>().CloneTemplateAsync(template,ClientId,today,today.AddDays(7)));
        await Use(Trainer,sp=>sp.GetRequiredService<WorkoutService>().SetSessionsAsync(plan,ClientId,[Clock.Now.UtcDateTime]));
        await Use(Trainer,sp=>sp.GetRequiredService<WorkoutService>().PublishAsync(plan,ClientId));
        var sessions=await Use(Client,sp=>sp.GetRequiredService<WorkoutService>().SessionsAsync(plan,ClientId));
        await Use(Client,sp=>sp.GetRequiredService<WorkoutService>().LogAsync(new(plan,Clock.Now.UtcDateTime,Assert.Single(sessions).Id)));
        var revision=await Use(Trainer,sp=>sp.GetRequiredService<WorkoutService>().ReviseAsync(plan,ClientId));
        var row=await Use(null,sp=>sp.GetRequiredService<HealthTrackDbContext>().WorkoutPlans.SingleAsync(x=>x.Id==revision));
        Assert.Equal(plan,row.PreviousPlanId); Assert.Equal(2,row.Version);
        await Use(Client,sp=>sp.GetRequiredService<GoalService>().CreateAsync(new(HealthTrack.Domain.Health.GoalType.WorkoutCompletion,1,today,today.AddDays(7))));
        var goal=Assert.Single(await Use(Client,sp=>sp.GetRequiredService<GoalService>().ListAsync(ClientId,1,20)));
        Assert.Equal(100m,goal.ProgressPercent);
        await Use(Client,sp=>sp.GetRequiredService<GoalService>().FinishAsync(goal.Id,goal.RowVersion,false));
        await Use(Client, sp => sp.GetRequiredService<HealthService>().RecordAsync(new(80,175,Clock.Now.UtcDateTime)));
        await Use(Client, sp => sp.GetRequiredService<GoalService>().CreateAsync(new(HealthTrack.Domain.Health.GoalType.WeightReduction,70,today,today.AddDays(7))));
        var goals = await Use(Client, sp => sp.GetRequiredService<GoalService>().ListAsync(ClientId,1,20));
        Assert.Equal(80m, Assert.Single(goals, x => x.Type == HealthTrack.Domain.Health.GoalType.WeightReduction).Current);
        await Use(Client,sp=>sp.GetRequiredService<NotificationService>().UpdatePreferencesAsync(new(false,true,true)));
        Assert.False((await Use(Client,sp=>sp.GetRequiredService<NotificationService>().PreferencesAsync())).WorkoutReminders);
        Assert.Equal(100m,(await Use(Client,sp=>sp.GetRequiredService<ReportingService>().AdherenceAsync(ClientId,Clock.Now.UtcDateTime.AddDays(-1),Clock.Now.UtcDateTime.AddDays(1)))).AdherencePercent);
    }

    [SqlServerFact]
    public async Task Password_reset_persists_new_stamp_and_rejects_token_reuse()
    {
        var issued=await Use(null,async sp=>
        {
            var users=sp.GetRequiredService<UserManager<ApplicationUser>>();
            var user=(await users.FindByIdAsync(ClientId))!;
            return (user.Email!,Token:await users.GeneratePasswordResetTokenAsync(user),Stamp:user.SecurityStamp);
        });
        const string password="Synthetic-New-Sql-Password42!";
        Assert.True(await Use(null,sp=>sp.GetRequiredService<IPasswordRecoveryGateway>().ResetAsync(new(issued.Item1,issued.Token,password),default)));
        Assert.False(await Use(null,sp=>sp.GetRequiredService<IPasswordRecoveryGateway>().ResetAsync(new(issued.Item1,issued.Token,password),default)));
        await Use(null,async sp=>
        {
            var users=sp.GetRequiredService<UserManager<ApplicationUser>>(); var user=(await users.FindByIdAsync(ClientId))!;
            Assert.NotEqual(issued.Stamp,user.SecurityStamp);
            Assert.True(await users.CheckPasswordAsync(user,password));
        });
    }
    private Task<int> Assign() => Use(Admin, sp => sp.GetRequiredService<AssignmentService>().AssignAsync(new(TrainerId, ClientId)));

    [SqlServerFact]
    public Task Identity_registration_persists_role_and_profile_in_new_scope() => Use(null, async sp =>
    {
        var user = await sp.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(ClientId);
        Assert.NotNull(user);
        Assert.True(await sp.GetRequiredService<UserManager<ApplicationUser>>().IsInRoleAsync(user, Roles.Client));
        Assert.True(await sp.GetRequiredService<HealthTrackDbContext>().ClientProfiles.AnyAsync(x => x.UserId == ClientId));
        Assert.NotNull(user.PasswordHash);
    });

    [SqlServerFact]
    public async Task Assignments_persist_and_revocation_removes_access()
    {
        var id = await Assign();
        var version = await Use(null, sp => sp.GetRequiredService<HealthTrackDbContext>().TrainerClientAssignments
            .Where(x => x.Id == id).Select(x => x.RowVersion).SingleAsync());
        Assert.Equal(8, version.Length);
        await Use(Trainer, sp => sp.GetRequiredService<ClientAccess>().ReadAsync(ClientId));
        await Use(Admin, sp => sp.GetRequiredService<AssignmentService>().RevokeAsync(id, version));
        await Assert.ThrowsAsync<RequestFailure>(() => Use(Trainer, sp => sp.GetRequiredService<ClientAccess>().ReadAsync(ClientId)));
    }

    [SqlServerFact]
    public async Task Workouts_persist_exercises_publication_and_owned_logs()
    {
        await Assign();
        var today = DateOnly.FromDateTime(Clock.Now.UtcDateTime);
        var id = await Use(Trainer, sp => sp.GetRequiredService<WorkoutService>().CreateAsync(
            new(ClientId, "Synthetic SQL workout", today, today.AddDays(7), [new("Walk", null, null, 20)])));
        await Use(Trainer, sp => sp.GetRequiredService<WorkoutService>().PublishAsync(id, ClientId));
        await Use(Client, sp => sp.GetRequiredService<WorkoutService>().LogAsync(new(id, Clock.Now.UtcDateTime)));
        var plans = await Use(Client, sp => sp.GetRequiredService<WorkoutService>().PlansAsync(ClientId, 1, 20));
        Assert.Single(Assert.Single(plans).Exercises);
        await Assert.ThrowsAsync<RequestFailure>(() => Use(new(OtherClientId, [Roles.Client]),
            sp => sp.GetRequiredService<WorkoutService>().LogAsync(new(id, Clock.Now.UtcDateTime))));
    }

    [SqlServerFact]
    public async Task Diet_plans_and_meal_logs_round_trip()
    {
        await Assign();
        var today = DateOnly.FromDateTime(Clock.Now.UtcDateTime);
        await Use(Trainer, sp => sp.GetRequiredService<NutritionService>().AssignPlanAsync(
            new(ClientId, "Synthetic SQL diet", today, today.AddDays(7), [new("Meal", "Lunch", 450.25m)])));
        await Use(Client, sp => sp.GetRequiredService<NutritionService>().LogAsync(new("Meal", 450.25m, Clock.Now.UtcDateTime)));
        var plans = await Use(Client, sp => sp.GetRequiredService<NutritionService>().PlansAsync(ClientId, 1, 20));
        Assert.Equal(450.25m, Assert.Single(Assert.Single(plans).Meals).Calories);
        var logs = await Use(Client, sp => sp.GetRequiredService<NutritionService>().LogsAsync(ClientId, Clock.Now.UtcDateTime.AddDays(-1), Clock.Now.UtcDateTime.AddDays(1), 1, 20));
        Assert.Equal(450.25m, Assert.Single(logs).Calories);
    }

    [SqlServerFact]
    public async Task Health_measurements_preserve_precision_utc_and_check_constraint()
    {
        await Use(Client, sp => sp.GetRequiredService<HealthService>().RecordAsync(new(70.25m, 175, Clock.Now.UtcDateTime)));
        var rows = await Use(Client, sp => sp.GetRequiredService<HealthService>().HistoryAsync(ClientId, Clock.Now.UtcDateTime.AddDays(-1), Clock.Now.UtcDateTime.AddDays(1), 1, 20));
        Assert.Equal(70.25m, Assert.Single(rows).WeightKg);
        Assert.Equal(DateTimeKind.Utc, rows[0].RecordedAtUtc.Kind);
        await Assert.ThrowsAsync<DbUpdateException>(() => Use(null, async sp =>
        {
            var db = sp.GetRequiredService<HealthTrackDbContext>();
            db.HealthMeasurements.Add(new HealthMeasurement { ClientUserId = ClientId, WeightKg = -1, RecordedAtUtc = Clock.Now.UtcDateTime });
            await db.SaveChangesAsync();
        }));
    }

    [SqlServerFact]
    public async Task Concurrent_booking_requests_on_separate_connections_allow_one_reservation()
    {
        await Assign();
        await Use(Admin, sp => sp.GetRequiredService<AssignmentService>().AssignAsync(new(TrainerId, OtherClientId)));
        var start = Clock.Now.UtcDateTime.AddDays(2);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Book(string client)
        {
            await gate.Task;
            try { await Use(new(client, [Roles.Client]), sp => sp.GetRequiredService<AppointmentService>().RequestAsync(new(TrainerId, start, start.AddHours(1)))); return true; }
            catch (RequestFailure error) when (error.StatusCode == 409) { return false; }
        }
        var first = Book(ClientId); var second = Book(OtherClientId); gate.SetResult();
        Assert.Single(await Task.WhenAll(first, second), x => x);
        var count = await Use(null, sp => sp.GetRequiredService<HealthTrackDbContext>().Appointments.CountAsync(x => x.TrainerUserId == TrainerId));
        Assert.Equal(1, count);
    }

    [SqlServerFact]
    public async Task Reminders_survive_new_scope_and_deliver_only_once()
    {
        var id = await Use(Client, sp => sp.GetRequiredService<NotificationService>().ScheduleAsync(new(ReminderKind.Meal, Clock.Now.UtcDateTime.AddMinutes(1), Guid.NewGuid())));
        Clock.Now = Clock.Now.AddMinutes(2);
        await Use(null, sp => sp.GetRequiredService<ReminderProcessor>().ProcessAsync());
        await Use(null, sp => sp.GetRequiredService<ReminderProcessor>().ProcessAsync());
        var notifications = await Use(Client, sp => sp.GetRequiredService<NotificationService>().ListAsync(true, 1, 20));
        var notification = Assert.Single(notifications);
        await Use(Client, sp => sp.GetRequiredService<NotificationService>().ReadAsync(notification.Id));
        Assert.Empty(await Use(Client, sp => sp.GetRequiredService<NotificationService>().ListAsync(true, 1, 20)));
        var delivered = await Use(null, sp => sp.GetRequiredService<HealthTrackDbContext>().NotificationSchedules.Where(x => x.Id == id).Select(x => x.DeliveredAtUtc).SingleAsync());
        Assert.NotNull(delivered);
    }

    [SqlServerFact]
    public async Task Reports_execute_grouped_and_correlated_sql_queries()
    {
        await Assign();
        await Use(Client, sp => sp.GetRequiredService<WorkoutService>().LogAsync(new(null, Clock.Now.UtcDateTime)));
        await Use(Client, sp => sp.GetRequiredService<NutritionService>().LogAsync(new("Synthetic meal", 123.45m, Clock.Now.UtcDateTime)));
        await Use(Client, sp => sp.GetRequiredService<HealthService>().RecordAsync(new(70, 175, Clock.Now.UtcDateTime)));
        var from = Clock.Now.UtcDateTime.AddDays(-1); var to = Clock.Now.UtcDateTime.AddDays(1);
        var report = await Use(Client, sp => sp.GetRequiredService<ReportingService>().ProgressAsync(ClientId, from, to));
        Assert.Equal(123.45m, Assert.Single(report.Calories).RecordedCalories);
        Assert.Equal(70m, Assert.Single(report.Weight).AverageWeightKg);
        Assert.Equal(1, Assert.Single(report.Workouts).CompletedWorkouts);
        Assert.Equal(1, (await Use(Trainer, sp => sp.GetRequiredService<ReportingService>().TrainerAsync(1, 20))).ActiveClients);
        Assert.Equal(1, (await Use(Client, sp => sp.GetRequiredService<ReportingService>().ClientAsync())).WorkoutsLast30Days);
        await Use(Trainer, sp => sp.GetRequiredService<ReportingService>().TrainerReportAsync(from, to));
        await Use(Admin, sp => sp.GetRequiredService<ReportingService>().AdminAsync(from, to));
    }
}
