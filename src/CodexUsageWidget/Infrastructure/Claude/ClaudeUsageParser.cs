using System.Globalization;
using System.Text.Json;
using CodexUsageWidget.Domain;

namespace CodexUsageWidget.Infrastructure.Claude;

public static class ClaudeUsageParser
{
    public static UsageSnapshot Parse(JsonElement payload, DateTimeOffset fetchedAt)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Claude_InvalidResponse");
        }

        var windows = new List<UsageWindow>();
        AddWindow(payload, "five_hour", "5h limit", 300, windows);
        AddWindow(payload, "seven_day", "Weekly limit", 10_080, windows);
        if (windows.Count == 0)
        {
            throw new InvalidOperationException("Claude_NoLimits");
        }

        return new UsageSnapshot(
            new UsageRateLimits(
                [new UsageLimitBucket("claude", "Claude Code", true, windows,
                    null, null, null, null)],
                null, null),
            null, fetchedAt);
    }

    private static void AddWindow(
        JsonElement payload, string key, string label, long duration,
        List<UsageWindow> windows)
    {
        if (!payload.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("utilization", out var utilization) ||
            utilization.ValueKind != JsonValueKind.Number ||
            !utilization.TryGetDouble(out var used) || !double.IsFinite(used) || used < 0 || used > 100)
        {
            throw new InvalidOperationException("Claude_InvalidResponse");
        }

        DateTimeOffset? resetsAt = null;
        if (value.TryGetProperty("resets_at", out var reset) && reset.ValueKind == JsonValueKind.String)
        {
            if (!DateTimeOffset.TryParse(reset.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var parsed))
            {
                throw new InvalidOperationException("Claude_InvalidResponse");
            }

            resetsAt = parsed;
        }

        windows.Add(new UsageWindow(label, used, duration, resetsAt));
    }
}
