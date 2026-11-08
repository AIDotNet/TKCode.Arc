using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Shell;

namespace TKCode.Arc.Gallery.Desktop;

/// <summary>
/// Keeps the language chosen in the top bar in a one-line file of the user's local application data
/// (<c>~/Library/Application Support</c> on macOS, <c>%LOCALAPPDATA%</c> on Windows, <c>~/.local/share</c> on Linux), in
/// <c>TKCode.Arc.Gallery/language</c>.
/// </summary>
internal sealed class FileLanguageStore : IGalleryLanguageStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TKCode.Arc.Gallery", "language");

    public string? Read() => File.Exists(_path) ? File.ReadAllText(_path).Trim() : null;

    public void Write(string languageTag)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, languageTag);
    }
}

/// <summary>Keeps the About page's "check when the app starts" switch in <c>TKCode.Arc.Gallery/auto-update-check</c> beside the language.</summary>
internal sealed class FileUpdateStore : IGalleryUpdateStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TKCode.Arc.Gallery", "auto-update-check");

    public bool? ReadAutoCheck() => File.Exists(_path) && bool.TryParse(File.ReadAllText(_path).Trim(), out var value) ? value : null;

    public void WriteAutoCheck(bool value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, value ? "true" : "false");
    }
}
