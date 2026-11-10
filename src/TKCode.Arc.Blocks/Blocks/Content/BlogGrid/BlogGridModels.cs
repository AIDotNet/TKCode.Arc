// Ported from Arc's free blog-grid block (MIT, Copyright (c) 2026 Elia Kuratli): the data model of blog-grid-data.ts. The sample posts are original.
using TKCode.Arc.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The author of a <see cref="BlogPost"/>.</summary>
public sealed class BlogAuthor
{
    /// <summary>The name in the byline.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The role, for your own use (the byline does not show it).</summary>
    public string? Role { get; set; }

    /// <summary>A decorative portrait; without one the byline shows an empty disc.</summary>
    public IImage? Avatar { get; set; }
}

/// <summary>One post of a <see cref="BlogGrid"/>.</summary>
public sealed class BlogPost
{
    /// <summary>A stable id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The card and reader title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>A two line summary on cards, the lead paragraph in the reader.</summary>
    public string Excerpt { get; set; } = string.Empty;

    /// <summary>Must match one of <see cref="BlogGrid.Categories"/> to be filterable.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>The date, used for sorting and shown on the card.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Minutes to read.</summary>
    public int ReadMinutes { get; set; }

    /// <summary>The byline.</summary>
    public BlogAuthor Author { get; set; } = new();

    /// <summary>The cover as an image object, such as a bitmap or a drawing. Cards crop it to 3:2.</summary>
    public IImage? Image { get; set; }

    /// <summary>The cover as an address or file, loaded when the card shows. Used when <see cref="Image"/> is null.</summary>
    public ImageSource? ImageSource { get; set; }

    /// <summary>The cover's text alternative.</summary>
    public string? ImageAlt { get; set; }

    /// <summary>The paragraphs of the in-place reader.</summary>
    public IReadOnlyList<string>? Body { get; set; }

    /// <summary>Prefer this post for the featured slot.</summary>
    public bool IsFeatured { get; set; }

    internal bool HasCover => Image is not null || ImageSource is not null;
}

/// <summary>Arguments of <see cref="BlogGrid.PostOpened"/>.</summary>
public sealed class BlogPostEventArgs : RoutedEventArgs
{
    internal BlogPostEventArgs(RoutedEvent routedEvent, object source, BlogPost post, string? href) : base(routedEvent, source)
    {
        Post = post;
        Href = href;
    }

    /// <summary>The post that opened.</summary>
    public BlogPost Post { get; }

    /// <summary>Its link from <see cref="BlogGrid.HrefSelector"/>, or null when it opens in place. Mark the event handled to stop the link from opening.</summary>
    public string? Href { get; }
}

/// <summary>Original sample posts with drawn covers.</summary>
internal static class BlogGridSamples
{
    public static readonly string[] Categories = ["Product", "Design", "Engineering", "Company"];

    public static IReadOnlyList<BlogPost> Create()
    {
        var ines = Author("Ines Varga", "Product designer", 28);
        var tomas = Author("Tomas Reid", "Engineer", 200);
        var priya = Author("Priya Nair", "Design lead", 330);
        var owen = Author("Owen Lark", "Operations", 120);
        return
        [
            Post("slow-sync", "What we learned by making sync slower", "Waiting half a second before saving cut conflicts by a third and made the app feel calmer.", "Engineering", "2026-10-28", 7, tomas, 210, 0, featured: true),
            Post("quiet-defaults", "Quiet defaults, loud exceptions", "A settings page should mostly say nothing. Here is how we choose what deserves a word.", "Design", "2026-10-21", 5, priya, 330, 1),
            Post("shared-views", "Shared views are here", "Save a filter once and everyone on the project sees the same list.", "Product", "2026-10-14", 3, ines, 20, 2),
            Post("four-days", "A year of four-day release weeks", "Shipping less often on paper meant shipping more in practice. The numbers surprised us.", "Company", "2026-10-07", 6, owen, 150, 3),
            Post("empty-boards", "Designing the empty board", "The first screen a team sees is the one with nothing on it. We redrew it nine times.", "Design", "2026-09-30", 4, ines, 275, 0),
            Post("index-budget", "An index budget for every table", "Each new index now has to pay for itself. A small rule that kept writes fast.", "Engineering", "2026-09-22", 8, tomas, 190, 1),
            Post("guest-access", "Guests can now comment", "Invite a client to a single board without giving them a seat.", "Product", "2026-09-15", 2, ines, 45, 2),
            Post("handbook", "Why our handbook has no org chart", "We describe the work, not the boxes. New hires find the right person faster.", "Company", "2026-09-08", 5, owen, 100, 3),
            Post("type-scale", "Choosing a type scale in an afternoon", "Three ratios, one long document and a rule for when to stop testing.", "Design", "2026-09-01", 4, priya, 300, 0),
            Post("offline-notes", "Notes that survive a tunnel", "Offline edits now merge on reconnect, with a short history if they cannot.", "Product", "2026-08-25", 3, ines, 170, 1),
        ];
    }

    private static BlogAuthor Author(string name, string role, double hue) => new() { Name = name, Role = role, Avatar = BlockSampleArt.Scene(hue, 1) };

    private static BlogPost Post(string id, string title, string excerpt, string category, string date, int minutes, BlogAuthor author, double hue, int variant, bool featured = false) => new()
    {
        Id = id,
        Title = title,
        Excerpt = excerpt,
        Category = category,
        Date = DateOnly.ParseExact(date, "yyyy-MM-dd"),
        ReadMinutes = minutes,
        Author = author,
        Image = BlockSampleArt.Scene(hue, variant),
        ImageAlt = "An abstract landscape of soft ridges under a pale sun.",
        IsFeatured = featured,
        Body =
        [
            "This sample post is placeholder text written for the block. Replace it with your own article when you load posts from your content system.",
            "The reader shows the post in place: the cover grows out of its card, the text rises in under it, and going back returns the cover to its card.",
            "Pass a link selector to send each card to its own article page instead, and the reader stays out of the way."
        ]
    };
}
