namespace HealthTrack.Domain.Health;

public static class HealthCalculations
{
    public static decimal? BodyMassIndex(decimal weightKg, decimal? heightCm)
    {
        if (weightKg is <= 0 or > 9999.99m) throw new ArgumentOutOfRangeException(nameof(weightKg));
        if (!heightCm.HasValue) return null;
        if (heightCm.Value is < 0.01m or > 9999.99m) throw new ArgumentOutOfRangeException(nameof(heightCm));
        var heightMeters = heightCm.Value / 100m;
        return decimal.Round(weightKg / (heightMeters * heightMeters), 2, MidpointRounding.AwayFromZero);
    }
}
