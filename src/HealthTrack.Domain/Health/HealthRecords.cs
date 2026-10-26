namespace HealthTrack.Domain.Health;

public sealed class HealthMeasurement
{
    public int Id { get; set; }
    public string ClientUserId { get; set; } = null!;
    public decimal WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}

public sealed class TrainerFeedback
{
    public int Id { get; set; }
    public string TrainerUserId { get; set; } = null!;
    public string ClientUserId { get; set; } = null!;
    public string Text { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}
