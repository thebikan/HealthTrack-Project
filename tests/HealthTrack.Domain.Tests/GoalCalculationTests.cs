using HealthTrack.Domain.Health;
namespace HealthTrack.Domain.Tests;
public sealed class GoalCalculationTests
{
    [Theory]
    [InlineData(GoalType.WeightReduction, 80, 70, 75, 50)]
    [InlineData(GoalType.WeightGain, 60, 70, 65, 50)]
    [InlineData(GoalType.WeightReduction, 80, 70, 85, 0)]
    [InlineData(GoalType.WeightGain, 60, 70, 75, 100)]
    [InlineData(GoalType.WorkoutConsistency, 0, 10, 4, 40)]
    [InlineData(GoalType.WorkoutCompletion, 0, 5, 7, 100)]
    public void Type_specific_progress_is_bounded(GoalType type, decimal baseline, decimal target, decimal current, decimal expected) =>
        Assert.Equal(expected, GoalCalculations.Percent(type, baseline, target, current));
    [Fact]
    public void Missing_measurements_do_not_invent_progress() => Assert.Null(GoalCalculations.Percent(GoalType.WeightGain, 60, 70, null));
    [Fact]
    public void Equal_or_wrong_direction_weight_target_is_invalid()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GoalCalculations.Percent(GoalType.WeightReduction, 70, 70, 65));
        Assert.Throws<ArgumentOutOfRangeException>(() => GoalCalculations.Percent(GoalType.WeightGain, 70, 60, 65));
        Assert.Throws<ArgumentOutOfRangeException>(() => GoalCalculations.Percent(GoalType.WorkoutCompletion, null, 0, 1));
    }
}
