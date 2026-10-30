using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Lays the formality and detail sliders out side by side in two equal columns, or (narrow cards) stacked full width.
/// </summary>
internal sealed class ToneTunerSliderRow : Panel
{
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<ToneTunerSliderRow, double>(nameof(ColumnGap), 24);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<ToneTunerSliderRow, double>(nameof(RowGap), 12);

    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<ToneTunerSliderRow, bool>(nameof(IsStacked));

    static ToneTunerSliderRow()
    {
        AffectsMeasure<ToneTunerSliderRow>(ColumnGapProperty, RowGapProperty, IsStackedProperty);
    }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    private double Column(double width, int count) =>
        IsStacked || count < 2 ? width : Math.Max(0, (width - ColumnGap * (count - 1)) / count);

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Children.Count;
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 480;
        var column = Column(width, count);
        var height = 0.0;
        var row = 0.0;
        for (var i = 0; i < count; i++)
        {
            var child = Children[i];
            child.Measure(new Size(column, double.PositiveInfinity));
            if (IsStacked)
            {
                height += child.DesiredSize.Height + (i > 0 ? RowGap : 0);
            }
            else
            {
                row = Math.Max(row, child.DesiredSize.Height);
            }
        }
        return new Size(width, IsStacked ? height : row);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = Children.Count;
        var column = Column(finalSize.Width, count);
        var x = 0.0;
        var y = 0.0;
        foreach (var child in Children)
        {
            if (IsStacked)
            {
                child.Arrange(new Rect(0, y, finalSize.Width, child.DesiredSize.Height));
                y += child.DesiredSize.Height + RowGap;
            }
            else
            {
                child.Arrange(new Rect(x, 0, column, finalSize.Height));
                x += column + ColumnGap;
            }
        }
        return finalSize;
    }
}
