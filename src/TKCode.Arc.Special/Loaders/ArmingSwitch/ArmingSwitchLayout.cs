using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Lays out an <see cref="ArmingSwitch"/> card (Arc Arming switch, design reference). Its children are the header, the
/// stage and the footer, in that order. Wide (<see cref="IsWide"/>): two columns at 1 : 1.55 with
/// <see cref="ColumnGap"/> between them; the stage fills the second column and its height is split across two rows, so
/// the header sits at the bottom of the first row and the footer at the top of the second, the extra height shared
/// equally (as a CSS grid with the stage spanning both auto rows). Narrow: header, stage and footer stacked with
/// <see cref="RowGap"/>.
/// </summary>
internal sealed class ArmingSwitchLayout : Panel
{
    public static readonly StyledProperty<bool> IsWideProperty =
        AvaloniaProperty.Register<ArmingSwitchLayout, bool>(nameof(IsWide), true);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<ArmingSwitchLayout, double>(nameof(ColumnGap), 24);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<ArmingSwitchLayout, double>(nameof(RowGap), 16);

    private const double SecondColumnShare = 1.55;

    static ArmingSwitchLayout()
    {
        AffectsMeasure<ArmingSwitchLayout>(IsWideProperty, ColumnGapProperty, RowGapProperty);
    }

    public bool IsWide { get => GetValue(IsWideProperty); set => SetValue(IsWideProperty, value); }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    private (Control? Header, Control? Stage, Control? Footer) Parts() =>
        (Children.Count > 0 ? Children[0] : null, Children.Count > 1 ? Children[1] : null, Children.Count > 2 ? Children[2] : null);

    private (double First, double Second) Columns(double width)
    {
        var free = Math.Max(0, width - ColumnGap);
        var first = free / (1 + SecondColumnShare);
        return (first, free - first);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var (header, stage, footer) = Parts();
        var width = double.IsInfinity(availableSize.Width) ? 760 : availableSize.Width;
        if (IsWide)
        {
            var (first, second) = Columns(width);
            header?.Measure(new Size(first, double.PositiveInfinity));
            footer?.Measure(new Size(first, double.PositiveInfinity));
            stage?.Measure(new Size(second, double.PositiveInfinity));
            var left = (header?.DesiredSize.Height ?? 0) + RowGap + (footer?.DesiredSize.Height ?? 0);
            return new Size(width, Math.Max(left, stage?.DesiredSize.Height ?? 0));
        }
        var height = 0.0;
        var count = 0;
        foreach (var child in new[] { header, stage, footer })
        {
            if (child is null || !child.IsVisible)
            {
                continue;
            }
            child.Measure(new Size(width, double.PositiveInfinity));
            height += child.DesiredSize.Height;
            count++;
        }
        return new Size(width, height + (Math.Max(0, count - 1) * RowGap));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (header, stage, footer) = Parts();
        var width = finalSize.Width;
        if (IsWide)
        {
            var (first, second) = Columns(width);
            var h1 = header?.DesiredSize.Height ?? 0;
            var h2 = footer?.DesiredSize.Height ?? 0;
            var total = Math.Max(finalSize.Height, h1 + RowGap + h2);
            var extra = Math.Max(0, total - RowGap - h1 - h2);
            var row1 = h1 + (extra / 2);
            header?.Arrange(new Rect(0, row1 - h1, first, h1));
            footer?.Arrange(new Rect(0, row1 + RowGap, first, h2));
            var hs = stage?.DesiredSize.Height ?? 0;
            stage?.Arrange(new Rect(first + ColumnGap, (total - hs) / 2, second, hs));
            return new Size(width, total);
        }
        var y = 0.0;
        foreach (var child in new[] { header, stage, footer })
        {
            if (child is null || !child.IsVisible)
            {
                continue;
            }
            var h = child.DesiredSize.Height;
            child.Arrange(new Rect(0, y, width, h));
            y += h + RowGap;
        }
        return finalSize;
    }
}

/// <summary>
/// The header of an <see cref="ArmingSwitch"/>: the titles and the status chip. Wide, the chip sits under the titles
/// (<see cref="Gap"/> 16). Narrow, they share a row with the chip at the right end when both fit and the chip wraps
/// under the titles (8 px) when not, as Arc's wrapping flex row.
/// </summary>
internal sealed class ArmingSwitchHeader : Panel
{
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<ArmingSwitchHeader, bool>(nameof(IsStacked), true);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ArmingSwitchHeader, double>(nameof(Gap), 16);

    private const double WrapGap = 8;
    private bool _row;

    static ArmingSwitchHeader()
    {
        AffectsMeasure<ArmingSwitchHeader>(IsStackedProperty, GapProperty);
    }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var titles = Children.Count > 0 ? Children[0] : null;
        var chip = Children.Count > 1 ? Children[1] : null;
        var width = double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width;
        chip?.Measure(Size.Infinity);
        var chipSize = chip?.DesiredSize ?? default;
        titles?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var natural = titles?.DesiredSize.Width ?? 0;
        _row = !IsStacked && natural + Gap + chipSize.Width <= width;
        if (_row)
        {
            titles?.Measure(new Size(width - Gap - chipSize.Width, double.PositiveInfinity));
            return new Size(width, Math.Max(titles?.DesiredSize.Height ?? 0, chipSize.Height));
        }
        titles?.Measure(new Size(width, double.PositiveInfinity));
        var gap = IsStacked ? Gap : WrapGap;
        return new Size(width, (titles?.DesiredSize.Height ?? 0) + gap + chipSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var titles = Children.Count > 0 ? Children[0] : null;
        var chip = Children.Count > 1 ? Children[1] : null;
        var chipSize = chip?.DesiredSize ?? default;
        if (_row)
        {
            titles?.Arrange(new Rect(0, 0, finalSize.Width - Gap - chipSize.Width, titles.DesiredSize.Height));
            chip?.Arrange(new Rect(finalSize.Width - chipSize.Width, 0, chipSize.Width, chipSize.Height));
            return finalSize;
        }
        var th = titles?.DesiredSize.Height ?? 0;
        titles?.Arrange(new Rect(0, 0, finalSize.Width, th));
        var gap = IsStacked ? Gap : WrapGap;
        chip?.Arrange(new Rect(0, th + gap, chipSize.Width, chipSize.Height));
        return finalSize;
    }
}
