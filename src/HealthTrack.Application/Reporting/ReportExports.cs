using System.Globalization;
using System.Text;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Workouts;

namespace HealthTrack.Application.Reporting;

public sealed record AdherenceReport(int ScheduledSessionsDue, int CompletedSessions, decimal? AdherencePercent);
public sealed record CsvFile(byte[] Content, string FileName);
public static class SafeCsv
{
    public static string Cell(string? value)
    {
        value ??= "";
        // Spreadsheet engines may ignore leading whitespace/control characters before formulas.
        var first = value.TrimStart();
        if ((first.Length > 0 && "=+-@".Contains(first[0])) || value.Any(c => c is '\t' or '\r' or '\n')) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
    public static byte[] Encode(IEnumerable<IReadOnlyList<string>> rows)
    {
        var output = new StringBuilder(); var count = 0;
        foreach (var row in rows)
        {
            if (++count > 11000 || row.Any(x => x.Length > 2000)) throw RequestFailure.Invalid("Export exceeds the permitted size. Narrow the date filter.");
            output.AppendJoin(',', row.Select(Cell)); output.Append("\r\n");
        }
        var encoding = new UTF8Encoding(true);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(output.ToString())];
    }
}

public sealed partial class ReportingService
{
    public async Task<AdherenceReport> AdherenceAsync(string clientId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        await access.ReadAsync(clientId, ct); Input.DateRange(from, to);
        var now = clock.GetUtcNow().UtcDateTime;
        var plans = data.Query<WorkoutPlan>().Where(x => x.ClientUserId == clientId && x.Status != PlanStatus.Draft).Select(x => x.Id);
        var sessions = data.Query<WorkoutSession>().Where(x => x.ClientUserId == clientId && plans.Contains(x.WorkoutPlanId) &&
            x.ScheduledAtUtc >= from && x.ScheduledAtUtc < to && x.ScheduledAtUtc <= now).Select(x => x.Id);
        var due = await data.CountAsync(sessions, ct);
        var completed = await data.CountAsync(data.Query<WorkoutLog>().Where(x => x.ClientUserId == clientId && x.SessionId.HasValue &&
            sessions.Contains(x.SessionId.Value) && x.CompletedAtUtc <= now).Select(x => x.SessionId).Distinct(), ct);
        return new(due, completed, due == 0 ? null : Math.Round(Math.Min(100, 100m * completed / due), 2));
    }
    public async Task<CsvFile> ProgressCsvAsync(string clientId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var progress = await ProgressAsync(clientId, from, to, ct);
        var rows = new List<IReadOnlyList<string>> { new[] { "Metric", "UTC date", "Value" } };
        rows.AddRange(progress.Workouts.Select(x => new[] { "Completed workouts", x.DayUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), x.CompletedWorkouts.ToString(CultureInfo.InvariantCulture) }));
        rows.AddRange(progress.Calories.Select(x => new[] { "Recorded calories", x.DayUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), x.RecordedCalories.ToString(CultureInfo.InvariantCulture) }));
        rows.AddRange(progress.Weight.Select(x => new[] { "Average weight kg", x.DayUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), x.AverageWeightKg.ToString(CultureInfo.InvariantCulture) }));
        return new(SafeCsv.Encode(rows), "healthtrack-progress.csv");
    }
    public async Task<CsvFile> AdminCsvAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var report = await AdminAsync(from, to, ct);
        return new(SafeCsv.Encode(new[] { new[] { "Metric", "Count" },
            new[] { "Accounts (current)", report.Accounts.Total.ToString(CultureInfo.InvariantCulture) },
            new[] { "Workouts", report.Workouts.ToString(CultureInfo.InvariantCulture) },
            new[] { "Meals", report.Meals.ToString(CultureInfo.InvariantCulture) },
            new[] { "Measurements", report.Measurements.ToString(CultureInfo.InvariantCulture) },
            new[] { "Completed appointments", report.CompletedAppointments.ToString(CultureInfo.InvariantCulture) } }), "healthtrack-system.csv");
    }
    public async Task<CsvFile> TrainerCsvAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var report = await TrainerReportAsync(from, to, ct);
        return new(SafeCsv.Encode(new[] { new[] { "Metric", "Count" },
            new[] { "Active clients (current)", report.ActiveClients.ToString(CultureInfo.InvariantCulture) },
            new[] { "Workout plans", report.WorkoutPlans.ToString(CultureInfo.InvariantCulture) },
            new[] { "Diet plans", report.DietPlans.ToString(CultureInfo.InvariantCulture) },
            new[] { "Feedback entries", report.FeedbackEntries.ToString(CultureInfo.InvariantCulture) },
            new[] { "Completed appointments", report.CompletedAppointments.ToString(CultureInfo.InvariantCulture) } }), "healthtrack-trainer.csv");
    }
}
