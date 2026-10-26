using HealthTrack.Application.Persistence;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Nutrition;

namespace HealthTrack.Application.Nutrition;

public sealed record PlannedMeal(string Name, string MealType, decimal Calories, TimeOnly? PlannedTimeUtc = null);
public sealed record DietPlanRequest(string ClientUserId, string Title, DateOnly StartDate, DateOnly EndDate,
    IReadOnlyList<PlannedMeal> Meals);
public sealed record DietPlanView(int Id, string Title, DateOnly StartDate, DateOnly EndDate, IReadOnlyList<PlannedMeal> Meals);
public sealed record MealLogRequest(string Name, decimal Calories, DateTime ConsumedAtUtc);
public sealed record MealLogView(int Id, string Name, decimal Calories, DateTime ConsumedAtUtc);

public sealed class NutritionService(IDataSession data, ClientAccess access, TimeProvider clock)
{
    public async Task<int> AssignPlanAsync(DietPlanRequest request, CancellationToken ct = default)
    {
        Validate(request);
        return await data.SerializeAsync(["client:" + request.ClientUserId], async () =>
        {
            var actor = await access.TrainerWriteAsync(request.ClientUserId, ct);
            var plan = new DietPlan
            {
                ClientUserId = request.ClientUserId, TrainerUserId = actor.UserId, Title = request.Title.Trim(),
                StartDate = request.StartDate, EndDate = request.EndDate
            };
            data.Add(plan);
            await data.SaveAsync(ct);
            foreach (var meal in request.Meals) data.Add(new DietPlanMeal
            {
                DietPlanId = plan.Id, MealName = meal.Name.Trim(), MealType = meal.MealType.Trim(), Calories = meal.Calories, PlannedTimeUtc = meal.PlannedTimeUtc
            });
            await data.SaveAsync(ct);
            return plan.Id;
        }, ct);
    }

    public async Task<IReadOnlyList<DietPlanView>> PlansAsync(string clientId, int page, int size, CancellationToken ct = default, DateOnly? from = null, DateOnly? to = null)
    {
        await access.ReadAsync(clientId, ct); Input.Page(page, size);
        if (from.HasValue != to.HasValue || to < from) throw RequestFailure.Invalid("Invalid diet date filter.");
        var query = data.Query<DietPlan>().Where(x => x.ClientUserId == clientId);
        if (from.HasValue) query = query.Where(x => x.StartDate <= to && x.EndDate >= from);
        var plans = await data.ListAsync(query
            .OrderByDescending(x => x.Id).Skip((page - 1) * size).Take(size), ct);
        var ids = plans.Select(x => x.Id).ToArray();
        var meals = await data.ListAsync(data.Query<DietPlanMeal>().Where(x => ids.Contains(x.DietPlanId)).OrderBy(x => x.Id), ct);
        var mealLookup = meals.ToLookup(x => x.DietPlanId);
        return plans.Select(x => new DietPlanView(x.Id, x.Title, x.StartDate, x.EndDate,
            mealLookup[x.Id].Select(m => new PlannedMeal(m.MealName, m.MealType, m.Calories, m.PlannedTimeUtc)).ToArray())).ToArray();
    }

    public async Task<int> LogAsync(MealLogRequest request, CancellationToken ct = default)
    {
        var actor = await access.ClientAsync(ct);
        var name = Input.Text(request.Name, 200, "Meal name");
        Input.Decimal(request.Calories, 0, 99999999.99m, "Calories"); Input.EventTime(request.ConsumedAtUtc, clock);
        var log = new MealLog { ClientUserId = actor.UserId, MealName = name, Calories = request.Calories, ConsumedAtUtc = request.ConsumedAtUtc };
        data.Add(log);
        await data.SaveAsync(ct);
        return log.Id;
    }

    public async Task<IReadOnlyList<MealLogView>> LogsAsync(string clientId, DateTime from, DateTime to, int page, int size, CancellationToken ct = default)
    {
        await access.ReadAsync(clientId, ct); Input.DateRange(from, to); Input.Page(page, size);
        return await data.ListAsync(data.Query<MealLog>().Where(x => x.ClientUserId == clientId && x.ConsumedAtUtc >= from && x.ConsumedAtUtc < to)
            .OrderByDescending(x => x.ConsumedAtUtc).ThenByDescending(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new MealLogView(x.Id, x.MealName, x.Calories, x.ConsumedAtUtc)), ct);
    }
    private static void Validate(DietPlanRequest request)
    {
        Input.Text(request.ClientUserId, 128, "Client ID"); Input.Text(request.Title, 200, "Title");
        if (request.StartDate.Year < 1970 || request.EndDate < request.StartDate)
            throw RequestFailure.Invalid("A valid increasing plan date range is required.");
        if (request.Meals is null || request.Meals.Count is < 1 or > 100)
            throw RequestFailure.Invalid("Supply between one and 100 planned meals.");
        foreach (var meal in request.Meals)
        {
            if (meal is null) throw RequestFailure.Invalid("Meal is required.");
            Input.Text(meal.Name, 200, "Meal name"); Input.Text(meal.MealType, 40, "Meal type");
            Input.Decimal(meal.Calories, 0, 99999999.99m, "Calories");
        }
    }

    public async Task EditAsync(int id, DietPlanRequest request, CancellationToken ct = default)
    {
        Validate(request);
        await data.SerializeAsync(["client:" + request.ClientUserId], async () =>
        {
            var actor = await access.TrainerWriteAsync(request.ClientUserId, ct);
            var plan = await data.SingleAsync(data.Query<DietPlan>(true).Where(x => x.Id == id && x.ClientUserId == request.ClientUserId && x.TrainerUserId == actor.UserId), ct) ?? throw RequestFailure.Missing();
            plan.Title = request.Title.Trim(); plan.StartDate = request.StartDate; plan.EndDate = request.EndDate;
            foreach (var meal in await data.ListAsync(data.Query<DietPlanMeal>(true).Where(x => x.DietPlanId == id), ct)) data.Remove(meal);
            foreach (var meal in request.Meals) data.Add(new DietPlanMeal { DietPlanId = id, MealName = meal.Name.Trim(), MealType = meal.MealType.Trim(), Calories = meal.Calories, PlannedTimeUtc = meal.PlannedTimeUtc });
            await data.SaveAsync(ct); return 0;
        }, ct);
    }

}
