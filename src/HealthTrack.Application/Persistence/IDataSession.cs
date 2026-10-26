namespace HealthTrack.Application.Persistence;

// Minimal context port keeps EF-specific APIs in Infrastructure; no per-entity repositories.
public interface IDataSession
{
    IQueryable<T> Query<T>(bool tracking = false) where T : class;
    Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken ct = default);
    Task<T?> SingleAsync<T>(IQueryable<T> query, CancellationToken ct = default);
    Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct = default);
    Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct = default);
    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
    Task SaveAsync(CancellationToken ct = default);
    Task<T> SerializeAsync<T>(IReadOnlyCollection<string> resources, Func<Task<T>> action, CancellationToken ct = default);
}

public interface IAccountDirectory
{
    Task<bool> IsActiveInRoleAsync(string userId, string role, CancellationToken ct = default);
    Task<AccountTotals> TotalsAsync(CancellationToken ct = default);
}

public sealed record AccountTotals(int Total, int ActiveClients, int ActiveTrainers, int Inactive);
