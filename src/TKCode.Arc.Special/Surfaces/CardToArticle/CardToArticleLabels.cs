namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="CardToArticle"/> shows or reads out, in English by default. Replace the instance to
/// translate or reword; names built from a story are functions so that word order can change.
/// </summary>
public sealed class CardToArticleLabels
{
    /// <summary>The accessible name of the article's close button.</summary>
    public string Close { get; init; } = "Close article";

    /// <summary>The accessible name of a card; the default reads the title, then the meta line when there is one.</summary>
    public Func<CardToArticleItem, string> CardName { get; init; } =
        static item => string.IsNullOrWhiteSpace(item.Meta) ? item.Title : $"{item.Title}, {item.Meta}";

    /// <summary>The accessible name of the open article dialog; the default is the story's title.</summary>
    public Func<CardToArticleItem, string> ArticleName { get; init; } = static item => item.Title;
}
