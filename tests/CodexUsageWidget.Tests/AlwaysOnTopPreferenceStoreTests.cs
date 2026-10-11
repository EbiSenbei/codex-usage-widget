using CodexUsageWidget.Infrastructure.Settings;

namespace CodexUsageWidget.Tests;

public sealed class AlwaysOnTopPreferenceStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "CodexUsageWidget.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void DefaultAndSavedPreferenceSurviveRestart()
    {
        var path = Path.Combine(_directory, "topmost.txt");
        var store = new AlwaysOnTopPreferenceStore(path);
        Assert.True(store.Load());
        Assert.True(store.TrySave(false));
        Assert.False(new AlwaysOnTopPreferenceStore(path).Load());
        Assert.True(store.TrySave(true));
        Assert.True(new AlwaysOnTopPreferenceStore(path).Load());
    }

    [Fact]
    public void UnwritablePreferenceReportsFailure()
    {
        Directory.CreateDirectory(_directory);
        Assert.False(new AlwaysOnTopPreferenceStore(_directory).TrySave(false));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) { Directory.Delete(_directory, recursive: true); }
    }
}
