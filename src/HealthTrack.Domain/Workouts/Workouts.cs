namespace HealthTrack.Domain.Workouts;

public enum PlanStatus { Draft, Published, Archived }

public sealed class WorkoutPlan
{
    public int Id { get; set; }
    public string ClientUserId { get; set; } = null!;
    public string TrainerUserId { get; set; } = null!;
    public string Title { get; set; } = null!;
    public PlanStatus Status { get; set; }
    public int Version { get; set; } = 1;
    public int? PreviousPlanId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

public sealed class WorkoutPlanExercise
{
    public int Id { get; set; }
    public int WorkoutPlanId { get; set; }
    public string ExerciseName { get; set; } = null!;
    public int? Sets { get; set; }
    public int? Reps { get; set; }
    public decimal? DurationMinutes { get; set; }
}

public sealed class WorkoutLog
{
    public int Id { get; set; }
    public string ClientUserId { get; set; } = null!;
    public int? WorkoutPlanId { get; set; }
    public int? SessionId { get; set; }
    public DateTime CompletedAtUtc { get; set; }
}

public sealed class WorkoutSession
{
    public int Id { get; set; }
    public int WorkoutPlanId { get; set; }
    public string ClientUserId { get; set; } = null!;
    public DateTime ScheduledAtUtc { get; set; }
}

public sealed class WorkoutTemplate
{
    public int Id { get; set; }
    public string TrainerUserId { get; set; } = null!;
    public string Title { get; set; } = null!;
    public bool IsArchived { get; set; }
}

public sealed class WorkoutTemplateExercise
{
    public int Id { get; set; }
    public int WorkoutTemplateId { get; set; }
    public string ExerciseName { get; set; } = null!;
    public int? Sets { get; set; }
    public int? Reps { get; set; }
    public decimal? DurationMinutes { get; set; }
}
