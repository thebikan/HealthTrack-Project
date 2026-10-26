namespace HealthTrack.Domain.Profiles;

public sealed class ClientProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public decimal? HeightCm { get; set; }
}

public sealed class TrainerProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Specialization { get; set; } = string.Empty;
}

public enum AssignmentStatus { Active, Revoked }

public sealed class TrainerClientAssignment
{
    public int Id { get; set; }
    public string TrainerUserId { get; set; } = null!;
    public string ClientUserId { get; set; } = null!;
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Active;
    public DateTime AssignedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
