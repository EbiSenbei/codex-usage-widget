using System.IO;
using System.Text.Json;

namespace CodexUsageWidget.Infrastructure.Settings;

public sealed record ProviderVisibility(bool Codex = true, bool Claude = true);

public sealed class ProviderVisibilityStore
{
    private readonly string _path;
    public ProviderVisibilityStore(string? path = null) =>
        _path = path ?? Path.Combine(AppPaths.LocalDataDirectory, "provider-visibility.json");

    public ProviderVisibility Load()
    {
        try { return JsonSerializer.Deserialize<ProviderVisibility>(File.ReadAllText(_path)) ?? new(); }
        catch (IOException) { return new(); }
        catch (UnauthorizedAccessException) { return new(); }
        catch (JsonException) { return new(); }
    }

    public bool TrySave(ProviderVisibility preference)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(preference));
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
