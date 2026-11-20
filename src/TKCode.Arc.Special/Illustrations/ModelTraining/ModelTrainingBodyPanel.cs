using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Lays out a <see cref="ModelTraining"/>'s diagram (first child) and its readout panel (second child) (Arc Model
/// training, design reference): from <see cref="WideWidth"/> the panel stands <see cref="PanelWidth"/> wide beside the
/// diagram, <see cref="ColumnGap"/> apart and centred on it; narrower it sits under the diagram, <see cref="RowGap"/>
/// apart.
/// </summary>
internal sealed class ModelTrainingBodyPanel : Panel
{
    internal static readonly StyledProperty<double> WideWidthProperty =
        AvaloniaProperty.Register<ModelTrainingBodyPanel, double>(nameof(WideWidth), 720);

    internal static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<ModelTrainingBodyPanel, double>(nameof(PanelWidth), 232);

    internal static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<ModelTrainingBodyPanel, double>(nameof(ColumnGap), 32);

    internal static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<ModelTrainingBodyPanel, double>(nameof(RowGap), 24);

    static ModelTrainingBodyPanel()
    {
        AffectsMeasure<ModelTrainingBodyPanel>(WideWidthProperty, PanelWidthProperty, ColumnGapProperty, RowGapProperty);
    }

    public double WideWidth { get => GetValue(WideWidthProperty); set => SetValue(WideWidthProperty, value); }

    public double PanelWidth { get => GetValue(PanelWidthProperty); set => SetValue(PanelWidthProperty, value); }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    private bool IsWide(double width) => width >= WideWidth;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 904;
        var diagram = Children.Count > 0 ? Children[0] : null;
        var panel = Children.Count > 1 ? Children[1] : null;
        if (IsWide(width))
        {
            var left = Math.Max(0, width - PanelWidth - ColumnGap);
            diagram?.Measure(new Size(left, double.PositiveInfinity));
            panel?.Measure(new Size(PanelWidth, double.PositiveInfinity));
            var height = Math.Max(diagram?.DesiredSize.Height ?? 0, panel?.DesiredSize.Height ?? 0);
            return new Size(width, height);
        }
        diagram?.Measure(new Size(width, double.PositiveInfinity));
        panel?.Measure(new Size(width, double.PositiveInfinity));
        var stacked = (diagram?.DesiredSize.Height ?? 0) + (panel is { IsVisible: true } ? RowGap + panel.DesiredSize.Height : 0);
        return new Size(width, stacked);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        var diagram = Children.Count > 0 ? Children[0] : null;
        var panel = Children.Count > 1 ? Children[1] : null;
        if (IsWide(width))
        {
            var left = Math.Max(0, width - PanelWidth - ColumnGap);
            var height = finalSize.Height;
            var diagramHeight = diagram?.DesiredSize.Height ?? 0;
            diagram?.Arrange(new Rect(0, (height - diagramHeight) / 2, left, diagramHeight));
            var panelHeight = panel?.DesiredSize.Height ?? 0;
            panel?.Arrange(new Rect(left + ColumnGap, (height - panelHeight) / 2, PanelWidth, panelHeight));
            return finalSize;
        }
        var top = diagram?.DesiredSize.Height ?? 0;
        diagram?.Arrange(new Rect(0, 0, width, top));
        panel?.Arrange(new Rect(0, top + RowGap, width, panel.DesiredSize.Height));
        return finalSize;
    }
}
