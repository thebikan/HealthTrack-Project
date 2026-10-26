using HealthTrack.Infrastructure.Identity;
using HealthTrack.Application.Accounts;
using HealthTrack.Application.Security;
using HealthTrack.Application.Persistence;
using HealthTrack.Application.Assignments;
using HealthTrack.Application.Workouts;
using HealthTrack.Application.Nutrition;
using HealthTrack.Application.Health;
using HealthTrack.Application.Appointments;
using HealthTrack.Application.Reporting;
using HealthTrack.Application.Notifications;
using HealthTrack.Infrastructure.Notifications;
using HealthTrack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HealthTrack.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddHealthTrackInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<HealthTrackDbContext>(options =>
        {
            var connection = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException(
                    "Configure ConnectionStrings:DefaultConnection using environment variables or development user-secrets.");
            options.UseSqlServer(connection);
            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(false);
        });

        // EF exception messages can contain database identifiers or duplicate values.
        // Use sanitized application-level diagnostics in later persistence services.
        services.AddLogging(logging => logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None));
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
            .AddEntityFrameworkStores<HealthTrackDbContext>()
            .AddSignInManager<ActiveSignInManager>()
            .AddDefaultTokenProviders();
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActor, CurrentActor>();
        services.AddScoped<IAccountGateway, IdentityAccountGateway>();
        services.AddScoped<AccountService>();
        services.AddScoped<PasswordRecoveryService>();
        services.AddScoped<IPasswordRecoveryGateway, IdentityPasswordRecovery>();
        services.AddSingleton<IPasswordRecoveryDelivery, UnconfiguredRecoveryDelivery>();
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(1));
        services.AddScoped<IDataSession, DataSession>();
        services.AddScoped<IAccountDirectory, AccountDirectory>();
        services.AddScoped<ClientAccess>();
        services.AddScoped<AssignmentService>();
        services.AddScoped<WorkoutService>();
        services.AddScoped<NutritionService>();
        services.AddScoped<HealthService>();
        services.AddScoped<GoalService>();
        services.AddScoped<AppointmentService>();
        services.AddScoped<ReportingService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<ReminderProcessor>();
        services.AddOptions<ReminderOptions>().Bind(configuration.GetSection("Notifications"))
            .Validate(x => x.PollIntervalSeconds is >= 10 and <= 3600, "Reminder polling must be between 10 and 3600 seconds.")
            .ValidateOnStart();
        services.AddHostedService<ReminderWorker>();
        services.AddScoped<IdentityInitializer>();
        services.AddScoped<ActiveAccountCookieEvents>();
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "__Host-HealthTrack";
            options.EventsType = typeof(ActiveAccountCookieEvents);
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Path = "/";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = true;
        });
        return services;
    }
}
