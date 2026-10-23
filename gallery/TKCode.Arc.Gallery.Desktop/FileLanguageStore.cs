using TKCode.Arc.Gallery.Localization;

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
