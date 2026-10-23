using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace TKCode.Arc;

/// <summary>What the arena's vote strip shows (<see cref="ArenaVoteBar.Kind"/>).</summary>
internal enum ArenaVoteKind
{
    /// <summary>Nothing: the strip has no height (before the first run).</summary>
    Hidden,

    /// <summary>Waiting for both answers: a note and a count.</summary>
    Pending,

    /// <summary>The question and the four vote buttons.</summary>
    Vote,

    /// <summary>The result of the vote and Next round.</summary>
    Voted,

    /// <summary>The run was stopped: a note and Run again.</summary>
    Stopped,

    /// <summary>A model failed: a note and Next round.</summary>
    Failed
}

/// <summary>Carries the choice of a vote button of <see cref="ArenaVoteBar"/>.</summary>
internal sealed class ArenaVoteRequestedEventArgs(RoutedEvent routedEvent, object source, ArenaChoice choice) : RoutedEventArgs(routedEvent, source)
{
    /// <summary>The choice.</summary>
    public ArenaChoice Choice { get; } = choice;
}

/// <summary>
/// One row of the vote strip (Arc's flex row: wrap, gap 8 16, centered, space-between): the lead (a note, the question,
/// the result) at the start and the trail (buttons) at the end on one line when both fit; otherwise, or when
/// <see cref="ForceWrap"/> is set, the lead takes its own line (wrapping its text) and the trail goes below it, at the
/// start (or across the row when it stretches). A row is as wide as it is offered, so a row that fades out on top of the
/// next one keeps its own arrangement.
/// </summary>
internal sealed class ArenaVoteRowPanel : Panel
{
    /// <summary>Between the lead and the trail on one line (16).</summary>
    public const double ColumnGap = 16;

    /// <summary>Between the lines of a wrapped row (8).</summary>
    public const double RowGap = 8;

    /// <summary>Puts the trail below the lead even when both would fit on one line.</summary>
    public static readonly StyledProperty<bool> ForceWrapProperty =
        AvaloniaProperty.Register<ArenaVoteRowPanel, bool>(nameof(ForceWrap));

    private bool _inline;

    static ArenaVoteRowPanel()
    {
        AffectsMeasure<ArenaVoteRowPanel>(ForceWrapProperty);
    }

    /// <inheritdoc cref="ForceWrapProperty"/>
    public bool ForceWrap { get => GetValue(ForceWrapProperty); set => SetValue(ForceWrapProperty, value); }

    /// <summary>Whether the last layout put lead and trail on one line.</summary>
    public bool IsInline => _inline;

    private Control? Lead => Children.Count > 0 && Children[0].IsVisible ? Children[0] : null;

    private Control? Trail => Children.Count > 1 && Children[1].IsVisible ? Children[1] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var unbounded = double.IsInfinity(width);
        var lead = Lead;
        var trail = Trail;
        var free = new Size(double.PositiveInfinity, double.PositiveInfinity);
        var line = new Size(width, double.PositiveInfinity);
        _inline = false;
        if (lead is null && trail is null)
        {
            return default;
        }
        if (lead is null || trail is null)
        {
            var only = (lead ?? trail)!;
            only.Measure(line);
            return new Size(unbounded ? only.DesiredSize.Width : width, only.DesiredSize.Height);
        }
        trail.Measure(line);
        lead.Measure(free);
        var natural = lead.DesiredSize.Width + ColumnGap + trail.DesiredSize.Width;
        _inline = !ForceWrap && (unbounded || natural <= width);
        if (_inline)
        {
            return new Size(unbounded ? natural : width, Math.Max(lead.DesiredSize.Height, trail.DesiredSize.Height));
        }
        // The lead now gets the whole line and wraps its text if it must.
        lead.Measure(line);
        var height = lead.DesiredSize.Height + RowGap + trail.DesiredSize.Height;
        return new Size(unbounded ? Math.Max(lead.DesiredSize.Width, trail.DesiredSize.Width) : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var lead = Lead;
        var trail = Trail;
        if (lead is null && trail is null)
        {
            return finalSize;
        }
        if (lead is null || trail is null)
        {
            var only = (lead ?? trail)!;
            var size = only.DesiredSize;
            var top = (finalSize.Height - size.Height) / 2;
            only.Arrange(new Rect(0, top, Across(only, finalSize.Width), size.Height));
            return finalSize;
        }
        if (_inline)
        {
            var leadSize = lead.DesiredSize;
            var trailSize = trail.DesiredSize;
            lead.Arrange(new Rect(0, (finalSize.Height - leadSize.Height) / 2,
                Math.Max(0, finalSize.Width - trailSize.Width - ColumnGap), leadSize.Height));
            trail.Arrange(new Rect(finalSize.Width - trailSize.Width, (finalSize.Height - trailSize.Height) / 2,
                trailSize.Width, trailSize.Height));
            return finalSize;
        }
        var block = lead.DesiredSize.Height + RowGap + trail.DesiredSize.Height;
        var y = Math.Max(0, (finalSize.Height - block) / 2);
        lead.Arrange(new Rect(0, y, finalSize.Width, lead.DesiredSize.Height));
        y += lead.DesiredSize.Height + RowGap;
        trail.Arrange(new Rect(0, y, Across(trail, finalSize.Width), trail.DesiredSize.Height));
        return finalSize;
    }

