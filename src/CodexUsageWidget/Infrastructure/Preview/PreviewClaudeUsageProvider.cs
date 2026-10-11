using CodexUsageWidget.Application;
using CodexUsageWidget.Domain;

namespace CodexUsageWidget.Infrastructure.Preview;

public sealed class PreviewClaudeUsageProvider(IUsageProvider wrappedProvider) : IUsageProvider
{
    public event EventHandler? RateLimitsChanged { add { } remove { } }
    public event EventHandler<string>? DiagnosticMessage { add { } remove { } }

    public Task<UsageSnapshot> ReadUsageAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.Now;
        return Task.FromResult(new UsageSnapshot(new UsageRateLimits(
            [new UsageLimitBucket("claude", "Claude Code", true,
                [new UsageWindow("5h limit", 42, 300, now.AddHours(3)),
                 new UsageWindow("Weekly limit", 67, 10_080, now.AddDays(4))],
                null, null, null, null)], "preview", null), null, now));
    }

    public ValueTask DisposeAsync() => wrappedProvider.DisposeAsync();
}
