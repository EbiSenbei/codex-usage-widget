using CodexUsageWidget.Application;
using CodexUsageWidget.Domain;

namespace CodexUsageWidget.Tests;

public sealed class UsageMonitorTests
{
    [Fact]
    public async Task OneProviderFailureDoesNotBlockTheOtherMonitor()
    {
        var successProvider = new BlockingUsageProvider();
        successProvider.ReleaseFirstRead.TrySetResult();
        await using var codex = new UsageMonitor(successProvider);
        await using var claude = new UsageMonitor(new FailingUsageProvider(), errorFormatter: static key => key);
        var codexUpdated = false;
        string? claudeError = null;
        codex.SnapshotUpdated += _ => codexUpdated = true;
        claude.RefreshFailed += error => claudeError = error;

        await Task.WhenAll(codex.RefreshAsync(), claude.RefreshAsync());

        Assert.True(codexUpdated);
        Assert.Equal("Claude_LoginRequired", claudeError);
    }

    [Fact]
    public async Task ShutdownWaitsForAnActiveReadWithoutReportingARefreshFailure()
    {
        var provider = new CancellableUsageProvider();
        var monitor = new UsageMonitor(provider);
        var failed = false;
        monitor.RefreshFailed += _ => failed = true;
        var activeRead = monitor.RefreshAsync();
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await monitor.DisposeAsync();
        await activeRead;

        Assert.True(provider.DisposedAfterReadEnded);
        Assert.False(failed);
    }

    private sealed class FailingUsageProvider : IUsageProvider
    {
        public event EventHandler? RateLimitsChanged { add { } remove { } }
        public event EventHandler<string>? DiagnosticMessage { add { } remove { } }
        public Task<UsageSnapshot> ReadUsageAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<UsageSnapshot>(new InvalidOperationException("Claude_LoginRequired"));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CancellableUsageProvider : IUsageProvider
    {
        private bool _readEnded;
        public bool DisposedAfterReadEnded { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public event EventHandler? RateLimitsChanged { add { } remove { } }
        public event EventHandler<string>? DiagnosticMessage { add { } remove { } }
        public async Task<UsageSnapshot> ReadUsageAsync(CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Read should be cancelled");
            }
            finally { _readEnded = true; }
        }
        public ValueTask DisposeAsync()
        {
            DisposedAfterReadEnded = _readEnded;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task RefreshAfterCurrentWaitsAndReadsFreshUsage()
    {
        var provider = new BlockingUsageProvider();
        await using var monitor = new UsageMonitor(provider);

        var activeRefresh = monitor.RefreshAsync();
        await provider.FirstReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var refreshAfterCurrent = monitor.RefreshAfterCurrentAsync();
        Assert.False(refreshAfterCurrent.IsCompleted);

        provider.ReleaseFirstRead.TrySetResult();
        await Task.WhenAll(activeRefresh, refreshAfterCurrent)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, provider.ReadCount);
    }

    private sealed class BlockingUsageProvider : IUsageProvider
    {
        private int _readCount;

        public TaskCompletionSource FirstReadStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstRead { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int ReadCount => Volatile.Read(ref _readCount);

        public event EventHandler? RateLimitsChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<string>? DiagnosticMessage
        {
            add { }
            remove { }
        }

        public async Task<UsageSnapshot> ReadUsageAsync(
            CancellationToken cancellationToken = default)
        {
            var read = Interlocked.Increment(ref _readCount);
            if (read == 1)
            {
                FirstReadStarted.TrySetResult();
                await ReleaseFirstRead.Task.WaitAsync(cancellationToken);
            }

            return new UsageSnapshot(
                new UsageRateLimits([], PlanType: null, ResetCredits: null),
                TokenActivity: null,
                FetchedAt: DateTimeOffset.Now);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
