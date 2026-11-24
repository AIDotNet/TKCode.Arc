using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Lays the TimePicker's visible columns side by side in equal widths with <see cref="Spacing"/> between them. It asks
/// for at least <see cref="ColumnMinWidth"/> (or the widest column's values) per column, so the drop-down grows past a
/// narrow trigger.
/// </summary>
internal sealed class TimePickerColumnsPanel : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<TimePickerColumnsPanel, double>(nameof(Spacing), 4);

    public static readonly StyledProperty<double> ColumnMinWidthProperty =
        AvaloniaProperty.Register<TimePickerColumnsPanel, double>(nameof(ColumnMinWidth), 56);

    static TimePickerColumnsPanel()
    {
        AffectsMeasure<TimePickerColumnsPanel>(SpacingProperty, ColumnMinWidthProperty);
    }

    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    public double ColumnMinWidth { get => GetValue(ColumnMinWidthProperty); set => SetValue(ColumnMinWidthProperty, value); }

    private int VisibleCount
    {
        get
        {
            var count = 0;
            foreach (var child in Children)
            {
                if (child.IsVisible)
                {
                    count++;
                }
            }
            return count;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = VisibleCount;
        if (count == 0)
        {
            return default;
        }
        // Children are measured at their natural width (the widest value of a column, such as a long AM / PM word) and
        // every column gets the widest of them, at least ColumnMinWidth. They are never arranged narrower than that: a
        // child measured wider than it is arranged would keep the wider size.
        var gaps = Spacing * (count - 1);
        var column = ColumnMinWidth;
        var height = 0.0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            column = Math.Max(column, Math.Ceiling(child.DesiredSize.Width));
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(column * count + gaps, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = VisibleCount;
        if (count == 0)
        {
            return finalSize;
        }
        var width = Math.Max(0, (finalSize.Width - Spacing * (count - 1)) / count);
        var x = 0.0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width + Spacing;
        }
        return finalSize;
    }
}
