namespace HealthTrack.Domain.Appointments;

public static class AppointmentRules
{
    public static bool CanTransition(AppointmentStatus from, AppointmentStatus to) => (from, to) switch
    {
        (AppointmentStatus.Pending, AppointmentStatus.Approved or AppointmentStatus.Rejected or AppointmentStatus.Cancelled) => true,
        (AppointmentStatus.Approved, AppointmentStatus.Cancelled or AppointmentStatus.Completed) => true,
        _ => false
    };

    public static bool Overlaps(DateTime start, DateTime end, DateTime otherStart, DateTime otherEnd) =>
        start < otherEnd && end > otherStart;
}
