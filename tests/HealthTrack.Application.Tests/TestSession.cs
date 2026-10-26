using HealthTrack.Application.Persistence;
using HealthTrack.Application.Security;

namespace HealthTrack.Application.Tests;

// Application unit-test double only: not an EF provider, SQL transaction or persistence test.
internal sealed class TestSession : IDataSession
{
    private readonly Dictionary<Type, object> rows = [];
    public List<string> LockedResources { get; } = [];
    public bool FailNextNotificationAdd { get; set; }
    public int ListQueries { get; private set; }
    public List<T> Rows<T>() where T : class
    {
        if (!rows.TryGetValue(typeof(T), out var list)) rows[typeof(T)] = list = new List<T>();
        return (List<T>)list;
    }
    public IQueryable<T> Query<T>(bool tracking = false) where T : class => Rows<T>().AsQueryable();
    public Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken ct = default)
    { ListQueries++; return Task.FromResult(query.ToList()); }
    public Task<T?> SingleAsync<T>(IQueryable<T> query, CancellationToken ct = default) => Task.FromResult(query.SingleOrDefault());
    public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct = default) => Task.FromResult(query.Any());
    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct = default) => Task.FromResult(query.Count());
    public void Add<T>(T entity) where T : class
    {
        if (entity is HealthTrack.Domain.Notifications.Notification && FailNextNotificationAdd)
        {
            FailNextNotificationAdd = false;
            throw new InvalidOperationException("Simulated unit-test delivery failure.");
        }
        var id = typeof(T).GetProperty("Id");
        if (id?.PropertyType == typeof(int)) id.SetValue(entity, Rows<T>().Count + 1);
        var version = typeof(T).GetProperty("RowVersion");
        if (version is not null) version.SetValue(entity, new byte[8]);
        Rows<T>().Add(entity);
    }
    public void Remove<T>(T entity) where T : class => Rows<T>().Remove(entity);
    public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task<T> SerializeAsync<T>(IReadOnlyCollection<string> resources, Func<Task<T>> action, CancellationToken ct = default)
    {
        LockedResources.AddRange(resources);
        return action();
    }
}

internal sealed class MutableActor : ICurrentActor
{
    public Actor? Value { get; set; }
    public Task<Actor?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);
}

internal sealed class TestAccounts : IAccountDirectory
{
    public Task<AccountTotals> TotalsAsync(CancellationToken ct = default) => Task.FromResult(new AccountTotals(
        Active.Count, Active.Count(x => x.Item2 == Roles.Client), Active.Count(x => x.Item2 == Roles.Trainer), 0));
    public HashSet<(string, string)> Active { get; } = [("client-a", Roles.Client), ("client-b", Roles.Client),
        ("trainer-a", Roles.Trainer), ("trainer-b", Roles.Trainer), ("admin", Roles.Admin)];
    public Task<bool> IsActiveInRoleAsync(string userId, string role, CancellationToken ct = default) =>
        Task.FromResult(Active.Contains((userId, role)));
}

internal sealed class FixedClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
