using System.Diagnostics;
using HealthTrack.Application.Reporting;
using HealthTrack.Domain.Nutrition;
using HealthTrack.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace HealthTrack.IntegrationTests.SqlServer;

[Trait("Category", "SqlServer"), Trait("Purpose", "PerformanceObservation")]
public sealed class SqlServerPerformanceTests(ITestOutputHelper output) : SqlTestEnvironment
{
    [SqlServerFact]
    public async Task Observe_grouped_report_with_one_thousand_synthetic_meals()
    {
        await Use(Client, async sp =>
        {
            var db = sp.GetRequiredService<HealthTrackDbContext>();
            db.MealLogs.AddRange(Enumerable.Range(0, 1000).Select(_ => new MealLog
            { ClientUserId = ClientId, MealName = "Synthetic benchmark meal", Calories = 1, ConsumedAtUtc = Clock.Now.UtcDateTime }));
            await db.SaveChangesAsync();
        });
        var watch = Stopwatch.StartNew();
        var result = await Use(Client, sp => sp.GetRequiredService<ReportingService>()
            .ProgressAsync(ClientId, Clock.Now.UtcDateTime.AddDays(-1), Clock.Now.UtcDateTime.AddDays(1)));
        watch.Stop();
        Assert.Equal(1000m, Assert.Single(result.Calories).RecordedCalories);
        output.WriteLine("Observed grouped report duration for 1000 synthetic rows: {0} ms. No SLA or baseline comparison asserted.", watch.ElapsedMilliseconds);
    }
}
