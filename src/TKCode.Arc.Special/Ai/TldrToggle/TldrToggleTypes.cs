using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>What a <see cref="TldrToggle"/> shows in its body.</summary>
public enum TldrMode
{
    /// <summary>The article itself, paragraph by paragraph.</summary>
    Full,

    /// <summary>A prose summary, streamed in place of the article.</summary>
    Summary,

    /// <summary>A checklist of key points, each linking back to the paragraph it came from.</summary>
    KeyPoints
}

/// <summary>One paragraph of a <see cref="TldrArticle"/>. Its <see cref="Id"/> is what key points link back to.</summary>
public sealed class TldrParagraph
{
    /// <summary>Creates an empty paragraph; set <see cref="Id"/> and <see cref="Text"/>.</summary>
    public TldrParagraph()
    {
    }

    /// <summary>Creates a paragraph with a stable id and its text.</summary>
    /// <param name="id">A stable id, such as "intro"; key points name it as their source.</param>
    /// <param name="text">The paragraph's text.</param>
    public TldrParagraph(string id, string text)
    {
        Id = id;
        Text = text;
    }

    /// <summary>A stable id, such as "intro". Key points name it as their source.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The paragraph's text.</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// The article a <see cref="TldrToggle"/> shows and summarizes: its title, its paragraphs (with stable ids) and the
/// optional source, author, date and reading time of the header. Data only; give the card a new article to change it.
/// </summary>
public sealed class TldrArticle
{
    /// <summary>The headline.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The paragraphs in order. An article with none shows a quiet empty state.</summary>
    public IReadOnlyList<TldrParagraph> Paragraphs { get; set; } = [];

    /// <summary>Where the article was published ("Engineering blog"); null hides it.</summary>
    public string? SourceName { get; set; }

    /// <summary>The source's mark, a 24 × 24 stroked glyph; null shows the source's first letter in a tinted square.</summary>
    public Geometry? SourceMark { get; set; }

    /// <summary>The author's name; null hides the author row.</summary>
    public string? AuthorName { get; set; }

    /// <summary>The author's picture; null shows their initials.</summary>
    public ImageSource? AuthorAvatar { get; set; }

    /// <summary>The publication date as it should read ("Oct 21, 2026"); null hides it.</summary>
    public string? Date { get; set; }

    /// <summary>The reading time as it should read ("5 min read"); null works it out at 230 words a minute.</summary>
    public string? ReadingTime { get; set; }
}

/// <summary>A key point: its text and the id of the paragraph it came from.</summary>
public sealed class TldrKeyPoint
{
    /// <summary>Creates a key point.</summary>
    /// <param name="source">The id of the paragraph it came from, or null when it has none.</param>
    /// <param name="text">The point's text.</param>
    public TldrKeyPoint(string? source, string text)
    {
        Source = source;
        Text = text;
    }

    /// <summary>The id of the paragraph the point came from; null shows no jump link.</summary>
    public string? Source { get; }

    /// <summary>The point's text.</summary>
    public string Text { get; }
}

/// <summary>What a <see cref="TldrToggle"/> asks its summarizer for.</summary>
public sealed class TldrRequest
{
    /// <summary>Creates a request.</summary>
    public TldrRequest(TldrMode mode, int length, TldrArticle article, CancellationToken cancellationToken)
    {
        Mode = mode;
        Length = Math.Clamp(length, TldrToggle.MinLength, TldrToggle.MaxLength);
        Article = article;
        CancellationToken = cancellationToken;
    }

    /// <summary><see cref="TldrMode.Summary"/> for prose or <see cref="TldrMode.KeyPoints"/> for a list.</summary>
    public TldrMode Mode { get; }

    /// <summary>The length from 1 (shortest) to 5 (longest).</summary>
    public int Length { get; }

    /// <summary>The words a summary aims for at this length: about 35, 60, 85, 100 or 130.</summary>
    public int TargetWords => TldrToggle.TargetWordsFor(Length);

    /// <summary>The key points a list returns at this length: 3 to 7.</summary>
    public int PointCount => TldrToggle.PointCountFor(Length);

