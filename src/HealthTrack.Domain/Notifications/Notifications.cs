namespace HealthTrack.Domain.Notifications;

public enum ReminderKind { Workout, Meal, Appointment }

public sealed class NotificationPreference
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public bool WorkoutReminders { get; set; } = true;
    public bool MealReminders { get; set; } = true;
    public bool AppointmentReminders { get; set; } = true;
}

public sealed class Notification
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public DateTime DueAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public string? DeliveryKey { get; set; }
}

public sealed class NotificationSchedule
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public ReminderKind Kind { get; set; }
    public DateTime DueAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public bool IsCancelled { get; set; }
    public int? AppointmentId { get; set; }
    public int Attempts { get; set; }
    public string DeliveryKey { get; set; } = null!;
    public byte[] RowVersion { get; set; } = [];
}
