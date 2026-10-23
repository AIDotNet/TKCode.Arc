using AtomUI.Localization;
using TKCode.Arc.Gallery.Docs;

namespace TKCode.Arc.Gallery.Localization;

/// <summary>One of the gallery's languages: its tag and its own name for itself (an endonym, so it is never translated).</summary>
public readonly record struct GalleryLanguage(string Tag, string Name);

/// <summary>
/// Keeps the language a visitor chose between sessions: local storage in the browser, a small file on the desktop. A host
/// sets <see cref="GalleryLanguages.Store"/>; without one nothing is kept and every session starts in the system's language.
/// </summary>
public interface IGalleryLanguageStore
{
    /// <summary>The tag chosen last time, or null when there is none.</summary>
    string? Read();

    /// <summary>Remembers <paramref name="languageTag"/> for the next session.</summary>
    void Write(string languageTag);
}

/// <summary>
/// The gallery's languages and the one a session starts in. A host passes <see cref="Initial"/> and <see cref="Tags"/> to
/// <c>UseLanguages</c>: a visitor's own earlier choice wins, then the first of the system's (or the browser's) preferred
/// languages that the gallery has, then English.
/// </summary>
public static class GalleryLanguages
{
    /// <summary>Every language of the gallery, in menu order.</summary>
    public static IReadOnlyList<GalleryLanguage> All { get; } =
        [new("en-US", "English"), new("zh-CN", "简体中文"), new("zh-TW", "繁體中文")];

    /// <summary>The tags of <see cref="All"/>, for <c>UseLanguages</c>.</summary>
    public static LanguageTag[] Tags => [LanguageTags.EnUS, LanguageTags.ZhCN, LanguageTags.ZhTW];

    /// <summary>Where the visitor's choice is kept; set by the host before the application is built.</summary>
    public static IGalleryLanguageStore? Store { get; set; }

    /// <summary>
    /// The gallery language for a BCP 47 tag, or null when the gallery has none for it. Any English is en-US; Chinese in
    /// Traditional script, or of Taiwan, Hong Kong or Macao, is zh-TW; any other Chinese is zh-CN. Underscores count as
    /// hyphens (<c>zh_TW</c>), and case does not matter.
    /// </summary>
    public static string? Match(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }
        var parts = tag.Trim().Replace('_', '-').Split('-', StringSplitOptions.RemoveEmptyEntries);
        var language = parts[0].ToLowerInvariant();
        if (language == "en")
        {
            return "en-US";
        }
        if (language != "zh")
        {
            return null;
        }
        foreach (var part in parts.Skip(1))
        {
            switch (part.ToLowerInvariant())
            {
                case "hant":
                case "tw":
                case "hk":
                case "mo":
                    return "zh-TW";
                case "hans":
                    return "zh-CN";
            }
        }
        return "zh-CN";
    }

    /// <summary>
    /// The language a session starts in: the one in <see cref="Store"/>, else the first of <paramref name="preferred"/> (most
    /// preferred first) that <see cref="Match"/> knows, else en-US.
    /// </summary>
    public static LanguageTag Initial(IEnumerable<string?> preferred)
    {
        var tag = Match(ReadStore()) ?? preferred.Select(Match).FirstOrDefault(t => t is not null) ?? GalleryDocs.SourceLanguage;
        return LanguageTag.Parse(tag);
    }

    /// <summary>Switches the application to <paramref name="languageTag"/> and keeps it for the next session.</summary>
    public static void Choose(string languageTag)
    {
        GalleryText.SetLanguage(languageTag);
        try
        {
            Store?.Write(languageTag);
        }
        catch (Exception)
        {
            // A read-only profile or blocked storage only means the choice is not kept.
        }
    }

    private static string? ReadStore()
    {
        try
        {
            return Store?.Read();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
