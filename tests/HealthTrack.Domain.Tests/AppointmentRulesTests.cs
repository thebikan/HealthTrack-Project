using HealthTrack.Domain.Appointments;

namespace HealthTrack.Domain.Tests;

public sealed class AppointmentRulesTests
{
    [Theory]
    [InlineData(AppointmentStatus.Pending, AppointmentStatus.Approved, true)]
    [InlineData(AppointmentStatus.Pending, AppointmentStatus.Rejected, true)]
    [InlineData(AppointmentStatus.Pending, AppointmentStatus.Cancelled, true)]
    [InlineData(AppointmentStatus.Approved, AppointmentStatus.Completed, true)]
    [InlineData(AppointmentStatus.Approved, AppointmentStatus.Cancelled, true)]
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.Pending, false)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Approved, false)]
    [InlineData(AppointmentStatus.Rejected, AppointmentStatus.Pending, false)]
    [InlineData(AppointmentStatus.Pending, AppointmentStatus.Completed, false)]
    public void Lifecycle_enforces_valid_transitions(AppointmentStatus from, AppointmentStatus to, bool expected) =>
        Assert.Equal(expected, AppointmentRules.CanTransition(from, to));

    [Fact]
    public void Touching_intervals_do_not_overlap()
    {
        var start = DateTime.UtcNow;
        Assert.False(AppointmentRules.Overlaps(start, start.AddHours(1), start.AddHours(1), start.AddHours(2)));
        Assert.True(AppointmentRules.Overlaps(start, start.AddHours(1), start.AddMinutes(30), start.AddHours(2)));
    }
}
