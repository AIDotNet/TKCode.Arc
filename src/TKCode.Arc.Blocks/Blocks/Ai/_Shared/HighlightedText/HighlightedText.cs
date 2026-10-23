using System.Globalization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// Text with every occurrence of <see cref="Query"/> marked (Arc's search <c>mark</c>): a background in the accent color
/// at 14% with 3px corners and no padding, repeated on every line a match wraps over; the text keeps its own case.
/// Matching is a plain substring search (never a pattern), case-insensitive by default, on the query without its
/// leading and trailing white space; matches do not overlap, and matches that touch share one background. An empty or
/// white-space query marks nothing and allocates nothing.
/// </summary>
/// <remarks>
/// A <see cref="DecoratedTextBlock"/> rather than a wrapper around one: <c>Text</c>, wrapping, trimming, line height
/// and the text layout the marks are measured on are the block's own, so the owner styles it like any text block and
/// nothing has to be forwarded. The marks are <see cref="TextRangeDecoration"/>s drawn under the glyphs without a new
/// layout; they are reused from change to change (the list only grows to the most matches seen) and owned by this
/// control, so do not edit <see cref="DecoratedTextBlock.Decorations"/> yourself. Only <c>Text</c> is searched (not
/// <c>Inlines</c>).
/// </remarks>
internal sealed class HighlightedText : DecoratedTextBlock
{
    /// <summary>Share of the accent color in a mark's background (Arc: <c>color-mix(accent 14%, transparent)</c>).</summary>
    internal const double MatchAlpha = 0.14;

    /// <summary>Corner radius of a mark's background.</summary>
    internal const double MatchRadius = 3;

    /// <summary>The text to mark; null, empty or only white space marks nothing.</summary>
    public static readonly StyledProperty<string?> QueryProperty =
        AvaloniaProperty.Register<HighlightedText, string?>(nameof(Query));

    /// <summary>How the query is compared with the text (default <see cref="StringComparison.OrdinalIgnoreCase"/>).</summary>
    public static readonly StyledProperty<StringComparison> ComparisonProperty =
        AvaloniaProperty.Register<HighlightedText, StringComparison>(nameof(Comparison), StringComparison.OrdinalIgnoreCase);

    /// <summary>The accent the marks are tinted from (the theme binds it to the <c>Accent</c> token).</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<HighlightedText, IBrush?>(nameof(AccentBrush));

    /// <summary>The number of matches in the text (touching matches count one each).</summary>
    public static readonly DirectProperty<HighlightedText, int> MatchCountProperty =
        AvaloniaProperty.RegisterDirect<HighlightedText, int>(nameof(MatchCount), o => o.MatchCount);

    private readonly List<TextRangeDecoration> _marks = new();
    private IBrush? _markBrush;
    private int _matchCount;

    static HighlightedText()
    {
        // Selectable with the pointer like web text, but not a stop on the Tab path.
        KeyboardNavigation.IsTabStopProperty.OverrideDefaultValue<HighlightedText>(false);
    }

    protected override Type StyleKeyOverride => typeof(HighlightedText);

    /// <inheritdoc cref="QueryProperty"/>
    public string? Query { get => GetValue(QueryProperty); set => SetValue(QueryProperty, value); }

    /// <inheritdoc cref="ComparisonProperty"/>
    public StringComparison Comparison { get => GetValue(ComparisonProperty); set => SetValue(ComparisonProperty, value); }

    /// <inheritdoc cref="AccentBrushProperty"/>
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    /// <inheritdoc cref="MatchCountProperty"/>
    public int MatchCount
    {
        get => _matchCount;
        private set => SetAndRaise(MatchCountProperty, ref _matchCount, value);
    }

    /// <summary>The marks' background: the accent at 14% (null until the theme gives an accent).</summary>
    internal IBrush? MarkBrush => _markBrush;

