using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc;

/// <summary>
/// The main area of a <see cref="PromptCache"/>: the requests (first child) and the cache panel (second child). Wide, the
/// panel is a fixed column beside the requests; stacked (narrow containers), it is a full-width strip above them, so the
/// flight still travels from a row into the cache.
/// </summary>
internal sealed class PromptCacheSplit : Panel
{
    /// <summary>Defines the <see cref="IsStacked"/> property.</summary>
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<PromptCacheSplit, bool>(nameof(IsStacked));

    /// <summary>Defines the <see cref="PanelWidth"/> property.</summary>
    public static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<PromptCacheSplit, double>(nameof(PanelWidth), 192);

    /// <summary>Defines the <see cref="Gap"/> property.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<PromptCacheSplit, double>(nameof(Gap), 24);

    /// <summary>Defines the <see cref="StackGap"/> property.</summary>
    public static readonly StyledProperty<double> StackGapProperty =
        AvaloniaProperty.Register<PromptCacheSplit, double>(nameof(StackGap), 20);

    static PromptCacheSplit()
    {
        AffectsMeasure<PromptCacheSplit>(IsStackedProperty, PanelWidthProperty, GapProperty, StackGapProperty);
    }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    public double PanelWidth { get => GetValue(PanelWidthProperty); set => SetValue(PanelWidthProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double StackGap { get => GetValue(StackGapProperty); set => SetValue(StackGapProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return default;
        }
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 694;
        var table = Children[0];
        var panel = Children[1];
        if (IsStacked)
        {
            panel.Measure(new Size(width, double.PositiveInfinity));
            table.Measure(new Size(width, double.PositiveInfinity));
            return new Size(width, panel.DesiredSize.Height + StackGap + table.DesiredSize.Height);
        }
        panel.Measure(new Size(PanelWidth, double.PositiveInfinity));
        table.Measure(new Size(Math.Max(0, width - PanelWidth - Gap), double.PositiveInfinity));
        return new Size(width, Math.Max(panel.DesiredSize.Height, table.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return finalSize;
        }
        var table = Children[0];
        var panel = Children[1];
        if (IsStacked)
        {
            panel.Arrange(new Rect(0, 0, finalSize.Width, panel.DesiredSize.Height));
            table.Arrange(new Rect(0, panel.DesiredSize.Height + StackGap, finalSize.Width, table.DesiredSize.Height));
            return finalSize;
        }
        var tableWidth = Math.Max(0, finalSize.Width - PanelWidth - Gap);
        table.Arrange(new Rect(0, 0, tableWidth, table.DesiredSize.Height));
        panel.Arrange(new Rect(finalSize.Width - PanelWidth, 0, PanelWidth, panel.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>How a <see cref="PromptCache"/> part lays out at the card's width.</summary>
internal enum PromptCacheWidthMode
{
    /// <summary>660px of content and more.</summary>
    Wide,

    /// <summary>Under 660px.</summary>
    Narrow,

    /// <summary>Under 480px.</summary>
    Compact
}

/// <summary>
/// The footer of a <see cref="PromptCache"/>: the statistics (first child) and the change toggle (second child). Wide, the
/// toggle sits at the bottom right when it fits beside the statistics; otherwise, and in narrow cards, it drops under
/// them on the left; compact cards stretch it to the full width.
/// </summary>
internal sealed class PromptCacheFooter : Panel
{
    /// <summary>Defines the <see cref="Mode"/> property.</summary>
    public static readonly StyledProperty<PromptCacheWidthMode> ModeProperty =
        AvaloniaProperty.Register<PromptCacheFooter, PromptCacheWidthMode>(nameof(Mode));

    private const double ColumnGap = 24;
    private const double RowGap = 16;

    static PromptCacheFooter()
    {
        AffectsMeasure<PromptCacheFooter>(ModeProperty);
    }

    public PromptCacheWidthMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    private bool Beside(double width, Control stats, Control toggle)
        => Mode == PromptCacheWidthMode.Wide && (!toggle.IsVisible || stats.DesiredSize.Width + ColumnGap + toggle.DesiredSize.Width <= width);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return default;
        }
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 694;
        var stats = Children[0];
        var toggle = Children[1];
        stats.Measure(new Size(width, double.PositiveInfinity));
        toggle.Measure(new Size(width, double.PositiveInfinity));
        if (!toggle.IsVisible)
        {
            return new Size(width, stats.DesiredSize.Height);
        }
        if (Beside(width, stats, toggle))
        {
            return new Size(width, Math.Max(stats.DesiredSize.Height, toggle.DesiredSize.Height));
        }
        return new Size(width, stats.DesiredSize.Height + RowGap + toggle.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return finalSize;
        }
        var stats = Children[0];
        var toggle = Children[1];
        var statsHeight = stats.DesiredSize.Height;
        stats.Arrange(new Rect(0, 0, finalSize.Width, statsHeight));
        if (!toggle.IsVisible)
        {
            return finalSize;
        }
        var th = toggle.DesiredSize.Height;
        if (Beside(finalSize.Width, stats, toggle))
        {
            var tw = toggle.DesiredSize.Width;
            var height = Math.Max(statsHeight, th);
            toggle.Arrange(new Rect(finalSize.Width - tw, height - th, tw, th));
            return finalSize;
        }
        var w = Mode == PromptCacheWidthMode.Compact ? finalSize.Width : Math.Min(finalSize.Width, toggle.DesiredSize.Width);
        toggle.Arrange(new Rect(0, statsHeight + RowGap, w, th));
        return finalSize;
    }
}

/// <summary>
/// The prices in a <see cref="PromptCache"/>'s cache panel: three label and value pairs and a unit note. Beside the
/// requests they are rows (label left, value right) above the note; in the strip above the requests they run on one line
/// with the note at the right, wrapping when the card is narrow, the note then on its own line.
/// </summary>
internal sealed class PromptCachePrices : Panel
{
    /// <summary>Defines the <see cref="Mode"/> property.</summary>
    public static readonly StyledProperty<PromptCacheWidthMode> ModeProperty =
        AvaloniaProperty.Register<PromptCachePrices, PromptCacheWidthMode>(nameof(Mode));

    private const double RowGap = 4;
    private const double PairGap = 6;
    private const double ItemGap = 16;

    static PromptCachePrices()
    {
        AffectsMeasure<PromptCachePrices>(ModeProperty);
    }

    public PromptCacheWidthMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
        }
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 400;
        return new Size(width, Place(width, arrange: false));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Place(finalSize.Width, arrange: true);
        return finalSize;
    }

    private double Place(double width, bool arrange)
    {
        if (Children.Count < 7)
        {
            return 0;
        }
        var unit = Children[6];
        if (Mode == PromptCacheWidthMode.Wide)
        {
            var y = 0.0;
            for (var i = 0; i < 3; i++)
            {
                var label = Children[i * 2];
                var value = Children[i * 2 + 1];
                var h = Math.Max(label.DesiredSize.Height, value.DesiredSize.Height);
                if (arrange)
                {
                    label.Arrange(new Rect(0, y, label.DesiredSize.Width, h));
                    value.Arrange(new Rect(width - value.DesiredSize.Width, y, value.DesiredSize.Width, h));
                }
                y += h + RowGap;
            }
            if (arrange)
            {
                unit.Arrange(new Rect(0, y, unit.DesiredSize.Width, unit.DesiredSize.Height));
            }
            return y + unit.DesiredSize.Height;
        }
        // One line of pairs; the unit note at the right of the first line when it fits, else on a line of its own.
        var lineHeight = 0.0;
        var x = 0.0;
        var top = 0.0;
        var unitBeside = Mode == PromptCacheWidthMode.Narrow;
        var limit = unitBeside ? width - unit.DesiredSize.Width - ItemGap : width;
        var lines = 1;
        for (var i = 0; i < 3; i++)
        {
            var label = Children[i * 2];
            var value = Children[i * 2 + 1];
            var w = label.DesiredSize.Width + PairGap + value.DesiredSize.Width;
            var h = Math.Max(label.DesiredSize.Height, value.DesiredSize.Height);
            if (x > 0 && x + w > limit)
            {
                top += lineHeight + RowGap;
                x = 0;
                lineHeight = 0;
                lines++;
                limit = width;
            }
            if (arrange)
            {
                label.Arrange(new Rect(x, top, label.DesiredSize.Width, h));
                value.Arrange(new Rect(x + label.DesiredSize.Width + PairGap, top, value.DesiredSize.Width, h));
            }
            x += w + ItemGap;
            lineHeight = Math.Max(lineHeight, h);
        }
        if (unitBeside && lines == 1)
        {
            if (arrange)
            {
                unit.Arrange(new Rect(width - unit.DesiredSize.Width, 0, unit.DesiredSize.Width, unit.DesiredSize.Height));
            }
            return Math.Max(lineHeight, unit.DesiredSize.Height);
        }
        var unitTop = top + lineHeight + RowGap;
        if (arrange)
        {
            unit.Arrange(new Rect(0, unitTop, unit.DesiredSize.Width, unit.DesiredSize.Height));
        }
        return unitTop + unit.DesiredSize.Height;
    }
}
