using System.IO;

namespace CodexUsageWidget.Infrastructure.Settings;

public sealed class AlwaysOnTopPreferenceStore
{
    private readonly string _path;

    public AlwaysOnTopPreferenceStore(string? path = null) =>
        _path = path ?? Path.Combine(AppPaths.LocalDataDirectory, "always-on-top.txt");

    public bool Load()
    {
        try { return !string.Equals(File.ReadAllText(_path).Trim(), "false", StringComparison.OrdinalIgnoreCase); }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
    }

    public bool TrySave(bool enabled)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, enabled ? "true" : "false");
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
