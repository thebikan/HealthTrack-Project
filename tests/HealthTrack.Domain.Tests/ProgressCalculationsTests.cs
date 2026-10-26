using HealthTrack.Domain.Health;

namespace HealthTrack.Domain.Tests;

public sealed class ProgressCalculationsTests
{
    [Fact]
    public void Missing_denominator_is_unknown_not_zero() => Assert.Null(ProgressCalculations.PlanCoverage(0, 0));
    [Theory]
    [InlineData(1, 2, 50)]
    [InlineData(1, 3, 33.33)]
    [InlineData(4, 2, 100)]
    public void Coverage_is_bounded_percentage(int logged, int assigned, double expected) =>
        Assert.Equal((decimal)expected, ProgressCalculations.PlanCoverage(logged, assigned));
}
