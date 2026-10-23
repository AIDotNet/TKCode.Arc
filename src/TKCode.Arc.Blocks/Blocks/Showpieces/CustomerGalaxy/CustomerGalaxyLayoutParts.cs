using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The body of <see cref="CustomerGalaxy"/>: the stage (child 0) and the totals panel (child 1, drawing its own 1 px
/// divider). Beside each other when <see cref="IsSide"/> (the panel <see cref="PanelWidth"/> wide; the stage 16:9 up to
/// 560 px high, never shorter than the panel or <see cref="MinStageHeight"/>), otherwise stacked (the stage
/// <c>max(360, width × 0.52)</c> high, 380 under 620 px). A hidden panel leaves the stage alone.
/// </summary>
internal sealed class GalaxyBodyPanel : Panel
{
    public static readonly StyledProperty<bool> IsSideProperty =
        AvaloniaProperty.Register<GalaxyBodyPanel, bool>(nameof(IsSide));

    public static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<GalaxyBodyPanel, double>(nameof(PanelWidth), 288);

    public static readonly StyledProperty<double> MinStageHeightProperty =
        AvaloniaProperty.Register<GalaxyBodyPanel, double>(nameof(MinStageHeight), 360);

    static GalaxyBodyPanel()
    {
        AffectsMeasure<GalaxyBodyPanel>(IsSideProperty, PanelWidthProperty, MinStageHeightProperty);
    }

    /// <summary>The panel sits beside the stage.</summary>
    public bool IsSide { get => GetValue(IsSideProperty); set => SetValue(IsSideProperty, value); }

    /// <summary>Width of the side panel including its divider (288).</summary>
    public double PanelWidth { get => GetValue(PanelWidthProperty); set => SetValue(PanelWidthProperty, value); }

    /// <summary>The stage is never shorter than this beside the panel (360).</summary>
    public double MinStageHeight { get => GetValue(MinStageHeightProperty); set => SetValue(MinStageHeightProperty, value); }

    private Control? Stage => Children.Count > 0 ? Children[0] : null;

    private Control? Totals => Children.Count > 1 && Children[1].IsVisible ? Children[1] : null;

    /// <summary>The stage's height after the last measure.</summary>
    public double StageHeight { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 664;
        var totals = Totals;
        if (IsSide && totals is not null)
        {
            var panel = Math.Min(PanelWidth, width);
            totals.Measure(new Size(panel, double.PositiveInfinity));
            StageHeight = GalaxyLayout.SideStageHeight(Math.Max(0, width - panel), totals.DesiredSize.Height, MinStageHeight);
            Stage?.Measure(new Size(Math.Max(0, width - panel), StageHeight));
            return new Size(width, StageHeight);
        }
        StageHeight = GalaxyLayout.StageHeight(width);
        Stage?.Measure(new Size(width, StageHeight));
        var height = StageHeight;
        if (totals is not null)
        {
            totals.Measure(new Size(width, double.PositiveInfinity));
            height += totals.DesiredSize.Height;
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var totals = Totals;
        if (IsSide && totals is not null)
        {
            var panel = Math.Min(PanelWidth, finalSize.Width);
            var stageWidth = Math.Max(0, finalSize.Width - panel);
            Stage?.Arrange(new Rect(0, 0, stageWidth, finalSize.Height));
            totals.Arrange(new Rect(stageWidth, 0, panel, finalSize.Height));
            return finalSize;
        }
        Stage?.Arrange(new Rect(0, 0, finalSize.Width, StageHeight));
        totals?.Arrange(new Rect(0, StageHeight, finalSize.Width, Math.Max(0, finalSize.Height - StageHeight)));
        return finalSize;
    }
}

/// <summary>
/// The totals panel's two blocks: the scope (child 0: name, MRR, subtotals) and the list (child 1). Side by side
/// (0.44 / 0.56, <see cref="ColumnGap"/> apart) when at least <see cref="SplitWidth"/> wide, otherwise stacked
/// <see cref="RowGap"/> apart.
/// </summary>
internal sealed class GalaxyTotalsPanel : Panel
{
    public static readonly StyledProperty<double> SplitWidthProperty =
        AvaloniaProperty.Register<GalaxyTotalsPanel, double>(nameof(SplitWidth), 600);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<GalaxyTotalsPanel, double>(nameof(ColumnGap), 32);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<GalaxyTotalsPanel, double>(nameof(RowGap), 20);

    public static readonly StyledProperty<Thickness> PaddingProperty =
        Decorator.PaddingProperty.AddOwner<GalaxyTotalsPanel>();

    static GalaxyTotalsPanel()
    {
        AffectsMeasure<GalaxyTotalsPanel>(SplitWidthProperty, ColumnGapProperty, RowGapProperty, PaddingProperty);
    }

    /// <summary>From this width on the blocks sit side by side.</summary>
    public double SplitWidth { get => GetValue(SplitWidthProperty); set => SetValue(SplitWidthProperty, value); }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    /// <summary>The blocks are side by side (after the last measure).</summary>
    public bool IsSplit { get; private set; }

    private void Columns(double inner, out double left, out double right)
    {
        var free = Math.Max(0, inner - ColumnGap);
        left = free * 0.44;
        right = free - left;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var p = Padding;
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 288;
        var inner = Math.Max(0, width - p.Left - p.Right);
        IsSplit = width >= SplitWidth && Children.Count > 1;
        if (Children.Count == 0)
        {
            return new Size(width, p.Top + p.Bottom);
        }
        if (IsSplit)
        {
            Columns(inner, out var left, out var right);
            Children[0].Measure(new Size(left, double.PositiveInfinity));
            Children[1].Measure(new Size(right, double.PositiveInfinity));
            return new Size(width, p.Top + p.Bottom + Math.Max(Children[0].DesiredSize.Height, Children[1].DesiredSize.Height));
        }
        double height = 0;
        var first = true;
        foreach (var child in Children)
        {
            child.Measure(new Size(inner, double.PositiveInfinity));
            if (!child.IsVisible)
            {
                continue;
            }
            height += (first ? 0 : RowGap) + child.DesiredSize.Height;
            first = false;
        }
        return new Size(width, p.Top + p.Bottom + height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var p = Padding;
        var inner = Math.Max(0, finalSize.Width - p.Left - p.Right);
        if (Children.Count == 0)
        {
            return finalSize;
        }
        if (IsSplit)
        {
            Columns(inner, out var left, out var right);
            Children[0].Arrange(new Rect(p.Left, p.Top, left, Children[0].DesiredSize.Height));
            Children[1].Arrange(new Rect(p.Left + left + ColumnGap, p.Top, right, Children[1].DesiredSize.Height));
            return finalSize;
        }
        var y = p.Top;
        var first = true;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            y += first ? 0 : RowGap;
            child.Arrange(new Rect(p.Left, y, inner, child.DesiredSize.Height));
            y += child.DesiredSize.Height;
            first = false;
        }
        return finalSize;
    }
}

/// <summary>
/// The toolbar under the title: the grouping tabs (child 0) and the legend (child 1) on one row, the legend
/// right-aligned, when both fit with <see cref="ColumnGap"/> between them; otherwise the legend moves under the tabs
/// (<see cref="RowGap"/> apart) and wraps there.
/// </summary>
internal sealed class GalaxyToolbarPanel : Panel
{
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<GalaxyToolbarPanel, double>(nameof(ColumnGap), 20);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<GalaxyToolbarPanel, double>(nameof(RowGap), 12);

    static GalaxyToolbarPanel()
    {
        AffectsMeasure<GalaxyToolbarPanel>(ColumnGapProperty, RowGapProperty);
    }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>The legend shares the tabs' row (after the last measure).</summary>
    public bool IsOneRow { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 1000;
        var tabs = Children.Count > 0 ? Children[0] : null;
        var legend = Children.Count > 1 && Children[1].IsVisible ? Children[1] : null;
        tabs?.Measure(new Size(width, double.PositiveInfinity));
        var tabsSize = tabs?.DesiredSize ?? default;
        if (legend is null)
        {
            IsOneRow = true;
            return new Size(width, tabsSize.Height);
        }
        legend.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var single = legend.DesiredSize;
        IsOneRow = tabsSize.Width + ColumnGap + single.Width <= width;
        if (IsOneRow)
        {
            return new Size(width, Math.Max(tabsSize.Height, single.Height));
        }
        legend.Measure(new Size(width, double.PositiveInfinity));
        return new Size(width, tabsSize.Height + RowGap + legend.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var tabs = Children.Count > 0 ? Children[0] : null;
        var legend = Children.Count > 1 && Children[1].IsVisible ? Children[1] : null;
        var tabsSize = tabs?.DesiredSize ?? default;
        if (legend is null)
        {
            tabs?.Arrange(new Rect(0, 0, Math.Min(finalSize.Width, tabsSize.Width), tabsSize.Height));
            return finalSize;
        }
        if (IsOneRow)
        {
            var height = Math.Max(tabsSize.Height, legend.DesiredSize.Height);
            tabs?.Arrange(new Rect(0, (height - tabsSize.Height) / 2, tabsSize.Width, tabsSize.Height));
            legend.Arrange(new Rect(finalSize.Width - legend.DesiredSize.Width, (height - legend.DesiredSize.Height) / 2,
                legend.DesiredSize.Width, legend.DesiredSize.Height));
            return finalSize;
        }
        tabs?.Arrange(new Rect(0, 0, Math.Min(finalSize.Width, tabsSize.Width), tabsSize.Height));
        legend.Arrange(new Rect(0, tabsSize.Height + RowGap, finalSize.Width, legend.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>
/// The legend: one entry per health state (<see cref="ItemGap"/> apart, wrapping), then a 1 px
/// <see cref="GalaxyLegendDivider"/> and the size entry (the last two children) with <see cref="ItemGap"/> on both sides
/// of the divider. When the size entry starts a new line the divider is dropped.
/// </summary>
internal sealed class GalaxyLegendPanel : Panel
{
    public static readonly StyledProperty<double> ItemGapProperty =
        AvaloniaProperty.Register<GalaxyLegendPanel, double>(nameof(ItemGap), 16);

    public static readonly StyledProperty<double> LineGapProperty =
        AvaloniaProperty.Register<GalaxyLegendPanel, double>(nameof(LineGap), 6);

    static GalaxyLegendPanel()
    {
        AffectsMeasure<GalaxyLegendPanel>(ItemGapProperty, LineGapProperty);
    }

    public double ItemGap { get => GetValue(ItemGapProperty); set => SetValue(ItemGapProperty, value); }

    public double LineGap { get => GetValue(LineGapProperty); set => SetValue(LineGapProperty, value); }

    /// <summary>The divider stands before the size entry (after the last arrange).</summary>
    public bool IsDividerShown { get; private set; }

    private readonly List<Rect> _slots = new();

    private Size Flow(double width, bool record)
    {
        if (record)
        {
            _slots.Clear();
        }
        double x = 0, y = 0, line = 0, widest = 0;
        var count = Children.Count;
        var dividerIndex = count >= 2 && Children[count - 2] is GalaxyLegendDivider ? count - 2 : -1;
        var lineStart = 0;
        var shown = false;
        for (var i = 0; i < count; i++)
        {
            var child = Children[i];
            if (i == dividerIndex)
            {
                if (record)
                {
                    _slots.Add(default);
                }
                continue;
            }
            if (!child.IsVisible)
            {
                if (record)
                {
                    _slots.Add(default);
                }
                continue;
            }
            var size = child.DesiredSize;
            var isSize = dividerIndex >= 0 && i == count - 1;
            var lead = x == 0 ? 0 : isSize ? ItemGap * 2 + 1 : ItemGap;
            if (x > 0 && x + lead + size.Width > width)
            {
                if (record)
                {
                    Center(lineStart, _slots.Count, y, line);
                    lineStart = _slots.Count;
                }
                y += line + LineGap;
                x = 0;
                line = 0;
                lead = 0;
            }
            if (isSize && lead > 0)
            {
                shown = true;
                if (record)
                {
                    _slots[dividerIndex] = new Rect(x + ItemGap, y, 1, 0);
                }
            }
            x += lead;
            if (record)
            {
                _slots.Add(new Rect(x, y, size.Width, size.Height));
            }
            x += size.Width;
            line = Math.Max(line, size.Height);
            widest = Math.Max(widest, x);
        }
        if (record)
        {
            Center(lineStart, _slots.Count, y, line);
            IsDividerShown = shown;
            if (shown)
            {
                var d = _slots[dividerIndex];
                _slots[dividerIndex] = new Rect(d.X, y, 1, line);
            }
        }
        return new Size(widest, y + line);
    }

    /// <summary>Centers the entries of one line on its height.</summary>
    private void Center(int from, int to, double y, double line)
    {
        for (var i = from; i < to; i++)
        {
            var slot = _slots[i];
            if (slot.Width > 0)
            {
                _slots[i] = new Rect(slot.X, y + (line - slot.Height) / 2, slot.Width, slot.Height);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
        }
        return Flow(availableSize.Width, record: false);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Flow(finalSize.Width, record: true);
        for (var i = 0; i < Children.Count && i < _slots.Count; i++)
        {
            Children[i].Arrange(_slots[i]);
        }
        return finalSize;
    }
}

/// <summary>The 1 px rule between the health entries and the size entry of the legend.</summary>
internal sealed class GalaxyLegendDivider : Border
{
    protected override Type StyleKeyOverride => typeof(Border);
}
