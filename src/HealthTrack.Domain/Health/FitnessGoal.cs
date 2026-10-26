namespace HealthTrack.Domain.Health;

public enum GoalType { WeightReduction, WeightGain, WorkoutConsistency, WorkoutCompletion }
public enum GoalStatus { Active, Completed, Cancelled }
public sealed class FitnessGoal
{
    public int Id { get; set; }
    public string ClientUserId { get; set; } = null!;
    public GoalType Type { get; set; }
    public GoalStatus Status { get; set; }
    public decimal? Baseline { get; set; }
    public decimal Target { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly TargetDate { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public decimal? CompletedValue { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public static class GoalCalculations
{
    public static decimal? Percent(GoalType type, decimal? baseline, decimal target, decimal? current)
    {
        if (!Enum.IsDefined(type) || target <= 0) throw new ArgumentOutOfRangeException(nameof(target));
        if (type is GoalType.WeightGain or GoalType.WeightReduction)
        {
            if (!baseline.HasValue || !current.HasValue) return null;
            if (baseline <= 0 || current <= 0 || (type == GoalType.WeightGain ? target <= baseline : target >= baseline))
                throw new ArgumentOutOfRangeException(nameof(baseline));
            return Math.Round(Math.Clamp((current.Value - baseline.Value) / (target - baseline.Value) * 100, 0, 100), 2);
        }
        return current.HasValue ? Math.Round(Math.Clamp(current.Value / target * 100, 0, 100), 2) : null;
    }
}
