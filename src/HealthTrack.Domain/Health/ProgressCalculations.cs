namespace HealthTrack.Domain.Health;

public static class ProgressCalculations
{
    // Coverage of current published plans with at least one log; not scheduled-session adherence.
    public static decimal? PlanCoverage(int plansWithLogs, int publishedPlans)
    {
        if (plansWithLogs < 0 || publishedPlans < 0) throw new ArgumentOutOfRangeException(nameof(plansWithLogs));
        return publishedPlans == 0 ? null : decimal.Round(Math.Min(plansWithLogs, publishedPlans) * 100m / publishedPlans, 2);
    }
}
