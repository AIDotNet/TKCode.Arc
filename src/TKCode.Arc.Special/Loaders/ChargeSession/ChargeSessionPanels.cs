using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The hero row of <see cref="ChargeSession"/>: the scene at full width (its height 0.24 of the width) under
/// <see cref="TopSpace"/> for the pill, and the limit marker centered on the limit's x at the top, real size at any width.
/// The marker's stem is stretched to reach the pack's top edge.
/// </summary>
internal sealed class ChargeSessionHero : Panel
{
    public static readonly StyledProperty<double> TopSpaceProperty =
        AvaloniaProperty.Register<ChargeSessionHero, double>(nameof(TopSpace), 36);

    public static readonly StyledProperty<double> LimitProperty =
        AvaloniaProperty.Register<ChargeSessionHero, double>(nameof(Limit), 80);

    static ChargeSessionHero()
    {
        AffectsMeasure<ChargeSessionHero>(TopSpaceProperty);
        AffectsArrange<ChargeSessionHero>(LimitProperty);
    }

    public double TopSpace { get => GetValue(TopSpaceProperty); set => SetValue(TopSpaceProperty, value); }

    public double Limit { get => GetValue(LimitProperty); set => SetValue(LimitProperty, value); }

    /// <summary>The scene's rectangle in this panel (pointer math reads it once per press).</summary>
    public Rect SceneRect { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : ChargeSessionScene.UnitWidth;
        var sceneHeight = width * ChargeSessionScene.UnitHeight / ChargeSessionScene.UnitWidth;
        foreach (var child in Children)
        {
            if (child is ChargeSessionScene)
            {
                child.Measure(new Size(width, sceneHeight));
            }
            else
            {
                child.Measure(Size.Infinity);
            }
        }
        return new Size(width, TopSpace + sceneHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        var k = width / ChargeSessionScene.UnitWidth;
        var sceneHeight = width * ChargeSessionScene.UnitHeight / ChargeSessionScene.UnitWidth;
        SceneRect = new Rect(0, TopSpace, width, sceneHeight);
        foreach (var child in Children)
        {
            if (child is ChargeSessionScene)
            {
                child.Arrange(SceneRect);
            }
            else
            {
                var size = child.DesiredSize;
                var x = k * ChargeSessionScene.LimitX(Limit) - size.Width / 2;
                child.Arrange(new Rect(x, 0, size.Width, size.Height));
                if (child is ChargeSessionLimitHandle handle)
                {
                    handle.StemOverhang = Math.Max(0, TopSpace + ChargeSessionScene.ShellTop * k - size.Height);
                }
            }
        }
        return new Size(width, TopSpace + sceneHeight);
    }
}

/// <summary>
/// The lower row of <see cref="ChargeSession"/>: the curve section and the stats grid side by side (columns
/// <see cref="CurveWeight"/> : 1, the stats stretched to the curve section's height) or stacked (curve on top).
/// </summary>
internal sealed class ChargeSessionLower : Panel
{
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<ChargeSessionLower, bool>(nameof(IsStacked));

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ChargeSessionLower, double>(nameof(Gap), 24);

    public static readonly StyledProperty<double> CurveWeightProperty =
        AvaloniaProperty.Register<ChargeSessionLower, double>(nameof(CurveWeight), 1.55);

    static ChargeSessionLower()
    {
        AffectsMeasure<ChargeSessionLower>(IsStackedProperty, GapProperty, CurveWeightProperty);
    }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double CurveWeight { get => GetValue(CurveWeightProperty); set => SetValue(CurveWeightProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return default;
        }
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 816;
        var curve = Children[0];
        var stats = Children[1];
        if (IsStacked)
        {
            curve.Measure(new Size(width, double.PositiveInfinity));
            stats.Measure(new Size(width, double.PositiveInfinity));
            return new Size(width, curve.DesiredSize.Height + Gap + stats.DesiredSize.Height);
        }
        var (left, right) = Columns(width);
        curve.Measure(new Size(left, double.PositiveInfinity));
        stats.Measure(new Size(right, double.PositiveInfinity));
        return new Size(width, Math.Max(curve.DesiredSize.Height, stats.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return finalSize;
        }
        var curve = Children[0];
        var stats = Children[1];
        var width = finalSize.Width;
        if (IsStacked)
        {
            curve.Arrange(new Rect(0, 0, width, curve.DesiredSize.Height));
            stats.Arrange(new Rect(0, curve.DesiredSize.Height + Gap, width, stats.DesiredSize.Height));
            return finalSize;
        }
        var (left, right) = Columns(width);
        var height = Math.Max(curve.DesiredSize.Height, stats.DesiredSize.Height);
        curve.Arrange(new Rect(0, 0, left, height));
        stats.Arrange(new Rect(left + Gap, 0, right, height));
        return finalSize;
    }

    private (double Left, double Right) Columns(double width)
    {
        var free = Math.Max(0, width - Gap);
        var left = free * CurveWeight / (CurveWeight + 1);
        return (left, free - left);
    }
}

/// <summary>
/// The footer row of <see cref="ChargeSession"/> (a wrapping flex row): the hint takes the room left of the button while it
/// gets at least <see cref="HintMinWidth"/>; otherwise the button moves to its own row under the hint, left-aligned or, with
/// <see cref="IsButtonFull"/>, across the whole row. Both are centered vertically on a shared row.
/// </summary>
internal sealed class ChargeSessionFooter : Panel
{
    public static readonly StyledProperty<double> HintMinWidthProperty =
        AvaloniaProperty.Register<ChargeSessionFooter, double>(nameof(HintMinWidth), 220);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<ChargeSessionFooter, double>(nameof(ColumnGap), 16);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<ChargeSessionFooter, double>(nameof(RowGap), 12);

