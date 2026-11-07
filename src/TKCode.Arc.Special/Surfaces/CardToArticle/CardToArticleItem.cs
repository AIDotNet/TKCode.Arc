using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One story of a <see cref="CardToArticle"/>: the card shows its <see cref="Image"/> with the <see cref="Tag"/>, the
/// <see cref="Title"/> and the <see cref="Meta"/> line drawn over it; the article grows out of the card with the same
/// picture as its hero and shows the byline (<see cref="Author"/>, <see cref="Published"/>) and the <see cref="Body"/>.
/// Set the properties before the item is shown: the control reads them when the card is built and when the article opens.
/// </summary>
public sealed class CardToArticleItem
{
    /// <summary>Identifies the story; <see cref="CardToArticle.OpenItemId"/> holds the id of the open article.</summary>
    public string Id { get; set; } = "";

    /// <summary>The headline, drawn over the picture on the card and in the article hero.</summary>
    public string Title { get; set; } = "";

    /// <summary>A short label in a pill at the top-left of the picture (a section or a kind of story); null shows none.</summary>
    public string? Tag { get; set; }

    /// <summary>A short line under the title, such as a reading time ("4 min read"); null shows none.</summary>
    public string? Meta { get; set; }

    /// <summary>The picture: it covers the card and the article hero (cropped to <see cref="CardToArticle.AspectRatio"/>).</summary>
    public IImage? Image { get; set; }

    /// <summary>The text alternative of the picture, read in the article (the card is named by its title).</summary>
    public string? ImageAlt { get; set; }

    /// <summary>The author shown in the article's byline; null leaves the byline out.</summary>
    public string? Author { get; set; }

    /// <summary>The date (or any short text) shown after the author in the byline, already formatted.</summary>
    public string? Published { get; set; }

    /// <summary>The author's picture in the byline; null shows the author's initials on a colored circle.</summary>
    public IImage? Avatar { get; set; }

    /// <summary>
    /// The article text. A string is set as prose: paragraphs are separated by blank lines and a paragraph that starts
    /// with "&gt; " becomes a pull quote. Any other object is shown with <see cref="CardToArticle.BodyTemplate"/> (or as
    /// a control when it is one).
    /// </summary>
    public object? Body { get; set; }
}
