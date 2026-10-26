using HealthTrack.Application.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Web;

public sealed class SafeExceptionMiddleware(RequestDelegate next, ILogger<SafeExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { context.Response.StatusCode = 499; }
        catch (Exception error)
        {
            if (context.Response.HasStarted) throw;
            var (status, code, message) = error switch
            {
                RequestFailure failure => (failure.StatusCode, failure.Code, failure.Message),
                DbUpdateConcurrencyException => (409, "conflict", "The record changed. Reload and try again."),
                DbUpdateException => (409, "persistence_conflict", "The operation conflicts with existing data."),
                SqlException => (503, "database_unavailable", "The database is unavailable."),
                InvalidOperationException invalid when invalid.Message.StartsWith("Configure ConnectionStrings:", StringComparison.Ordinal)
                    => (503, "database_unconfigured", "Database configuration is required."),
                _ => (500, "server_error", "The request could not be completed.")
            };
            if (status >= 500) logger.LogError("Request failed with category {Category}.", code);
            await Results.Problem(statusCode: status, title: message,
                extensions: new Dictionary<string, object?> { ["code"] = code }).ExecuteAsync(context);
        }
    }
}
