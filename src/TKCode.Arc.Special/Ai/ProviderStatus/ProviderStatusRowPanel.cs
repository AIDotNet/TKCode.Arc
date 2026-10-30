using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The columns of a <see cref="ProviderStatus"/> row, and of the column header above the rows, so both line up. Its seven
/// children in order: mark, name, health, latency, uptime bar, 30 day percentage, chevron. Wide (one line): the mark, a
/// flexible name column (a share of the free width, clamped), fixed status, latency and percentage columns, the bar taking
/// the rest and a reserved chevron column; every cell is reserved, so a changing value never moves its neighbors. Narrow
/// (two lines, below the card's breakpoint): the mark, the name with the latency under it, the health at the right and
/// the chevron; then the full-width bar with the percentage at its right. Everything is centered on its line.
/// </summary>
internal sealed class ProviderStatusRowPanel : Panel
{
    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<ProviderStatusRowPanel, bool>(nameof(IsNarrow));

    public static readonly StyledProperty<double> StatusWidthProperty =
        AvaloniaProperty.Register<ProviderStatusRowPanel, double>(nameof(StatusWidth), 112);

    public static readonly StyledProperty<double> LatencyWidthProperty =
        AvaloniaProperty.Register<ProviderStatusRowPanel, double>(nameof(LatencyWidth), 68);

    public static readonly StyledProperty<double> UptimeWidthProperty =
        AvaloniaProperty.Register<ProviderStatusRowPanel, double>(nameof(UptimeWidth), 60);

    public static readonly StyledProperty<double> ChevronWidthProperty =
        AvaloniaProperty.Register<ProviderStatusRowPanel, double>(nameof(ChevronWidth), 16);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<ProviderStatusRowPanel, double>(nameof(ColumnGap), 12);

    public static readonly StyledProperty<double> MarkGapProperty =
        AvaloniaProperty.Register<ProviderStatusRowPanel, double>(nameof(MarkGap), 10);

    public static readonly StyledProperty<double> NameShareProperty =
        AvaloniaProperty.Register<ProviderStatusRowPanel, double>(nameof(NameShare), 0.42);

    public static readonly StyledProperty<double> NameMinWidthProperty =
        AvaloniaProperty.Register<ProviderStatusRowPanel, double>(nameof(NameMinWidth), 110);

    public static readonly StyledProperty<double> NameMaxWidthProperty =
        AvaloniaProperty.Register<ProviderStatusRowPanel, double>(nameof(NameMaxWidth), 240);

    public static readonly StyledProperty<double> LineGapProperty =
        AvaloniaProperty.Register<ProviderStatusRowPanel, double>(nameof(LineGap), 10);

    private const int MarkIndex = 0;
    private const int NameIndex = 1;
    private const int HealthIndex = 2;
    private const int LatencyIndex = 3;
    private const int BarIndex = 4;
    private const int UptimeIndex = 5;
    private const int ChevronIndex = 6;

    static ProviderStatusRowPanel()
    {
        AffectsMeasure<ProviderStatusRowPanel>(IsNarrowProperty, StatusWidthProperty, LatencyWidthProperty, UptimeWidthProperty,
            ChevronWidthProperty, ColumnGapProperty, MarkGapProperty, NameShareProperty, NameMinWidthProperty, NameMaxWidthProperty,
            LineGapProperty);
    }

    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    public double StatusWidth { get => GetValue(StatusWidthProperty); set => SetValue(StatusWidthProperty, value); }

    public double LatencyWidth { get => GetValue(LatencyWidthProperty); set => SetValue(LatencyWidthProperty, value); }

    public double UptimeWidth { get => GetValue(UptimeWidthProperty); set => SetValue(UptimeWidthProperty, value); }

    public double ChevronWidth { get => GetValue(ChevronWidthProperty); set => SetValue(ChevronWidthProperty, value); }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double MarkGap { get => GetValue(MarkGapProperty); set => SetValue(MarkGapProperty, value); }

    public double NameShare { get => GetValue(NameShareProperty); set => SetValue(NameShareProperty, value); }

    public double NameMinWidth { get => GetValue(NameMinWidthProperty); set => SetValue(NameMinWidthProperty, value); }

    public double NameMaxWidth { get => GetValue(NameMaxWidthProperty); set => SetValue(NameMaxWidthProperty, value); }

    public double LineGap { get => GetValue(LineGapProperty); set => SetValue(LineGapProperty, value); }

    /// <summary>The rectangles of the seven cells from the last arrange (tests).</summary>
    internal Rect[] Cells { get; } = new Rect[7];

    private Control? Child(int index) => index < Children.Count && Children[index].IsVisible ? Children[index] : null;

    private double MarkWidth(Size available)
    {
        if (Child(MarkIndex) is not { } mark)
        {
            return 0;
        }
        mark.Measure(available);
        return mark.DesiredSize.Width;
    }

    /// <summary>The wide columns' widths for <paramref name="width"/>: mark, name and bar.</summary>
    private (double Mark, double Name, double Bar) WideColumns(double width, Size available)
    {
        var mark = MarkWidth(available);
        var gap = ColumnGap;
        var free = width - mark - (mark > 0 ? MarkGap : 0) - StatusWidth - LatencyWidth - UptimeWidth - ChevronWidth - 5 * gap;
        free = Math.Max(0, free);
        var name = Math.Min(free, Math.Clamp(free * NameShare, NameMinWidth, Math.Max(NameMinWidth, NameMaxWidth)));
        return (mark, name, Math.Max(0, free - name));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 640;
        var height = 0.0;
        if (!IsNarrow)
        {
            var (_, name, bar) = WideColumns(width, availableSize);
            height = Math.Max(height, MeasureCell(NameIndex, name));
            height = Math.Max(height, MeasureCell(HealthIndex, StatusWidth));
            height = Math.Max(height, MeasureCell(LatencyIndex, LatencyWidth));
            height = Math.Max(height, MeasureCell(BarIndex, bar));
            height = Math.Max(height, MeasureCell(UptimeIndex, UptimeWidth));
            height = Math.Max(height, MeasureCell(ChevronIndex, ChevronWidth));
            height = Math.Max(height, Child(MarkIndex)?.DesiredSize.Height ?? 0);
            return new Size(width, height);
        }
        var (line1, line2) = NarrowLines(width, availableSize);
        return new Size(width, line1 + (line2 > 0 ? LineGap + line2 : 0));
    }

