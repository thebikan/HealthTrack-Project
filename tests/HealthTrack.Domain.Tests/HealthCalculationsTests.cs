using HealthTrack.Domain.Health;

namespace HealthTrack.Domain.Tests;

public sealed class HealthCalculationsTests
{
    [Theory]
    [InlineData(70, 175, 22.86)]
    [InlineData(80, 200, 20)]
    [InlineData(50, 160, 19.53)]
    public void Bmi_converts_centimeters_to_meters(double weight, double height, double expected) =>
        Assert.Equal((decimal)expected, HealthCalculations.BodyMassIndex((decimal)weight, (decimal)height));

    [Fact]
    public void Missing_height_does_not_invent_bmi() => Assert.Null(HealthCalculations.BodyMassIndex(70, null));

    [Theory]
    [InlineData(0, 175)]
    [InlineData(70, 0)]
    [InlineData(-1, 175)]
    [InlineData(70, -1)]
    public void Invalid_measurements_are_rejected(double weight, double height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => HealthCalculations.BodyMassIndex((decimal)weight, (decimal)height));
}
