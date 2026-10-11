using System.Text.Json;
using CodexUsageWidget.Infrastructure.Claude;

namespace CodexUsageWidget.Tests;

public sealed class ClaudeUsageParserTests
{
    [Fact]
    public void ReadsBothWindowsWithPercentagesAndUtcResets()
    {
        using var json = JsonDocument.Parse("""
            {"five_hour":{"utilization":42.5,"resets_at":"2030-08-31T14:00:00Z"},
             "seven_day":{"utilization":67,"resets_at":"2030-09-04T14:00:00Z"},
             "seven_day_sonnet":{"utilization":12}}
            """);
        var fetchedAt = DateTimeOffset.UtcNow;
        var result = ClaudeUsageParser.Parse(json.RootElement, fetchedAt);
        Assert.Equal(fetchedAt, result.FetchedAt);
        Assert.Collection(result.GeneralWindows,
            window =>
            {
                Assert.Equal(300, window.WindowDurationMinutes);
                Assert.Equal(42.5, window.UsedPercent);
                Assert.Equal(57.5, window.RemainingPercent);
                Assert.Equal(new DateTimeOffset(2030, 8, 31, 14, 0, 0, TimeSpan.Zero), window.ResetsAt);
            },
            window =>
            {
                Assert.Equal(10_080, window.WindowDurationMinutes);
                Assert.Equal(67, window.UsedPercent);
            });
    }

    [Fact]
    public void MissingWeeklyWindowDoesNotInventZeroUsage()
    {
        using var json = JsonDocument.Parse("""{"five_hour":{"utilization":0},"seven_day":null}""");
        var window = Assert.Single(ClaudeUsageParser.Parse(json.RootElement, DateTimeOffset.UtcNow).GeneralWindows);
        Assert.Equal(0, window.UsedPercent);
        Assert.Null(window.ResetsAt);
    }

    [Theory]
    [InlineData("[]", "Claude_InvalidResponse")]
    [InlineData("{}", "Claude_NoLimits")]
    [InlineData("{\"five_hour\":{\"utilization\":\"secret\"}}", "Claude_InvalidResponse")]
    [InlineData("{\"five_hour\":{\"utilization\":-1}}", "Claude_InvalidResponse")]
    [InlineData("{\"five_hour\":{\"utilization\":10,\"resets_at\":\"bad\"}}", "Claude_InvalidResponse")]
    public void UnsupportedResponsesProduceSafeErrors(string payload, string expected)
    {
        using var json = JsonDocument.Parse(payload);
        var error = Assert.Throws<InvalidOperationException>(() => ClaudeUsageParser.Parse(json.RootElement, DateTimeOffset.UtcNow));
        Assert.Equal(expected, error.Message);
    }
}
