using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One source of a <see cref="SourceCards"/>: a page the answer cites. Sources are numbered by their place in
/// <see cref="SourceCards.Sources"/>, so the first is cited as [1].
/// </summary>
public sealed class SourceCardsSource
{
    /// <summary>Creates an empty source (set <see cref="Title"/> and <see cref="Url"/>).</summary>
    public SourceCardsSource()
    {
    }

    /// <summary>Creates a source with its title and address.</summary>
    public SourceCardsSource(string title, string url)
    {
        Title = title;
        Url = url;
    }

    /// <summary>The page title as the search result shows it; the card clamps it to two lines.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The full address. Its host, without "www.", is the domain the card shows and gives the lettered mark.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>A stable key; the address when not set.</summary>
    public string? Id { get; set; }

    /// <summary>A readable site name for the preview and the stack, such as "Ledgerpay Docs".</summary>
    public string? Site { get; set; }

    /// <summary>The passage the answer relied on, shown in the hover preview (four lines).</summary>
    public string? Snippet { get; set; }

    /// <summary>A short date label, such as "Oct 9, 2026".</summary>
    public string? Published { get; set; }

    /// <summary>
    /// The site's mark as a 24 × 24 stroked glyph (Lucide style), drawn at 14px on the card and 16px in the preview. Without
    /// it the card draws a lettered disc. Pass the site's own favicon shape here rather than inventing a logo.
    /// </summary>
    public Geometry? Icon { get; set; }

    /// <summary>The key the cards are matched by.</summary>
    internal string Key => string.IsNullOrEmpty(Id) ? Url : Id;
}
