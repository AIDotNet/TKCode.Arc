using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc;

/// <summary>
/// Two children on one line, the first at the start and the second at the end (CSS <c>space-between</c> with
/// <c>flex-wrap</c>): when they do not fit beside each other with <see cref="ColumnGap"/>, or <see cref="IsWrapForced"/>,
/// they stack at the start with <see cref="RowGap"/>, in reverse order when <see cref="IsReversedWhenWrapped"/>. Used for
/// the header (titles and switches) and the summary (headline and statistics).
/// </summary>
internal sealed class MrrWaterfallSplitPanel : Panel
{
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<MrrWaterfallSplitPanel, double>(nameof(ColumnGap), 16);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<MrrWaterfallSplitPanel, double>(nameof(RowGap), 12);

    public static readonly StyledProperty<bool> IsWrapForcedProperty =
        AvaloniaProperty.Register<MrrWaterfallSplitPanel, bool>(nameof(IsWrapForced));

    public static readonly StyledProperty<bool> IsReversedWhenWrappedProperty =
        AvaloniaProperty.Register<MrrWaterfallSplitPanel, bool>(nameof(IsReversedWhenWrapped));

    public static readonly StyledProperty<bool> StretchTrailProperty =
        AvaloniaProperty.Register<MrrWaterfallSplitPanel, bool>(nameof(StretchTrail));

    static MrrWaterfallSplitPanel()
    {
        AffectsMeasure<MrrWaterfallSplitPanel>(ColumnGapProperty, RowGapProperty, IsWrapForcedProperty, IsReversedWhenWrappedProperty, StretchTrailProperty);
    }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    public bool IsWrapForced { get => GetValue(IsWrapForcedProperty); set => SetValue(IsWrapForcedProperty, value); }

    /// <summary>The trailing child takes the whole width when the children stack (the statistics of a narrow block).</summary>
    public bool StretchTrail { get => GetValue(StretchTrailProperty); set => SetValue(StretchTrailProperty, value); }

    public bool IsReversedWhenWrapped { get => GetValue(IsReversedWhenWrappedProperty); set => SetValue(IsReversedWhenWrappedProperty, value); }

    /// <summary>True when the last layout stacked the children.</summary>
    public bool IsWrapped { get; private set; }

    private (Control? Lead, Control? Trail) Parts()
    {
        Control? lead = null;
        Control? trail = null;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            if (lead is null)
            {
                lead = child;
            }
            else if (trail is null)
            {
                trail = child;
            }
        }
        return (lead, trail);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var (lead, trail) = Parts();
        var width = availableSize.Width;
        if (lead is null)
        {
            IsWrapped = false;
            return default;
        }
        if (trail is null)
        {
            IsWrapped = false;
            lead.Measure(availableSize);
            return lead.DesiredSize;
        }
        trail.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        lead.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        var natural = lead.DesiredSize.Width;
        IsWrapped = IsWrapForced || natural + ColumnGap + trail.DesiredSize.Width > width;
        if (IsWrapped)
        {
            lead.Measure(new Size(width, double.PositiveInfinity));
            trail.Measure(new Size(width, double.PositiveInfinity));
            return new Size(StretchTrail ? width : Math.Max(lead.DesiredSize.Width, trail.DesiredSize.Width),
                lead.DesiredSize.Height + RowGap + trail.DesiredSize.Height);
        }
        lead.Measure(new Size(Math.Max(0, width - ColumnGap - trail.DesiredSize.Width), availableSize.Height));
        return new Size(lead.DesiredSize.Width + ColumnGap + trail.DesiredSize.Width,
            Math.Max(lead.DesiredSize.Height, trail.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (lead, trail) = Parts();
        if (lead is null)
        {
            return finalSize;
        }
        if (trail is null)
        {
            lead.Arrange(new Rect(0, 0, Math.Min(finalSize.Width, lead.DesiredSize.Width), finalSize.Height));
            return finalSize;
        }
        if (IsWrapped)
        {
            var first = IsReversedWhenWrapped ? trail : lead;
            var second = IsReversedWhenWrapped ? lead : trail;
            first.Arrange(new Rect(0, 0, Math.Min(finalSize.Width, first.DesiredSize.Width), first.DesiredSize.Height));
            second.Arrange(new Rect(0, first.DesiredSize.Height + RowGap, StretchTrail && second == trail ? finalSize.Width : Math.Min(finalSize.Width, second.DesiredSize.Width),
                second.DesiredSize.Height));
            return finalSize;
        }
        var height = finalSize.Height;
        lead.Arrange(new Rect(0, Offset(lead, height), lead.DesiredSize.Width, lead.DesiredSize.Height));
        var trailWidth = trail.DesiredSize.Width;
        trail.Arrange(new Rect(finalSize.Width - trailWidth, Offset(trail, height), trailWidth, trail.DesiredSize.Height));
        return finalSize;
    }

    private static double Offset(Control child, double height) => child.VerticalAlignment switch
    {
        VerticalAlignment.Center => (height - child.DesiredSize.Height) / 2,
        VerticalAlignment.Bottom => height - child.DesiredSize.Height,
        _ => 0
    };
}
