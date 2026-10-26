using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using HealthTrack.Application.Persistence;
using HealthTrack.Application.Security;
using HealthTrack.Application.Tests;
using HealthTrack.Domain.Profiles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthTrack.IntegrationTests;

// Real MVC routing, CSRF, role filters and services; synthetic authentication/data doubles.
// Does NOT establish Identity login, SQL persistence, transactions or concurrency.
public sealed class BackendJourneyTests
{
    [Fact]
    public async Task Diet_edit_goals_preferences_and_export_http_journey_enforces_ownership()
    {
        using var factory = new JourneyFactory();
        factory.Data.Add(new TrainerClientAssignment { TrainerUserId = "trainer-a", ClientUserId = "client-a" });
        factory.Data.Add(new ClientProfile { UserId = "client-a" });
        using var trainer = await ClientAsync(factory,"trainer-a",Roles.Trainer);
        using var client = await ClientAsync(factory,"client-a",Roles.Client);
        using var other = await ClientAsync(factory,"client-b",Roles.Client);
        var diet = await CreateAsync(trainer,"/api/nutrition/plans",new { clientUserId="client-a",title="Synthetic diet",startDate="2026-10-21",endDate="2026-11-21",meals=new[] {new {name="Lunch",mealType="Lunch",calories=400,plannedTimeUtc="12:00:00"}} });
        using var edit = await trainer.PutAsJsonAsync($"/api/nutrition/plans/{diet}",new { clientUserId="client-a",title="Edited diet",startDate="2026-10-21",endDate="2026-11-21",meals=new[] {new {name="Dinner",mealType="Dinner",calories=500}} });
        Assert.Equal(HttpStatusCode.NoContent,edit.StatusCode);
        var goal = await CreateAsync(client,"/api/goals",new {type="WorkoutCompletion",target=2,startDate="2026-10-21",targetDate="2026-11-21"});
        using var badUpdate = await other.PutAsJsonAsync($"/api/goals/{goal}",new {target=3,targetDate="2026-11-22",rowVersion=new byte[8]});
        Assert.Equal(HttpStatusCode.NotFound,badUpdate.StatusCode);
        using var preference = await client.PutAsJsonAsync("/api/notifications/preferences",new {workoutReminders=false,mealReminders=true,appointmentReminders=true});
        Assert.Equal(HttpStatusCode.NoContent,preference.StatusCode);
        Assert.True((await other.GetFromJsonAsync<JsonElement>("/api/notifications/preferences")).GetProperty("workoutReminders").GetBoolean());
        const string path="/api/reports/clients/client-a/progress.csv?from=2026-10-20T00:00:00Z&to=2026-10-22T00:00:00Z";
        using var export = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK,export.StatusCode);
        Assert.Equal("text/csv",export.Content.Headers.ContentType!.MediaType);
        Assert.True(export.Headers.CacheControl!.NoStore);
        using var denied = await other.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        using var invalid = await client.PostAsJsonAsync("/api/goals",new {type="WorkoutCompletion",target=-1,startDate="2026-10-21",targetDate="2026-11-21"});
        Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
    }

    [Fact]
    public async Task Template_routes_reject_client_role_and_preference_mutations_require_csrf()
    {
        using var factory = new JourneyFactory();
        using var client = await ClientAsync(factory,"client-a",Roles.Client);
        using var denied = await client.GetAsync("/api/workouts/templates");
        Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var csrf = await client.PutAsJsonAsync("/api/notifications/preferences",new {workoutReminders=false,mealReminders=false,appointmentReminders=false});
        Assert.Equal(HttpStatusCode.BadRequest,csrf.StatusCode);
    }
    [Fact]
    public async Task Admin_assignment_trainer_publish_client_log_and_revocation_journey()
    {
        using var factory = new JourneyFactory();
        using var admin = await ClientAsync(factory, "admin", Roles.Admin);
        using var trainer = await ClientAsync(factory, "trainer-a", Roles.Trainer);
        using var client = await ClientAsync(factory, "client-a", Roles.Client);
        using var other = await ClientAsync(factory, "client-b", Roles.Client);
        var assignment = await CreateAsync(admin, "/api/assignments", new { trainerUserId = "trainer-a", clientUserId = "client-a" });
        using var deniedAssignment = await client.PostAsJsonAsync("/api/assignments", new { trainerUserId = "trainer-a", clientUserId = "client-b" });
        Assert.Equal(HttpStatusCode.Forbidden, deniedAssignment.StatusCode);
        var plan = await CreateAsync(trainer, "/api/workouts/plans", new
        {
            clientUserId = "client-a", title = "Synthetic workout", startDate = "2026-10-21", endDate = "2026-11-21",
            exercises = new[] { new { name = "Walking", durationMinutes = 20 } }
        });
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/workouts/clients/client-a/plans")).EnumerateArray());
        using var publish = await trainer.PostAsJsonAsync($"/api/workouts/plans/{plan}/publish", new { clientUserId = "client-a" });
        Assert.Equal(HttpStatusCode.NoContent, publish.StatusCode);
        Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/workouts/clients/client-a/plans")).EnumerateArray());
        using var idor = await other.GetAsync("/api/workouts/clients/client-a/plans");
        Assert.Equal(HttpStatusCode.Forbidden, idor.StatusCode);
        await CreateAsync(client, "/api/workouts/logs", new { planId = plan, completedAtUtc = "2026-10-21T00:00:00Z" });
        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/reports/dashboard/client");
        Assert.Equal(1, dashboard.GetProperty("workoutsLast30Days").GetInt32());
        Assert.Equal(JsonValueKind.Null, dashboard.GetProperty("latestMeasurement").ValueKind);
        var progress = await client.GetFromJsonAsync<JsonElement>("/api/reports/clients/client-a/progress?from=2026-10-20T00:00:00Z&to=2026-10-22T00:00:00Z");
        Assert.Equal(100m, progress.GetProperty("currentPlanCoveragePercent").GetDecimal());
        var aggregate = await admin.GetFromJsonAsync<JsonElement>("/api/reports/dashboard/admin?from=2026-10-20T00:00:00Z&to=2026-10-22T00:00:00Z");
        Assert.Equal(1, aggregate.GetProperty("workouts").GetInt32());
        Assert.DoesNotContain("client-a", aggregate.GetRawText());
        using var revoke = await admin.PostAsJsonAsync($"/api/assignments/{assignment}/revoke", new { rowVersion = new byte[8] });
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        using var revokedRead = await trainer.GetAsync("/api/workouts/clients/client-a/plans");
        Assert.Equal(HttpStatusCode.Forbidden, revokedRead.StatusCode);
    }

    [Fact]
    public async Task Booking_approval_reminders_and_notification_ownership_journey()
    {
        using var factory = new JourneyFactory();
        factory.Data.Add(new TrainerClientAssignment { TrainerUserId = "trainer-a", ClientUserId = "client-a" });
        using var trainer = await ClientAsync(factory, "trainer-a", Roles.Trainer);
        using var client = await ClientAsync(factory, "client-a", Roles.Client);
        using var other = await ClientAsync(factory, "client-b", Roles.Client);
        var id = await CreateAsync(client, "/api/appointments", new
        { trainerUserId = "trainer-a", startAtUtc = "2026-10-22T10:00:00Z", endAtUtc = "2026-10-22T11:00:00Z" });
        using var approve = await trainer.PostAsJsonAsync($"/api/appointments/{id}/status", new { status = "Approved", rowVersion = new byte[8] });
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);
        Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/notifications/schedules")).EnumerateArray());
        var notifications = await client.GetFromJsonAsync<JsonElement>("/api/notifications?unreadOnly=true");
        Assert.Equal(2, notifications.GetArrayLength());
        var notificationId = notifications[0].GetProperty("id").GetInt32();
        using var deniedRead = await other.PostAsync($"/api/notifications/{notificationId}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, deniedRead.StatusCode);
        using var read = await client.PostAsync($"/api/notifications/{notificationId}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/notifications?unreadOnly=true")).EnumerateArray());
        using var cancel = await client.PostAsJsonAsync($"/api/appointments/{id}/status", new { status = "Cancelled", rowVersion = new byte[8] });
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        var schedules = await client.GetFromJsonAsync<JsonElement>("/api/notifications/schedules");
        Assert.True(schedules[0].GetProperty("isCancelled").GetBoolean());
    }

    private static async Task<int> CreateAsync(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync(path, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private static async Task<HttpClient> ClientAsync(JourneyFactory factory, string id, string role)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Test-User", id);
        client.DefaultRequestHeaders.Add("Test-Role", role);
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/account/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return client;
    }

    private sealed class JourneyFactory : WebApplicationFactory<Program>
    {
        public TestSession Data { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.AddSingleton<IDataSession>(Data);
            services.AddSingleton<IAccountDirectory, TestAccounts>();
            services.AddSingleton<TimeProvider, FixedClock>();
            services.AddScoped<ICurrentActor, TestActor>();
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Journey";
                options.DefaultChallengeScheme = "Journey";
                options.DefaultForbidScheme = "Journey";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthentication>("Journey", _ => { });
        });
    }

    private sealed class TestActor(IHttpContextAccessor http) : ICurrentActor
    {
        public Task<Actor?> GetAsync(CancellationToken cancellationToken = default)
        {
            var user = http.HttpContext!.User;
            return Task.FromResult<Actor?>(new Actor(user.FindFirstValue(ClaimTypes.NameIdentifier)!,
                user.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray()));
        }
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Request.Headers["Test-User"].ToString()),
                new Claim(ClaimTypes.Role, Request.Headers["Test-Role"].ToString())], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