    public static readonly StyledProperty<bool> IsButtonFullProperty =
        AvaloniaProperty.Register<ChargeSessionFooter, bool>(nameof(IsButtonFull));

    static ChargeSessionFooter()
    {
        AffectsMeasure<ChargeSessionFooter>(HintMinWidthProperty, ColumnGapProperty, RowGapProperty, IsButtonFullProperty);
    }

    public double HintMinWidth { get => GetValue(HintMinWidthProperty); set => SetValue(HintMinWidthProperty, value); }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    public bool IsButtonFull { get => GetValue(IsButtonFullProperty); set => SetValue(IsButtonFullProperty, value); }

    /// <summary>Whether the button sits on its own row.</summary>
    public bool IsWrapped { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return default;
        }
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 816;
        var hint = Children[0];
        var button = Children[1];
        button.Measure(new Size(width, double.PositiveInfinity));
        var buttonWidth = IsButtonFull ? width : button.DesiredSize.Width;
        IsWrapped = IsButtonFull || width - buttonWidth - ColumnGap < HintMinWidth;
        if (IsWrapped)
        {
            hint.Measure(new Size(width, double.PositiveInfinity));
            return new Size(width, hint.DesiredSize.Height + RowGap + button.DesiredSize.Height);
        }
        hint.Measure(new Size(width - buttonWidth - ColumnGap, double.PositiveInfinity));
        return new Size(width, Math.Max(hint.DesiredSize.Height, button.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return finalSize;
        }
        var hint = Children[0];
        var button = Children[1];
        var width = finalSize.Width;
        if (IsWrapped)
        {
            hint.Arrange(new Rect(0, 0, width, hint.DesiredSize.Height));
            var buttonWidth = IsButtonFull ? width : button.DesiredSize.Width;
            button.Arrange(new Rect(0, hint.DesiredSize.Height + RowGap, buttonWidth, button.DesiredSize.Height));
            return finalSize;
        }
        var bw = button.DesiredSize.Width;
        var row = Math.Max(hint.DesiredSize.Height, button.DesiredSize.Height);
        hint.Arrange(new Rect(0, (row - hint.DesiredSize.Height) / 2, width - bw - ColumnGap, hint.DesiredSize.Height));
        button.Arrange(new Rect(width - bw, (row - button.DesiredSize.Height) / 2, bw, button.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>
/// The curve plot with its axis labels: the plot full width, the power label over the full-power line at the left (its
/// bottom 4 above the line), and 0% / 50% / 100% 6 under the plot at the left, center and right.
/// </summary>
internal sealed class ChargeSessionPlotFrame : Panel
{
    public static readonly StyledProperty<double> LabelGapProperty =
        AvaloniaProperty.Register<ChargeSessionPlotFrame, double>(nameof(LabelGap), 6);

    static ChargeSessionPlotFrame()
    {
        AffectsMeasure<ChargeSessionPlotFrame>(LabelGapProperty);
    }

    public double LabelGap { get => GetValue(LabelGapProperty); set => SetValue(LabelGapProperty, value); }

    // Children: plot, power label, 0%, 50%, 100%.
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 5)
        {
            return default;
        }
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 480;
        Children[0].Measure(new Size(width, double.PositiveInfinity));
        var labels = 0.0;
        for (var i = 1; i < 5; i++)
        {
            Children[i].Measure(Size.Infinity);
            if (i > 1)
            {
                labels = Math.Max(labels, Children[i].DesiredSize.Height);
            }
        }
        return new Size(width, Children[0].DesiredSize.Height + LabelGap + labels);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 5)
        {
            return finalSize;
        }
        var width = finalSize.Width;
        var plot = Children[0];
        var plotHeight = plot.DesiredSize.Height;
        plot.Arrange(new Rect(0, 0, width, plotHeight));
        var power = Children[1];
        var lineY = plotHeight * ChargeSessionPlot.FullY / ChargeSessionPlot.UnitHeight;
        power.Arrange(new Rect(0, lineY - power.DesiredSize.Height - 4, power.DesiredSize.Width, power.DesiredSize.Height));
        var y = plotHeight + LabelGap;
        var zero = Children[2];
        zero.Arrange(new Rect(0, y, zero.DesiredSize.Width, zero.DesiredSize.Height));
        var half = Children[3];
        half.Arrange(new Rect((width - half.DesiredSize.Width) / 2, y, half.DesiredSize.Width, half.DesiredSize.Height));
        var full = Children[4];
        full.Arrange(new Rect(width - full.DesiredSize.Width, y, full.DesiredSize.Width, full.DesiredSize.Height));
        return finalSize;
    }
}
