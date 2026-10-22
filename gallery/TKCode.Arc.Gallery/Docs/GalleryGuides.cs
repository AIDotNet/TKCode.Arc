using System.Text;

namespace TKCode.Arc.Gallery.Docs;

/// <summary>A Docs-section page ready to show.</summary>
/// <param name="Language">The language of the text shown: the one asked for, or en-US when it has no translation.</param>
/// <param name="Markdown">The page with its screenshots turned into <c>&lt;shot&gt;</c> blocks.</param>
public sealed record GalleryGuide(string Page, string Language, string RequestedLanguage, string Title, string Markdown)
{
    public bool IsFallback => !string.Equals(Language, RequestedLanguage, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The pages of the sidebar's Docs section (Introduction, Installation, …): <c>Guides/&lt;Page&gt;/&lt;language&gt;.md</c>,
/// read from <see cref="GalleryTexts"/> when they are shown. Like the component pages, a page starts with a front matter
/// (<c>title</c>, and in translations <c>source-hash</c>), and a <c>![caption](images/Name.png)</c> line shows a
/// screenshot from <c>Guides/&lt;Page&gt;/images/</c>; an untranslated page falls back to en-US with a notice.
/// </summary>
public static class GalleryGuides
{
    /// <summary>The pages, in sidebar order; the gallery entry of each is <c>Docs&lt;Page&gt;</c>.</summary>
    public static readonly IReadOnlyList<string> Pages = ["Introduction", "Installation", "Theming", "Motion", "AiAndMcp", "Changelog"];

    /// <summary>The languages a guide may be written in, en-US first.</summary>
    public static readonly IReadOnlyList<string> Languages = [GalleryDocs.SourceLanguage, "zh-CN", "zh-TW"];

    /// <summary>
    /// The file of <paramref name="page"/> in <paramref name="language"/> as written, or null when there is none. For callers
    /// that cannot wait: it fails when <see cref="GalleryTexts.Source"/> fetches the text.
    /// </summary>
    public static string? Read(string page, string language) => GalleryTexts.Read(PathOf(page, language))?.Replace("\r\n", "\n");

    /// <summary>
    /// <paramref name="page"/> in <paramref name="language"/> (en-US when it is not translated), or null. For callers that
    /// cannot wait: it fails when <see cref="GalleryTexts.Source"/> fetches the text (a page view uses <see cref="GetAsync"/>).
    /// </summary>
    public static GalleryGuide? Get(string page, string language)
    {
        var shown = Shown(language);
        if (Read(page, shown) is { } text)
        {
            return Assemble(page, shown, language, text);
        }
        return shown != GalleryDocs.SourceLanguage && Read(page, GalleryDocs.SourceLanguage) is { } english
            ? Assemble(page, GalleryDocs.SourceLanguage, language, english)
            : null;
    }

    /// <summary><paramref name="page"/> in <paramref name="language"/>, as <see cref="Get"/>, once its text has been read.</summary>
    public static async ValueTask<GalleryGuide?> GetAsync(string page, string language)
    {
        var shown = Shown(language);
        if (await GalleryTexts.Source.ReadAsync(PathOf(page, shown)) is { } text)
        {
            return Assemble(page, shown, language, text.Replace("\r\n", "\n"));
        }
        return shown != GalleryDocs.SourceLanguage && await GalleryTexts.Source.ReadAsync(PathOf(page, GalleryDocs.SourceLanguage)) is { } english
            ? Assemble(page, GalleryDocs.SourceLanguage, language, english.Replace("\r\n", "\n"))
            : null;
    }

    private static string PathOf(string page, string language) => $"Guides/{page}/{language}.md";

    /// <summary>The language a page is shown in: <paramref name="language"/> when guides are written in it, otherwise en-US.</summary>
    private static string Shown(string language)
        => Languages.FirstOrDefault(l => string.Equals(l, language, StringComparison.OrdinalIgnoreCase)) ?? GalleryDocs.SourceLanguage;

    private static GalleryGuide Assemble(string page, string shown, string language, string text)
    {
        var (front, body) = Split(text);
        var markdown = new StringBuilder();
        if (!string.Equals(shown, language, StringComparison.OrdinalIgnoreCase))
        {
            markdown.Append("> ").Append(GalleryDocs.UntranslatedNotice(language)).Append("\n\n");
        }
        markdown.Append(GalleryDocs.WithScreenshots(body, $"Guides/{page}"));
        return new GalleryGuide(page, shown, language, front.GetValueOrDefault("title") ?? page, markdown.ToString());
    }

    /// <summary>The front matter's <c>key: value</c> lines and the Markdown after it.</summary>
    public static (Dictionary<string, string> Front, string Body) Split(string text)
    {
        var front = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            return (front, text);
        }
        var end = text.IndexOf("\n---\n", 3, StringComparison.Ordinal);
        if (end < 0)
        {
            return (front, text);
        }
        foreach (var line in text[4..end].Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon > 0)
            {
                front[line[..colon].Trim()] = line[(colon + 1)..].Trim();
            }
        }
        return (front, text[(end + 5)..].TrimStart('\n'));
    }
}
