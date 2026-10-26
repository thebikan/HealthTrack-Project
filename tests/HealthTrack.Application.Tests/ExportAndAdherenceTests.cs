using System.Text;
using HealthTrack.Application.Reporting;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Workouts;
using HealthTrack.Domain.Auditing;

namespace HealthTrack.Application.Tests;
public sealed class ExportAndAdherenceTests
{
    private readonly TestSession data = new();
    private readonly MutableActor actor = new() { Value = new("client-a", [Roles.Client]) };
    private ReportingService Service => new(data, new(actor,data,new TestAccounts()),new TestAccounts(),new FixedClock());
    private static DateTime From => new(2026,10,20,0,0,0,DateTimeKind.Utc);
    [Theory]
    [InlineData("=SUM(A1)")] [InlineData("  +1")] [InlineData("-1+2")] [InlineData("@SUM(A1)")] [InlineData("\t=1")] [InlineData("\r=1")]
    public void Formula_cells_are_neutralized(string value) => Assert.StartsWith("\"'",SafeCsv.Cell(value));
    [Fact]
    public void Csv_quotes_delimiters_quotes_and_newlines_and_has_utf8_bom()
    {
        Assert.Equal("\"a,\"\"b\"\"\"",SafeCsv.Cell("a,\"b\""));
        var bytes = SafeCsv.Encode([new[] { "Metric", "Value" }, new[] { "Weight", "70.25" }]);
        Assert.Equal(new byte[] {239,187,191},bytes.Take(3));
        Assert.Contains("\r\n",Encoding.UTF8.GetString(bytes));
    }
    [Fact]
    public async Task Exports_enforce_scope_and_do_not_give_admin_personal_health_access()
    {
        var file = await Service.ProgressCsvAsync("client-a",From,From.AddDays(1));
        Assert.Equal("healthtrack-progress.csv",file.FileName);
        actor.Value = new("client-b",[Roles.Client]);
        await Assert.ThrowsAsync<RequestFailure>(() => Service.ProgressCsvAsync("client-a",From,From.AddDays(1)));
        await Assert.ThrowsAsync<RequestFailure>(() => Service.AdminCsvAsync(From,From.AddDays(1)));
        actor.Value = new("admin",[Roles.Admin]);
        await Assert.ThrowsAsync<RequestFailure>(() => Service.ProgressCsvAsync("client-a",From,From.AddDays(1)));
        Assert.DoesNotContain("client-a",Encoding.UTF8.GetString((await Service.AdminCsvAsync(From,From.AddDays(1))).Content));
    }
    [Fact]
    public async Task Adherence_uses_due_scheduled_sessions_not_plan_coverage_or_free_logs()
    {
        data.Add(new WorkoutPlan { ClientUserId = "client-a",TrainerUserId="trainer-a",Title="Test",Status=PlanStatus.Published });
        data.Add(new WorkoutSession { ClientUserId="client-a",WorkoutPlanId=1,ScheduledAtUtc=From });
        data.Add(new WorkoutSession { ClientUserId="client-a",WorkoutPlanId=1,ScheduledAtUtc=From.AddHours(1) });
        data.Add(new WorkoutSession { ClientUserId="client-a",WorkoutPlanId=1,ScheduledAtUtc=From.AddDays(2) });
        data.Add(new WorkoutLog { ClientUserId="client-a",WorkoutPlanId=1,SessionId=1,CompletedAtUtc=From.AddHours(1) });
        data.Add(new WorkoutLog { ClientUserId="client-a",CompletedAtUtc=From });
        var result = await Service.AdherenceAsync("client-a",From,From.AddDays(3));
        Assert.Equal(2,result.ScheduledSessionsDue); Assert.Equal(50m,result.AdherencePercent);
    }
    [Fact]
    public async Task No_schedule_means_no_adherence_and_audit_is_admin_only()
    {
        Assert.Null((await Service.AdherenceAsync("client-a",From,From.AddDays(1))).AdherencePercent);
        await Assert.ThrowsAsync<RequestFailure>(()=>Service.AuditAsync(From,From.AddDays(1),1,20));
        actor.Value=new("admin",[Roles.Admin]);
        data.Add(new AuditLog {ActorUserId="admin",SubjectUserId="client-a",Action="AccountDeactivated",CreatedAtUtc=From});
        Assert.Single(await Service.AuditAsync(From,From.AddDays(1),1,20,action:"AccountDeactivated"));
        Assert.Empty(await Service.AuditAsync(From,From.AddDays(1),1,20,action:"PasswordReset"));
    }
}