    private double MeasureCell(int index, double width)
    {
        if (Child(index) is not { } child)
        {
            return 0;
        }
        child.Measure(new Size(Math.Max(0, width), double.PositiveInfinity));
        return child.DesiredSize.Height;
    }

    private (double Line1, double Line2) NarrowLines(double width, Size available)
    {
        var mark = MarkWidth(available);
        var gap = ColumnGap;
        var health = Math.Min(StatusWidth, Measured(HealthIndex, StatusWidth).Width);
        var nameWidth = NarrowNameWidth(width, mark, health);
        var name = MeasureCell(NameIndex, nameWidth);
        var latency = MeasureCell(LatencyIndex, nameWidth);
        MeasureCell(ChevronIndex, ChevronWidth);
        var line1 = Math.Max(Math.Max(name + latency, Child(MarkIndex)?.DesiredSize.Height ?? 0),
            Math.Max(Child(HealthIndex)?.DesiredSize.Height ?? 0, Child(ChevronIndex)?.DesiredSize.Height ?? 0));
        var uptime = Measured(UptimeIndex, UptimeWidth);
        var bar = MeasureCell(BarIndex, Math.Max(0, width - ChevronWidth - gap - UptimeWidth - gap));
        return (line1, Math.Max(bar, uptime.Height));
    }

    private double NarrowNameWidth(double width, double mark, double health) =>
        Math.Max(0, width - mark - (mark > 0 ? MarkGap : 0) - health - ColumnGap - ChevronWidth - ColumnGap);

    private Size Measured(int index, double width)
    {
        if (Child(index) is not { } child)
        {
            return default;
        }
        child.Measure(new Size(width, double.PositiveInfinity));
        return child.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        var gap = ColumnGap;
        Array.Clear(Cells);
        if (!IsNarrow)
        {
            var (mark, name, bar) = WideColumns(width, finalSize);
            var h = finalSize.Height;
            var x = 0.0;
            Place(MarkIndex, x, 0, mark, h);
            x += mark + (mark > 0 ? MarkGap : 0);
            Place(NameIndex, x, 0, name, h);
            x += name + gap;
            Place(HealthIndex, x, 0, StatusWidth, h);
            x += StatusWidth + gap;
            Place(LatencyIndex, x, 0, LatencyWidth, h);
            x += LatencyWidth + gap;
            Place(BarIndex, x, 0, bar, h);
            x += bar + gap;
            Place(UptimeIndex, x, 0, UptimeWidth, h);
            x += UptimeWidth + gap;
            Place(ChevronIndex, x, 0, ChevronWidth, h);
            return finalSize;
        }
        var markWidth = Child(MarkIndex)?.DesiredSize.Width ?? 0;
        var health = Math.Min(StatusWidth, Child(HealthIndex)?.DesiredSize.Width ?? 0);
        var nameWidth = NarrowNameWidth(width, markWidth, health);
        var nameHeight = Child(NameIndex)?.DesiredSize.Height ?? 0;
        var latencyHeight = Child(LatencyIndex)?.DesiredSize.Height ?? 0;
        var line1 = Math.Max(Math.Max(nameHeight + latencyHeight, Child(MarkIndex)?.DesiredSize.Height ?? 0),
            Math.Max(Child(HealthIndex)?.DesiredSize.Height ?? 0, Child(ChevronIndex)?.DesiredSize.Height ?? 0));
        var nameX = markWidth + (markWidth > 0 ? MarkGap : 0);
        var stackTop = (line1 - nameHeight - latencyHeight) / 2;
        Place(MarkIndex, 0, 0, markWidth, line1);
        Place(NameIndex, nameX, stackTop, nameWidth, nameHeight);
        Place(LatencyIndex, nameX, stackTop + nameHeight, nameWidth, latencyHeight);
        var chevronX = width - ChevronWidth;
        Place(ChevronIndex, chevronX, 0, ChevronWidth, line1);
        Place(HealthIndex, chevronX - gap - health, 0, health, line1);
        var top = line1 + LineGap;
        var line2 = Math.Max(0, finalSize.Height - top);
        var uptimeRight = chevronX - gap;
        Place(UptimeIndex, uptimeRight - UptimeWidth, top, UptimeWidth, line2);
        Place(BarIndex, 0, top, Math.Max(0, uptimeRight - UptimeWidth - gap), line2);
        return finalSize;
    }

    private void Place(int index, double x, double y, double width, double height)
    {
        var rect = new Rect(x, y, Math.Max(0, width), Math.Max(0, height));
        Cells[index] = rect;
        if (Child(index) is not { } child)
        {
            return;
        }
        // Each child is centered on its line at its own height (a cell never stretches its content).
        var childHeight = Math.Min(child.DesiredSize.Height, rect.Height);
        child.Arrange(new Rect(rect.X, rect.Y + (rect.Height - childHeight) / 2, rect.Width, childHeight));
    }
}
