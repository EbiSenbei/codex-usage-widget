using CodexUsageWidget.Application;
using CodexUsageWidget.Domain;
using CodexUsageWidget.Localization;

namespace CodexUsageWidget.Views.ViewModels;

public sealed class ClaudeUsageViewModel
{
    public string StatusText { get; init; } = Strings.Get("Status_Connecting");
    public string UpdatedText { get; init; } = Strings.Get("Claude_RefreshInterval");
    public IReadOnlyList<UsageLimitViewModel> Limits { get; init; } = [];

    public static ClaudeUsageViewModel Create(
        UsageSnapshot? snapshot, string? errorKey,
        TimeFormatPreference timeFormatPreference = TimeFormatPreference.Automatic) => new()
    {
        StatusText = errorKey is not null
            ? Strings.Get(errorKey)
            : snapshot is null ? Strings.Get("Status_Connecting") : Strings.Get(
                snapshot.RateLimits.PlanType == "preview" ? "Claude_Preview" : "Claude_Live"),
        UpdatedText = snapshot is null
            ? Strings.Get("Claude_RefreshInterval")
            : Strings.Format(errorKey is null ? "Claude_Updated" : "Claude_LastKnown",
                TimeTextFormatter.FormatTimeWithSeconds(snapshot.FetchedAt, timeFormatPreference)),
        Limits = snapshot?.GeneralWindows.Select(window => new UsageLimitViewModel(
            UsageLabelLocalizer.Localize(window.Label), window, timeFormatPreference)).ToArray() ?? []
    };
}
