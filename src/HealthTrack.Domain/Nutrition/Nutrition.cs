namespace HealthTrack.Domain.Nutrition;

public sealed class DietPlan
{
    public int Id { get; set; }
    public string ClientUserId { get; set; } = null!;
    public string TrainerUserId { get; set; } = null!;
    public string Title { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

public sealed class DietPlanMeal
{
    public int Id { get; set; }
    public int DietPlanId { get; set; }
    public string MealName { get; set; } = null!;
    public string MealType { get; set; } = null!;
    public TimeOnly? PlannedTimeUtc { get; set; }
    public decimal Calories { get; set; }
}

public sealed class MealLog
{
    public int Id { get; set; }
    public string ClientUserId { get; set; } = null!;
    public string MealName { get; set; } = null!;
    public decimal Calories { get; set; }
    public DateTime ConsumedAtUtc { get; set; }
}
