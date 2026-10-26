using HealthTrack.Infrastructure.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthTrack.IntegrationTests;

// Exercises the actual hosted worker with a failing scope factory, not SQL Server.
public sealed class ReminderWorkerTests
{
    [Fact]
    public async Task Disabled_worker_does_not_resolve_database_services()
    {
        var factory = new FailingScopes();
        using var worker = new ReminderWorker(factory, Options.Create(new ReminderOptions()), new RecordingLogger());
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);
        Assert.Equal(0, factory.Attempts);
    }

    [Fact]
    public async Task Worker_survives_failure_retries_and_redacts_exception_details()
    {
        var factory = new FailingScopes();
        var logger = new RecordingLogger();
        using var worker = new ReminderWorker(factory,
            Options.Create(new ReminderOptions { WorkerEnabled = true, PollIntervalSeconds = 1 }), logger);
        await worker.StartAsync(CancellationToken.None);
        try { await factory.Retried.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { await worker.StopAsync(CancellationToken.None); }
        Assert.True(factory.Attempts >= 2);
        Assert.NotEmpty(logger.Messages);
        Assert.All(logger.Messages, x => Assert.Equal("Reminder processing failed; persisted schedules will be retried.", x));
        Assert.All(logger.Exceptions, Assert.Null);
    }

    private sealed class FailingScopes : IServiceScopeFactory
    {
        public int Attempts;
        public TaskCompletionSource Retried { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IServiceScope CreateScope()
        {
            if (Interlocked.Increment(ref Attempts) >= 2) Retried.TrySetResult();
            throw new InvalidOperationException("Synthetic private connection details must not be logged");
        }
    }

    private sealed class RecordingLogger : ILogger<ReminderWorker>
    {
        public List<string> Messages { get; } = [];
        public List<Exception?> Exceptions { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        { Messages.Add(formatter(state, exception)); Exceptions.Add(exception); }
    }
}
