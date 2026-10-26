namespace HealthTrack.Domain.Auditing;

public sealed class AuditLog
{
    public int Id { get; set; }
    public string ActorUserId { get; set; } = null!;
    public string SubjectUserId { get; set; } = null!;
    public string Action { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}
