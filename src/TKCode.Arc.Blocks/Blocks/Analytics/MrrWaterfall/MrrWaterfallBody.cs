using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The body of <see cref="MrrWaterfall"/> (Arc's two-column grid): the chart takes the remaining width and the panel
/// takes <see cref="PanelWidth"/> beside it, <see cref="ColumnGap"/> apart; when <see cref="IsStacked"/> the panel goes under
/// the chart at the full width. Both children start at the top.
/// </summary>
internal sealed class MrrWaterfallBody : Panel
{
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<MrrWaterfallBody, double>(nameof(ColumnGap), 24);

    public static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<MrrWaterfallBody, double>(nameof(PanelWidth), 264);

    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<MrrWaterfallBody, bool>(nameof(IsStacked));

    static MrrWaterfallBody()
    {
        AffectsMeasure<MrrWaterfallBody>(ColumnGapProperty, PanelWidthProperty, IsStackedProperty);
    }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double PanelWidth { get => GetValue(PanelWidthProperty); set => SetValue(PanelWidthProperty, value); }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    private (Control? Plot, Control? Panel) Parts() => (Children.Count > 0 ? Children[0] : null, Children.Count > 1 ? Children[1] : null);

    protected override Size MeasureOverride(Size availableSize)
    {
        var (plot, panel) = Parts();
        var width = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
        if (plot is null)
        {
            return default;
        }
        if (panel is null)
        {
            plot.Measure(new Size(width, double.PositiveInfinity));
            return plot.DesiredSize;
        }
        if (IsStacked)
        {
            plot.Measure(new Size(width, double.PositiveInfinity));
            panel.Measure(new Size(width, double.PositiveInfinity));
            return new Size(width, plot.DesiredSize.Height + ColumnGap + panel.DesiredSize.Height);
        }
        var chartWidth = Math.Max(0, width - ColumnGap - PanelWidth);
        plot.Measure(new Size(chartWidth, double.PositiveInfinity));
        panel.Measure(new Size(PanelWidth, double.PositiveInfinity));
        return new Size(width, Math.Max(plot.DesiredSize.Height, panel.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (plot, panel) = Parts();
        if (plot is null)
        {
            return finalSize;
        }
        if (panel is null)
        {
            plot.Arrange(new Rect(0, 0, finalSize.Width, plot.DesiredSize.Height));
            return finalSize;
        }
        if (IsStacked)
        {
            plot.Arrange(new Rect(0, 0, finalSize.Width, plot.DesiredSize.Height));
            panel.Arrange(new Rect(0, plot.DesiredSize.Height + ColumnGap, finalSize.Width, panel.DesiredSize.Height));
            return finalSize;
        }
        var chartWidth = Math.Max(0, finalSize.Width - ColumnGap - PanelWidth);
        plot.Arrange(new Rect(0, 0, chartWidth, plot.DesiredSize.Height));
        panel.Arrange(new Rect(chartWidth + ColumnGap, 0, PanelWidth, panel.DesiredSize.Height));
        return finalSize;
    }
}
