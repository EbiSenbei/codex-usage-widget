using CodexUsageWidget.Infrastructure.Settings;

namespace CodexUsageWidget.Tests;

public sealed class ProviderVisibilityStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "CodexUsageWidget.Tests", Guid.NewGuid().ToString("N"));
    private string PreferencePath => Path.Combine(_directory, "providers.json");

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void IndependentPreferencesSurviveRestart(bool codex, bool claude)
    {
        var store = new ProviderVisibilityStore(PreferencePath);
        Assert.True(store.TrySave(new ProviderVisibility(codex, claude)));
        Assert.Equal(new ProviderVisibility(codex, claude), new ProviderVisibilityStore(PreferencePath).Load());
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{}")]
    public void InvalidOrEmptyPreferenceDefaultsToBothVisible(string value)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(PreferencePath, value);
        Assert.Equal(new ProviderVisibility(), new ProviderVisibilityStore(PreferencePath).Load());
    }

    [Fact]
    public void MissingPreferenceDefaultsToBothVisible() =>
        Assert.Equal(new ProviderVisibility(), new ProviderVisibilityStore(PreferencePath).Load());

    [Fact]
    public void SavingToADirectoryReportsFailure()
    {
        Directory.CreateDirectory(_directory);
        Assert.False(new ProviderVisibilityStore(_directory).TrySave(new ProviderVisibility(false, false)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) { Directory.Delete(_directory, recursive: true); }
    }
}
