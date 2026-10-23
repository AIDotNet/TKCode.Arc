using System;
using System.Runtime.InteropServices.JavaScript;
using TKCode.Arc.Gallery.Localization;

namespace TKCode.Arc.Gallery.Browser;

/// <summary>
/// The browser's side of the gallery language (the <c>arc-lang</c> module of main.js): the visitor's preferred languages from
/// <c>navigator.languages</c>, and the language chosen in the top bar, kept in local storage.
/// </summary>
internal sealed partial class BrowserLanguageStore : IGalleryLanguageStore
{
    /// <summary>The browser's preferred languages, most preferred first (for example <c>zh-TW, en-US</c>).</summary>
    public static string[] Preferred() => Languages().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public string? Read() => Load() is { Length: > 0 } tag ? tag : null;

    public void Write(string languageTag) => Save(languageTag);

    [JSImport("preferred", "arc-lang")]
    private static partial string Languages();

    [JSImport("read", "arc-lang")]
    private static partial string Load();

    [JSImport("write", "arc-lang")]
    private static partial void Save(string languageTag);
}