    /// <summary>The article to summarize.</summary>
    public TldrArticle Article { get; }

    /// <summary>Cancelled on Stop, Escape, a mode or length change, a new article or when the card leaves the window.</summary>
    public CancellationToken CancellationToken { get; }
}

/// <summary>The kind of a <see cref="TldrStreamEvent"/>.</summary>
public enum TldrStreamEventKind
{
    /// <summary>More text: prose for a summary, or the current point's text.</summary>
    Text,

    /// <summary>A new key point starts; its text follows as <see cref="Text"/> events.</summary>
    Point
}

/// <summary>One event of a summary stream: a piece of text, or the start of a key point with its source paragraph.</summary>
public readonly record struct TldrStreamEvent
{
    private TldrStreamEvent(TldrStreamEventKind kind, string? text, string? source)
    {
        Kind = kind;
        Text = text;
        Source = source;
    }

    /// <summary>What the event carries.</summary>
    public TldrStreamEventKind Kind { get; }

    /// <summary>The text of a <see cref="TldrStreamEventKind.Text"/> event.</summary>
    public string? Text { get; }

    /// <summary>The source paragraph id of a <see cref="TldrStreamEventKind.Point"/> event.</summary>
    public string? Source { get; }

    /// <summary>More text (include the spaces between words).</summary>
    public static TldrStreamEvent OfText(string text) => new(TldrStreamEventKind.Text, text, null);

    /// <summary>A new key point that came from the paragraph <paramref name="source"/>; send its text after it.</summary>
    public static TldrStreamEvent OfPoint(string? source) => new(TldrStreamEventKind.Point, null, source);
}

/// <summary>
/// Produces a summary or key points for a <see cref="TldrToggle"/> as a stream of events. Honor
/// <see cref="TldrRequest.CancellationToken"/>. A provider that returns whole results can use
/// <see cref="TldrStreams.FromResult(string, CancellationToken)"/> so the card still streams.
/// </summary>
public delegate IAsyncEnumerable<TldrStreamEvent> TldrSummarizer(TldrRequest request);

/// <summary>Arguments of <see cref="TldrToggle.ModeChanged"/>.</summary>
public sealed class TldrModeChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TldrModeChangedEventArgs(RoutedEvent routedEvent, TldrMode mode) : base(routedEvent) => Mode = mode;

    /// <summary>The new mode.</summary>
    public TldrMode Mode { get; }
}

/// <summary>Arguments of <see cref="TldrToggle.LengthChanged"/>.</summary>
public sealed class TldrLengthChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TldrLengthChangedEventArgs(RoutedEvent routedEvent, int length) : base(routedEvent) => Length = length;

    /// <summary>The new length, 1 to 5.</summary>
    public int Length { get; }
}

/// <summary>Arguments of <see cref="TldrToggle.Completed"/>: a finished result.</summary>
public sealed class TldrCompletedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TldrCompletedEventArgs(RoutedEvent routedEvent, TldrMode mode, int length, string text, IReadOnlyList<TldrKeyPoint>? points, int words)
        : base(routedEvent)
    {
        Mode = mode;
        Length = length;
        Text = text;
        Points = points;
        Words = words;
    }

    /// <summary>Summary or key points.</summary>
    public TldrMode Mode { get; }

    /// <summary>The length it was made for.</summary>
    public int Length { get; }

    /// <summary>The whole text: the summary, or the points one per line.</summary>
    public string Text { get; }

    /// <summary>The key points with their sources; null for a summary.</summary>
    public IReadOnlyList<TldrKeyPoint>? Points { get; }

    /// <summary>How many words the result has.</summary>
    public int Words { get; }
}

/// <summary>Arguments of <see cref="TldrToggle.Jumping"/>.</summary>
public sealed class TldrJumpEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TldrJumpEventArgs(RoutedEvent routedEvent, string paragraphId) : base(routedEvent) => ParagraphId = paragraphId;

    /// <summary>The id of the paragraph the card is about to show.</summary>
    public string ParagraphId { get; }
}
