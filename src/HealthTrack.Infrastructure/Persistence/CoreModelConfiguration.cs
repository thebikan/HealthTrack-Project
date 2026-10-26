using HealthTrack.Domain.Appointments;
using HealthTrack.Domain.Auditing;
using HealthTrack.Domain.Health;
using HealthTrack.Domain.Notifications;
using HealthTrack.Domain.Nutrition;
using HealthTrack.Domain.Profiles;
using HealthTrack.Domain.Workouts;
using HealthTrack.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthTrack.Infrastructure.Persistence;

internal static class CoreModelConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        Profiles(model);
        Workouts(model);
        WorkoutLibrary(model);
        Nutrition(model);
        Health(model);
        Appointments(model);
        Notifications(model);
        var goal = model.Entity<FitnessGoal>();
        Client(goal);
        goal.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        goal.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        goal.Property(x => x.Baseline).HasPrecision(6, 2);
        goal.Property(x => x.Target).HasPrecision(6, 2);
        goal.Property(x => x.CompletedValue).HasPrecision(12, 2);
        goal.Property(x => x.RowVersion).IsRowVersion();
        goal.HasIndex(x => new { x.ClientUserId, x.Status });
        goal.ToTable("FitnessGoals", t =>
        {
            t.HasCheckConstraint("CK_Goal_Target", "[Target] > 0 AND ([Baseline] IS NULL OR [Baseline] > 0)");
            t.HasCheckConstraint("CK_Goal_Dates", "[TargetDate] >= [StartDate]");
            t.HasCheckConstraint("CK_Goal_CompletionValue", "[Status] <> N'Completed' OR [CompletedValue] IS NOT NULL");
            t.HasCheckConstraint("CK_Goal_Type", "[Type] IN (N'WeightReduction', N'WeightGain', N'WorkoutConsistency', N'WorkoutCompletion')");
            t.HasCheckConstraint("CK_Goal_Status", "([Status] = N'Completed' AND [CompletedAtUtc] IS NOT NULL) OR ([Status] IN (N'Active', N'Cancelled') AND [CompletedAtUtc] IS NULL)");
        });
        var preference = model.Entity<NotificationPreference>();
        User(preference);
        preference.HasIndex(x => x.UserId).IsUnique();
        var audit = model.Entity<AuditLog>();
        audit.Property(x => x.ActorUserId).HasMaxLength(128);
        audit.Property(x => x.SubjectUserId).HasMaxLength(128);
        audit.Property(x => x.Action).HasMaxLength(80);
        audit.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId);
        audit.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.SubjectUserId);
        audit.HasIndex(x => x.CreatedAtUtc);
    }

    private static void Profiles(ModelBuilder model)
    {
        var client = model.Entity<ClientProfile>();
        client.HasAlternateKey(x => x.UserId);
        client.Property(x => x.UserId).HasMaxLength(128);
        client.HasOne<ApplicationUser>().WithOne().HasForeignKey<ClientProfile>(x => x.UserId);
        client.Property(x => x.HeightCm).HasPrecision(6, 2);
        client.ToTable("ClientProfiles", t =>
            t.HasCheckConstraint("CK_ClientProfiles_Height", "[HeightCm] IS NULL OR [HeightCm] > 0"));

        var trainer = model.Entity<TrainerProfile>();
        trainer.HasAlternateKey(x => x.UserId);
        trainer.Property(x => x.UserId).HasMaxLength(128);
        trainer.HasOne<ApplicationUser>().WithOne().HasForeignKey<TrainerProfile>(x => x.UserId);
        trainer.Property(x => x.Specialization).HasMaxLength(200);

        var assignment = model.Entity<TrainerClientAssignment>();
        Client(assignment);
        Trainer(assignment);
        assignment.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        assignment.Property(x => x.RowVersion).IsRowVersion();
        assignment.HasIndex(x => new { x.TrainerUserId, x.ClientUserId })
            .IsUnique().HasFilter("[Status] = N'Active'");
        assignment.ToTable("TrainerClientAssignments", t =>
        {
            t.HasCheckConstraint("CK_Assignments_Participants", "[TrainerUserId] <> [ClientUserId]");
            t.HasCheckConstraint("CK_Assignments_Status",
                "([Status] = N'Active' AND [RevokedAtUtc] IS NULL) OR " +
                "([Status] = N'Revoked' AND [RevokedAtUtc] IS NOT NULL AND [RevokedAtUtc] >= [AssignedAtUtc])");
        });
    }

    private static void Workouts(ModelBuilder model)
    {
        var plan = model.Entity<WorkoutPlan>();
        Client(plan);
        Trainer(plan);
        plan.Property(x => x.Title).HasMaxLength(200);
        plan.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        plan.HasAlternateKey(x => new { x.Id, x.ClientUserId });
        plan.ToTable("WorkoutPlans", t =>
        {
            t.HasCheckConstraint("CK_WorkoutPlans_Version", "[Version] > 0");
            t.HasCheckConstraint("CK_WorkoutPlans_Dates", "[EndDate] IS NULL OR [StartDate] IS NULL OR [EndDate] >= [StartDate]");
            t.HasCheckConstraint("CK_WorkoutPlans_Status", "[Status] IN (N'Draft', N'Published', N'Archived')");
        });

        var exercise = model.Entity<WorkoutPlanExercise>();
        exercise.HasOne<WorkoutPlan>().WithMany().HasForeignKey(x => x.WorkoutPlanId);
        exercise.Property(x => x.ExerciseName).HasMaxLength(200);
        exercise.Property(x => x.DurationMinutes).HasPrecision(8, 2);
        exercise.ToTable("WorkoutPlanExercises", t =>
        {
            t.HasCheckConstraint("CK_Exercises_Sets", "[Sets] IS NULL OR [Sets] > 0");
            t.HasCheckConstraint("CK_Exercises_Reps", "[Reps] IS NULL OR [Reps] > 0");
            t.HasCheckConstraint("CK_Exercises_Duration", "[DurationMinutes] IS NULL OR [DurationMinutes] > 0");
            t.HasCheckConstraint("CK_Exercises_Target", "[Reps] IS NOT NULL OR [DurationMinutes] IS NOT NULL");
        });

        var log = model.Entity<WorkoutLog>();
        Client(log);
        log.HasOne<WorkoutPlan>().WithMany()
            .HasForeignKey(x => new { x.WorkoutPlanId, x.ClientUserId })
            .HasPrincipalKey(x => new { x.Id, x.ClientUserId });
        log.HasIndex(x => new { x.ClientUserId, x.CompletedAtUtc });
    }

    private static void WorkoutLibrary(ModelBuilder model)
    {
        var plan = model.Entity<WorkoutPlan>();
        plan.HasOne<WorkoutPlan>().WithMany().HasForeignKey(x => new { x.PreviousPlanId, x.ClientUserId })
            .HasPrincipalKey(x => new { x.Id, x.ClientUserId });
        plan.HasIndex(x => x.PreviousPlanId).IsUnique().HasFilter("[PreviousPlanId] IS NOT NULL");
        var session = model.Entity<WorkoutSession>();
        Client(session);
        session.HasOne<WorkoutPlan>().WithMany().HasForeignKey(x => new { x.WorkoutPlanId, x.ClientUserId })
            .HasPrincipalKey(x => new { x.Id, x.ClientUserId });
        session.HasAlternateKey(x => new { x.Id, x.WorkoutPlanId, x.ClientUserId });
        session.HasIndex(x => new { x.WorkoutPlanId, x.ScheduledAtUtc }).IsUnique();
        session.HasIndex(x => new { x.ClientUserId, x.ScheduledAtUtc });
        var log = model.Entity<WorkoutLog>();
        log.HasOne<WorkoutSession>().WithMany().HasForeignKey(x => new { x.SessionId, x.WorkoutPlanId, x.ClientUserId })
            .HasPrincipalKey(x => new { x.Id, x.WorkoutPlanId, x.ClientUserId });
        log.HasIndex(x => x.SessionId).IsUnique().HasFilter("[SessionId] IS NOT NULL");
        log.ToTable("WorkoutLogs", t => t.HasCheckConstraint("CK_WorkoutLogs_SessionPlan", "[SessionId] IS NULL OR [WorkoutPlanId] IS NOT NULL"));
        var template = model.Entity<WorkoutTemplate>();
        Trainer(template);
        template.Property(x => x.Title).HasMaxLength(200);
        template.HasIndex(x => new { x.TrainerUserId, x.IsArchived });
        var exercise = model.Entity<WorkoutTemplateExercise>();
        exercise.HasOne<WorkoutTemplate>().WithMany().HasForeignKey(x => x.WorkoutTemplateId);
        exercise.Property(x => x.ExerciseName).HasMaxLength(200);
        exercise.Property(x => x.DurationMinutes).HasPrecision(8, 2);
        exercise.ToTable("WorkoutTemplateExercises", t =>
        {
            t.HasCheckConstraint("CK_TemplateExercise_Target", "([Reps] IS NOT NULL OR [DurationMinutes] IS NOT NULL) AND ([Sets] IS NULL OR [Sets] > 0) AND ([Reps] IS NULL OR [Reps] > 0) AND ([DurationMinutes] IS NULL OR [DurationMinutes] > 0)");
        });
    }

    private static void Nutrition(ModelBuilder model)
    {
        var plan = model.Entity<DietPlan>();
        Client(plan);
        Trainer(plan);
        plan.Property(x => x.Title).HasMaxLength(200);
        plan.ToTable("DietPlans", t => t.HasCheckConstraint("CK_DietPlans_Dates", "[EndDate] >= [StartDate]"));

        var meal = model.Entity<DietPlanMeal>();
        meal.HasOne<DietPlan>().WithMany().HasForeignKey(x => x.DietPlanId);
        meal.Property(x => x.MealName).HasMaxLength(200);
        meal.Property(x => x.MealType).HasMaxLength(40);
        meal.Property(x => x.Calories).HasPrecision(10, 2);
        meal.ToTable("DietPlanMeals", t => t.HasCheckConstraint("CK_DietPlanMeals_Calories", "[Calories] >= 0"));

        var log = model.Entity<MealLog>();
        Client(log);
        log.Property(x => x.MealName).HasMaxLength(200);
        log.Property(x => x.Calories).HasPrecision(10, 2);
        log.HasIndex(x => new { x.ClientUserId, x.ConsumedAtUtc });
        log.ToTable("MealLogs", t => t.HasCheckConstraint("CK_MealLogs_Calories", "[Calories] >= 0"));
    }

    private static void Health(ModelBuilder model)
    {
        var measurement = model.Entity<HealthMeasurement>();
        Client(measurement);
        measurement.Property(x => x.WeightKg).HasPrecision(6, 2);
        measurement.Property(x => x.HeightCm).HasPrecision(6, 2);
        measurement.HasIndex(x => new { x.ClientUserId, x.RecordedAtUtc });
        measurement.ToTable("HealthMeasurements", t =>
        {
            t.HasCheckConstraint("CK_Measurements_Weight", "[WeightKg] > 0");
            t.HasCheckConstraint("CK_Measurements_Height", "[HeightCm] IS NULL OR [HeightCm] > 0");
        });
        var feedback = model.Entity<TrainerFeedback>();
        Client(feedback);
        Trainer(feedback);
        feedback.Property(x => x.Text).HasMaxLength(2000);
        feedback.HasIndex(x => new { x.ClientUserId, x.CreatedAtUtc });
    }

    private static void Appointments(ModelBuilder model)
    {
        const string statuses = "(N'Pending', N'Approved', N'Rejected', N'Cancelled', N'Completed')";
        var appointment = model.Entity<Appointment>();
        Client(appointment);
        Trainer(appointment);
        appointment.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        appointment.Property(x => x.RowVersion).IsRowVersion();
        appointment.HasIndex(x => new { x.TrainerUserId, x.StartAtUtc, x.Status });
        appointment.HasIndex(x => new { x.ClientUserId, x.StartAtUtc });
        appointment.ToTable("Appointments", t =>
        {
            t.HasCheckConstraint("CK_Appointments_Interval", "[EndAtUtc] > [StartAtUtc]");
            t.HasCheckConstraint("CK_Appointments_Status", $"[Status] IN {statuses}");
            t.HasCheckConstraint("CK_Appointments_Participants", "[TrainerUserId] <> [ClientUserId]");
        });
        var history = model.Entity<AppointmentStatusHistory>();
        history.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId);
        history.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ChangedByUserId);
        history.Property(x => x.ChangedByUserId).HasMaxLength(128);
        history.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(20);
        history.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        history.HasIndex(x => new { x.AppointmentId, x.ChangedAtUtc });
        history.ToTable("AppointmentStatusHistory", t =>
        {
            t.HasCheckConstraint("CK_AppointmentHistory_Status", $"[Status] IN {statuses}");
            t.HasCheckConstraint("CK_AppointmentHistory_PreviousStatus",
                $"[PreviousStatus] IS NULL OR [PreviousStatus] IN {statuses}");
        });
    }

    private static void Notifications(ModelBuilder model)
    {
        var notification = model.Entity<Notification>();
        User(notification);
        notification.Property(x => x.Title).HasMaxLength(200);
        notification.Property(x => x.Message).HasMaxLength(1000);
        notification.Property(x => x.DeliveryKey).HasMaxLength(128);
        notification.HasIndex(x => x.DeliveryKey).IsUnique().HasFilter("[DeliveryKey] IS NOT NULL");
        notification.HasIndex(x => new { x.UserId, x.ReadAtUtc });

        var schedule = model.Entity<NotificationSchedule>();
        User(schedule);
        schedule.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        schedule.Property(x => x.DeliveryKey).HasMaxLength(128);
        schedule.Property(x => x.RowVersion).IsRowVersion();
        schedule.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId);
        schedule.HasIndex(x => x.DeliveryKey).IsUnique();
        schedule.HasIndex(x => new { x.IsCancelled, x.DeliveredAtUtc, x.NextAttemptAtUtc, x.DueAtUtc });
        schedule.ToTable("NotificationSchedules", t =>
        {
            t.HasCheckConstraint("CK_NotificationSchedules_Attempts", "[Attempts] >= 0");
            t.HasCheckConstraint("CK_NotificationSchedules_Kind", "[Kind] IN (N'Workout', N'Meal', N'Appointment')");
        });
    }

    private static void Client<T>(EntityTypeBuilder<T> entity) where T : class
    {
        entity.Property<string>("ClientUserId").HasMaxLength(128).IsRequired();
        entity.HasOne<ClientProfile>().WithMany().HasForeignKey("ClientUserId").HasPrincipalKey(x => x.UserId);
    }

    private static void Trainer<T>(EntityTypeBuilder<T> entity) where T : class
    {
        entity.Property<string>("TrainerUserId").HasMaxLength(128).IsRequired();
        entity.HasOne<TrainerProfile>().WithMany().HasForeignKey("TrainerUserId").HasPrincipalKey(x => x.UserId);
    }

    private static void User<T>(EntityTypeBuilder<T> entity) where T : class
    {
        entity.Property<string>("UserId").HasMaxLength(128).IsRequired();
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey("UserId");
    }
}
