namespace HealthTrack.Domain.Appointments;

public enum AppointmentStatus { Pending, Approved, Rejected, Cancelled, Completed }

public sealed class Appointment
{
    public int Id { get; set; }
    public string ClientUserId { get; set; } = null!;
    public string TrainerUserId { get; set; } = null!;
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public AppointmentStatus Status { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class AppointmentStatusHistory
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public AppointmentStatus? PreviousStatus { get; set; }
    public AppointmentStatus Status { get; set; }
    public string ChangedByUserId { get; set; } = null!;
    public DateTime ChangedAtUtc { get; set; }
}
