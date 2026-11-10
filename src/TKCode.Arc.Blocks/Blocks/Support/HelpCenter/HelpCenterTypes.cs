using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>A group of articles on a <see cref="HelpCenter"/> home, such as "Billing" or "Getting started".</summary>
public sealed class HelpCenterCategory
{
    /// <summary>Creates an empty category (XAML).</summary>
    public HelpCenterCategory()
    {
    }

    /// <summary>Creates a category.</summary>
    public HelpCenterCategory(string id, string title, string? description = null, Geometry? icon = null)
    {
        Id = id;
        Title = title;
        Description = description;
        Icon = icon;
    }

    /// <summary>Stable identity; articles name it in <see cref="HelpCenterArticle.CategoryId"/>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The category's name.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>One line on what it covers; null shows none.</summary>
    public string? Description { get; set; }

    /// <summary>A 24×24 stroke icon (<see cref="ArcIcons"/>); null shows a book.</summary>
    public Geometry? Icon { get; set; }
}

/// <summary>One article of a <see cref="HelpCenter"/>: its body is Markdown, and its <c>##</c> and <c>###</c> headings make the table of contents.</summary>
public sealed class HelpCenterArticle
{
    /// <summary>Creates an empty article (XAML).</summary>
    public HelpCenterArticle()
    {
    }

    /// <summary>Creates an article.</summary>
    public HelpCenterArticle(string id, string categoryId, string title, string? summary, string body)
    {
        Id = id;
        CategoryId = categoryId;
        Title = title;
        Summary = summary;
        Body = body;
    }

    /// <summary>Stable identity.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The <see cref="HelpCenterCategory.Id"/> it belongs to.</summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>The article's title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>One or two sentences shown in lists and search results; null shows none.</summary>
    public string? Summary { get; set; }

    /// <summary>The article as Markdown, without its title.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>When it was last updated; null shows no date.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Whether it is listed under "Popular articles" on the home.</summary>
    public bool IsPopular { get; set; }

    /// <summary>Minutes to read, at 200 words a minute (at least 1).</summary>
    public int ReadingMinutes => Math.Max(1, (int)Math.Round(Body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length / 200.0));
}

/// <summary>Arguments of <see cref="HelpCenter.ArticleOpened"/>.</summary>
public sealed class HelpCenterArticleEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public HelpCenterArticleEventArgs(RoutedEvent routedEvent, HelpCenterArticle article)
        : base(routedEvent)
    {
        Article = article;
    }

    /// <summary>The article.</summary>
    public HelpCenterArticle Article { get; }
}

/// <summary>Arguments of <see cref="HelpCenter.FeedbackGiven"/>: the answer to "Was this helpful?".</summary>
public sealed class HelpCenterFeedbackEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public HelpCenterFeedbackEventArgs(RoutedEvent routedEvent, HelpCenterArticle article, bool isHelpful, string? comment)
        : base(routedEvent)
    {
        Article = article;
        IsHelpful = isHelpful;
        Comment = comment;
    }

    /// <summary>The article rated.</summary>
    public HelpCenterArticle Article { get; }

    /// <summary>Whether the reader found it helpful.</summary>
    public bool IsHelpful { get; }

    /// <summary>What the reader said was missing (after "No"); null when they wrote nothing.</summary>
    public string? Comment { get; }
}
