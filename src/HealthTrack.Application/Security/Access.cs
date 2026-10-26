namespace HealthTrack.Application.Security;

public static class Roles
{
    public const string Client = "Client";
    public const string Trainer = "Trainer";
    public const string Admin = "Admin";
    public static readonly IReadOnlyList<string> All = Array.AsReadOnly(new[] { Client, Trainer, Admin });
}

public sealed record Actor(string UserId, IReadOnlyCollection<string> Roles)
{
    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.Ordinal);
}

public interface ICurrentActor
{
    Task<Actor?> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class RequestFailure(string code, int statusCode, string message) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public static RequestFailure Invalid(string message) => new("invalid_request", 400, message);
    public static RequestFailure Forbidden() => new("forbidden", 403, "Access denied.");
    public static RequestFailure Missing() => new("not_found", 404, "Record not found.");
    public static RequestFailure Conflict() => new("conflict", 409, "The operation conflicts with the current record state.");
}

public static class Input
{
    public static void Decimal(decimal value, decimal minimum, decimal maximum, string field)
    {
        if (value < minimum || value > maximum || decimal.Round(value, 2) != value)
            throw RequestFailure.Invalid($"{field} is outside its allowed range or has more than two decimal places.");
    }

    public static void EventTime(DateTime value, TimeProvider clock)
    {
        if (value.Kind != DateTimeKind.Utc || value < DateTime.UnixEpoch || value > clock.GetUtcNow().UtcDateTime.AddMinutes(5))
            throw RequestFailure.Invalid("Event time must be UTC, after 1970, and not in the future.");
    }

    public static void DateRange(DateTime from, DateTime to)
    {
        if (from.Kind != DateTimeKind.Utc || to.Kind != DateTimeKind.Utc || to <= from || (to - from).TotalDays > 3660)
            throw RequestFailure.Invalid("Use an increasing UTC date range of at most ten years.");
    }

    public static string Text(string? value, int maximum, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximum)
            throw RequestFailure.Invalid($"{field} is required and must not exceed {maximum} characters.");
        return value.Trim();
    }

    public static void Page(int page, int size)
    {
        if (page < 1 || page > 100000 || size < 1 || size > 100)
            throw RequestFailure.Invalid("Page must be 1–100000 and page size must be 1–100.");
    }
}
