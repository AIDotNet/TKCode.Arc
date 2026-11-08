namespace ArcStarterApp.Settings;

/// <summary>Where <see cref="AppSettings"/> keeps its JSON text.</summary>
public interface ISettingsStore
{
    /// <summary>The stored text, or null when nothing is stored yet or it cannot be read.</summary>
    string? Read();

    /// <summary>Stores <paramref name="json"/>; a failure is ignored (preferences are a convenience).</summary>
    void Write(string json);
}

/// <summary>Keeps the settings in a file (the desktop default).</summary>
public sealed class FileSettingsStore(string path) : ISettingsStore
{
    /// <summary>The file.</summary>
    public string Path { get; } = path;

    public string? Read()
    {
        try
        {
            return File.Exists(Path) ? File.ReadAllText(Path) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Write(string json)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            // Write a temporary file and move it over the old one, so a crash mid-write never leaves half a file.
            var temporary = Path + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, Path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>Keeps the settings in memory only, for tests and previews.</summary>
public sealed class MemorySettingsStore : ISettingsStore
{
    /// <summary>The last text written, or null.</summary>
    public string? Json { get; set; }

    public string? Read() => Json;

    public void Write(string json) => Json = json;
}
