using System.Data;
using HealthTrack.Application.Persistence;
using HealthTrack.Application.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HealthTrack.Infrastructure.Persistence;

public sealed class DataSession(HealthTrackDbContext db) : IDataSession
{
    public IQueryable<T> Query<T>(bool tracking = false) where T : class => tracking ? db.Set<T>() : db.Set<T>().AsNoTracking();
    public Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken ct = default) => query.ToListAsync(ct);
    public Task<T?> SingleAsync<T>(IQueryable<T> query, CancellationToken ct = default) => query.SingleOrDefaultAsync(ct);
    public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct = default) => query.AnyAsync(ct);
    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct = default) => query.CountAsync(ct);
    public void Add<T>(T entity) where T : class => db.Set<T>().Add(entity);
    public void Remove<T>(T entity) where T : class => db.Set<T>().Remove(entity);
    public async Task SaveAsync(CancellationToken ct = default) => await db.SaveChangesAsync(ct);

    public async Task<T> SerializeAsync<T>(IReadOnlyCollection<string> resources, Func<Task<T>> action, CancellationToken ct = default)
    {
        if (resources.Count == 0 || resources.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 200))
            throw new ArgumentException("A bounded lock resource is required.", nameof(resources));
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // Sorted transaction-owned SQL Server application locks serialize writes across processes.
        foreach (var resource in resources.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock " +
                "@Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; SELECT @result;";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@resource";
            parameter.Value = "HealthTrack:" + resource;
            command.Parameters.Add(parameter);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) < 0) throw RequestFailure.Conflict();
        }
        var result = await action();
        await transaction.CommitAsync(ct);
        return result;
    }
}