    /// <summary>
    /// Counts the matches of <paramref name="query"/> in <paramref name="text"/> the way <see cref="HighlightedText"/>
    /// marks them: the query without leading and trailing white space, as plain text, not overlapping. Use it to filter
    /// a list so that every row shown has at least one mark.
    /// </summary>
    /// <param name="text">The text to search.</param>
    /// <param name="query">What to look for; null, empty or white space matches nothing.</param>
    /// <param name="comparison">How to compare (ordinal or culture-aware, with or without case).</param>
    /// <returns>The number of matches.</returns>
    public static int CountMatches(string text, string? query, StringComparison comparison)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(query))
        {
            return 0;
        }
        var term = query.AsSpan().Trim();
        var count = 0;
        var from = 0;
        while (NextMatch(text, from, term, comparison, out var index, out var length))
        {
            count++;
            from = index + length;
        }
        return count;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == QueryProperty || change.Property == ComparisonProperty)
        {
            Redecorate();
        }
        else if (change.Property == AccentBrushProperty)
        {
            _markBrush = change.GetNewValue<IBrush?>() is ISolidColorBrush accent
                ? new ImmutableSolidColorBrush(Color.FromArgb((byte)Math.Round(accent.Color.A * MatchAlpha * accent.Opacity), accent.Color.R,
                    accent.Color.G, accent.Color.B))
                : null;
            foreach (var mark in _marks)
            {
                mark.Background = _markBrush;
            }
            InvalidateDecorations();
        }
    }

    /// <summary>
    /// Marks the current matches: the decorations in use are the first ones of the reused list, moved to the new ranges;
    /// extra ones are taken out of <see cref="DecoratedTextBlock.Decorations"/> but kept for later.
    /// </summary>
    internal void Redecorate()
    {
        var text = Text;
        var query = Query;
        var used = 0;
        var count = 0;
        if (!string.IsNullOrEmpty(text) && !string.IsNullOrWhiteSpace(query))
        {
            var term = query.AsSpan().Trim();
            var comparison = Comparison;
            var from = 0;
            var runStart = -1;
            var runEnd = -1;
            while (NextMatch(text, from, term, comparison, out var index, out var length))
            {
                count++;
                if (index == runEnd)
                {
                    // Touching the previous match: one background for both, so the 14% tint is not painted twice.
                    runEnd = index + length;
                }
                else
                {
                    if (runStart >= 0)
                    {
                        Mark(used++, runStart, runEnd - runStart);
                    }
                    runStart = index;
                    runEnd = index + length;
                }
                from = index + length;
            }
            if (runStart >= 0)
            {
                Mark(used++, runStart, runEnd - runStart);
            }
        }
        var decorations = Decorations;
        if (decorations.Count > used)
        {
            decorations.RemoveRange(used, decorations.Count - used);
        }
        else if (used > 0)
        {
            // Ranges moved in place: nothing was added or removed to tell the block.
            InvalidateDecorations();
        }
        MatchCount = count;
    }

    private void Mark(int slot, int start, int length)
    {
        TextRangeDecoration mark;
        if (slot < _marks.Count)
        {
            mark = _marks[slot];
        }
        else
        {
            mark = new TextRangeDecoration { CornerRadius = MatchRadius, Background = _markBrush };
            _marks.Add(mark);
        }
        mark.Start = start;
        mark.Length = length;
        if (slot >= Decorations.Count)
        {
            Decorations.Add(mark);
        }
    }

    /// <summary>
    /// The next match of <paramref name="term"/> at or after <paramref name="from"/>. Culture-aware comparisons report
    /// the matched length (it can differ from the term's); a match of no characters ends the search.
    /// </summary>
    private static bool NextMatch(string text, int from, ReadOnlySpan<char> term, StringComparison comparison, out int index, out int length)
    {
        index = -1;
        length = 0;
        if (term.IsEmpty || from >= text.Length)
        {
            return false;
        }
        var rest = text.AsSpan(from);
        int found;
        switch (comparison)
        {
            case StringComparison.Ordinal:
            case StringComparison.OrdinalIgnoreCase:
                if (term.Length > rest.Length)
                {
                    return false;
                }
                found = rest.IndexOf(term, comparison);
                length = term.Length;
                break;
            default:
                var culture = comparison is StringComparison.InvariantCulture or StringComparison.InvariantCultureIgnoreCase
                    ? CultureInfo.InvariantCulture
                    : CultureInfo.CurrentCulture;
                var options = comparison is StringComparison.CurrentCultureIgnoreCase or StringComparison.InvariantCultureIgnoreCase
                    ? CompareOptions.IgnoreCase
                    : CompareOptions.None;
                found = culture.CompareInfo.IndexOf(rest, term, options, out length);
                break;
        }
        if (found < 0 || length <= 0)
        {
            return false;
        }
        index = from + found;
        return true;
    }
}
