using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A source an AI reply drew on: a page, a document or a record. Shown as a numbered citation in the text, as a row in a
/// source list and as a preview card. Data only: an <see cref="AvaloniaObject"/> so that a change reaches what is on screen.
/// </summary>
public class AiSource : AvaloniaObject
{
    /// <summary>The number the text cites it with ("[1]"), counted from 1 in the order the sources arrived.</summary>
    public static readonly StyledProperty<int> IndexProperty =
        AvaloniaProperty.Register<AiSource, int>(nameof(Index), 1);

    /// <summary>The title of the page or document.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<AiSource, string>(nameof(Title), string.Empty);

    /// <summary>The site or place it comes from, such as "docs.example.org".</summary>
    public static readonly StyledProperty<string?> DomainProperty =
        AvaloniaProperty.Register<AiSource, string?>(nameof(Domain));

    /// <summary>Where the source can be opened; null makes the citation plain.</summary>
    public static readonly StyledProperty<Uri?> UrlProperty =
        AvaloniaProperty.Register<AiSource, Uri?>(nameof(Url));

    /// <summary>A short excerpt shown in the preview card.</summary>
    public static readonly StyledProperty<string?> SnippetProperty =
        AvaloniaProperty.Register<AiSource, string?>(nameof(Snippet));

    /// <summary>When the source was published or edited, as text ("Mar 3").</summary>
    public static readonly StyledProperty<string?> DateProperty =
        AvaloniaProperty.Register<AiSource, string?>(nameof(Date));

    /// <summary>The site's small mark: a <see cref="Geometry"/> in a 24 × 24 box or an <see cref="IImage"/>. Null shows the first letter of the domain.</summary>
    public static readonly StyledProperty<object?> MarkProperty =
        AvaloniaProperty.Register<AiSource, object?>(nameof(Mark));

    /// <summary>The fill of a <see cref="Geometry"/> mark; null uses the secondary text color.</summary>
    public static readonly StyledProperty<IBrush?> MarkBrushProperty =
        AvaloniaProperty.Register<AiSource, IBrush?>(nameof(MarkBrush));

    /// <summary>The number the text cites the source with.</summary>
    public int Index { get => GetValue(IndexProperty); set => SetValue(IndexProperty, value); }

    /// <summary>The title of the source.</summary>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>The site or place it comes from.</summary>
    public string? Domain { get => GetValue(DomainProperty); set => SetValue(DomainProperty, value); }

    /// <summary>Where the source can be opened.</summary>
    public Uri? Url { get => GetValue(UrlProperty); set => SetValue(UrlProperty, value); }

    /// <summary>The excerpt of the preview card.</summary>
    public string? Snippet { get => GetValue(SnippetProperty); set => SetValue(SnippetProperty, value); }

    /// <summary>When the source was published or edited.</summary>
    public string? Date { get => GetValue(DateProperty); set => SetValue(DateProperty, value); }

    /// <summary>The site's small mark.</summary>
    public object? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    /// <summary>The fill of a geometry mark.</summary>
    public IBrush? MarkBrush { get => GetValue(MarkBrushProperty); set => SetValue(MarkBrushProperty, value); }
}
