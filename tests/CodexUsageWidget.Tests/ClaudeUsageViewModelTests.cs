using System.Globalization;
using System.Text.Json;
using CodexUsageWidget.Infrastructure.Claude;
using CodexUsageWidget.Infrastructure.Settings;
using CodexUsageWidget.Localization;
using CodexUsageWidget.Views.ViewModels;

namespace CodexUsageWidget.Tests;

[Collection("Localization")]
public sealed class ClaudeUsageViewModelTests : IDisposable
{
    [Fact]
    public void JapaneseOverrideLocalizesUsageAndRetainsStaleData()
    {
        var controller = new AppLanguageController(LanguagePreference.Japanese, CultureInfo.GetCultureInfo("en-US"));
        Assert.Equal("ja-JP", Strings.Current.Culture.Name);
        using var json = JsonDocument.Parse("""{"five_hour":{"utilization":42},"seven_day":{"utilization":67}}""");
        var snapshot = ClaudeUsageParser.Parse(json.RootElement, DateTimeOffset.UtcNow);
        var viewModel = ClaudeUsageViewModel.Create(snapshot, "Claude_NetworkError");
        Assert.Equal("5時間枠", viewModel.Limits[0].Label);
        Assert.Equal("42% 使用済み", viewModel.Limits[0].UsedText);
        Assert.Equal("週間枠", viewModel.Limits[1].Label);
        Assert.Contains("前回の値", viewModel.UpdatedText);
        Assert.Contains("接続できません", viewModel.StatusText);

        controller.SetPreference(LanguagePreference.English);
        viewModel = ClaudeUsageViewModel.Create(snapshot, "Claude_NetworkError");
        Assert.Equal("5h limit", viewModel.Limits[0].Label);
        Assert.Equal("42% used", viewModel.Limits[0].UsedText);
        Assert.Contains("previous values", viewModel.UpdatedText);
    }

    public void Dispose() => Strings.Current.SetCulture(
        CultureInfo.GetCultureInfo("en-US"), CultureInfo.GetCultureInfo("en-US"));
}