    /// <summary>A stretching part takes the row's width; any other keeps its own, at the start.</summary>
    private static double Across(Control child, double width) =>
        child.HorizontalAlignment == HorizontalAlignment.Stretch ? width : Math.Min(child.DesiredSize.Width, width);
}

/// <summary>
/// The four vote buttons: one line 6px apart, or (<see cref="IsGrid"/>, the compact strip) a 2 × 2 grid of two equal
/// columns 6px apart whose buttons stretch (Arc: <c>grid-template-columns: 1fr 1fr</c>).
/// </summary>
internal sealed class ArenaVoteChoicesPanel : Panel
{
    /// <summary>Between the buttons, both ways (6).</summary>
    public const double Gap = 6;

    /// <summary>The 2 × 2 grid of the compact strip.</summary>
    public static readonly StyledProperty<bool> IsGridProperty =
        AvaloniaProperty.Register<ArenaVoteChoicesPanel, bool>(nameof(IsGrid));

    private double _column;
    private double _row;

    static ArenaVoteChoicesPanel()
    {
        AffectsMeasure<ArenaVoteChoicesPanel>(IsGridProperty);
    }

    /// <inheritdoc cref="IsGridProperty"/>
    public bool IsGrid { get => GetValue(IsGridProperty); set => SetValue(IsGridProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var free = new Size(double.PositiveInfinity, double.PositiveInfinity);
        double widest = 0, tallest = 0, sum = 0;
        var count = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            child.Measure(free);
            widest = Math.Max(widest, child.DesiredSize.Width);
            tallest = Math.Max(tallest, child.DesiredSize.Height);
            sum += child.DesiredSize.Width;
            count++;
        }
        if (count == 0)
        {
            return default;
        }
        if (!IsGrid)
        {
            return new Size(sum + Gap * (count - 1), tallest);
        }
        var width = double.IsInfinity(availableSize.Width) ? 2 * widest + Gap : availableSize.Width;
        _column = Math.Max(0, (width - Gap) / 2);
        _row = tallest;
        var rows = (count + 1) / 2;
        return new Size(width, rows * tallest + (rows - 1) * Gap);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var index = 0;
        double x = 0;
        if (IsGrid)
        {
            // The columns share the arranged width (a stretched panel may be wider than it measured).
            _column = Math.Max(0, (finalSize.Width - Gap) / 2);
        }
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            if (IsGrid)
            {
                var column = index % 2;
                var row = index / 2;
                child.Arrange(new Rect(column * (_column + Gap), row * (_row + Gap), _column, _row));
            }
            else
            {
                var size = child.DesiredSize;
                child.Arrange(new Rect(x, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
                x += size.Width + Gap;
            }
            index++;
        }
        return finalSize;
    }
}
