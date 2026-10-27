using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A metric tab: a button holding an <see cref="AnalyticsKpiTile"/> (label, counting value, change). Hover tints it
/// SurfaceMuted in 160ms; keyboard focus shows a 2px ring inside its edge (Arc's outside ring was cut off by its
/// neighbours and the block's clip). Screen readers hear a tab with its selection state.
/// </summary>
internal sealed class MetricsDashboardTab : Button
{
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<MetricsDashboardTab, bool>(nameof(IsSelected));

    public MetricsDashboardTab()
    {
        Content = Tile;
    }

    /// <summary>Whether this is the selected metric.</summary>
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <summary>Position in the strip.</summary>
    public int Index { get; set; }

    /// <summary>The tile inside.</summary>
    public AnalyticsKpiTile Tile { get; } = new()
    {
        NumberMotion = AnalyticsNumberMotion.CountUp,
        IsHitTestVisible = false
    };

    /// <summary>Set by the strip so the peer can select through it.</summary>
    internal Action<int>? SelectRequested { get; set; }

    protected override Type StyleKeyOverride => typeof(MetricsDashboardTab);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(":selected", IsSelected);
            Tile.IsLabelStrong = IsSelected;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TabPeer(this);

    private sealed class TabPeer(MetricsDashboardTab owner) : ButtonAutomationPeer(owner), ISelectionItemProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

        public bool IsSelected => owner.IsSelected;

        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }

        public void Select() => owner.SelectRequested?.Invoke(owner.Index);
    }
}

/// <summary>
/// Lays the metric tabs out in one row (large) or a 2 × 2 grid (medium and small), draws the tablist's lines (a 1px
/// Border line above and below, one between columns, one between rows) and glides the 2px accent underline to the
/// selected tab on Smooth (x and y, so it can change rows), across the tab's last pixel row and the line under it.
/// </summary>
internal sealed class MetricsDashboardTabStrip : Panel
{
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<MetricsDashboardTabStrip, int>(nameof(Columns), 4);

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<MetricsDashboardTabStrip, int>(nameof(SelectedIndex));

    public static readonly StyledProperty<double> UnderlineThicknessProperty =
        AvaloniaProperty.Register<MetricsDashboardTabStrip, double>(nameof(UnderlineThickness), 2);

    private readonly AnalyticsPalette _palette = new();
    private readonly GlidingIndicator _underline = new() { IsHitTestVisible = false, ZIndex = 1 };
    private readonly List<double> _columnLines = new();
    private readonly List<double> _rowLines = new();
    private bool _placed;
    private bool _selectionMoved;

    static MetricsDashboardTabStrip()
    {
        AffectsMeasure<MetricsDashboardTabStrip>(ColumnsProperty);
        AffectsArrange<MetricsDashboardTabStrip>(UnderlineThicknessProperty);
    }

    private readonly MetricsDashboardLineLayer _lines;

    public MetricsDashboardTabStrip()
    {
        _lines = new MetricsDashboardLineLayer(DrawLines) { IsHitTestVisible = false };
        Children.Add(_lines);
        Children.Add(_underline);
        ActualThemeVariantChanged += (_, _) => Refresh();
        AttachedToVisualTree += (_, _) => Refresh();
    }

    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    public double UnderlineThickness { get => GetValue(UnderlineThicknessProperty); set => SetValue(UnderlineThicknessProperty, value); }

    /// <summary>The underline (tests).</summary>
    internal GlidingIndicator Underline => _underline;

    /// <summary>The tabs, in order.</summary>
    public IEnumerable<MetricsDashboardTab> Tabs => Children.OfType<MetricsDashboardTab>();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty)
        {
            _selectionMoved = true;
            InvalidateArrange();
        }
    }

    private void Refresh()
    {
        if (VisualRoot is null)
        {
            return;
        }
        _palette.Refresh(this);
        _underline.Fill = _palette.Brush(_palette.Accent);
        _lines.InvalidateVisual();
    }

    private int ColumnCount(int count) => Math.Max(1, Math.Min(Columns, Math.Max(1, count)));

    protected override Size MeasureOverride(Size availableSize)
    {
        var tabs = Tabs.ToList();
        var columns = ColumnCount(tabs.Count);
        var rows = (tabs.Count + columns - 1) / columns;
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        var cell = columns > 0 && width > 0 ? Math.Max(0, (width - (columns - 1)) / columns) : double.PositiveInfinity;
        var rowHeights = new double[Math.Max(1, rows)];
        double natural = 0;
        for (var i = 0; i < tabs.Count; i++)
        {
            tabs[i].Measure(new Size(cell, double.PositiveInfinity));
            rowHeights[i / columns] = Math.Max(rowHeights[i / columns], tabs[i].DesiredSize.Height);
            natural = Math.Max(natural, tabs[i].DesiredSize.Width);
        }
        _underline.Measure(Size.Infinity);
        _lines.Measure(Size.Infinity);
        var height = 2 + rowHeights.Sum() + Math.Max(0, rows - 1);
        return new Size(width > 0 ? width : natural * columns, tabs.Count == 0 ? 0 : height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var tabs = Tabs.ToList();
        var columns = ColumnCount(tabs.Count);
        var rows = (tabs.Count + columns - 1) / columns;
        var rowHeights = new double[Math.Max(1, rows)];
        for (var i = 0; i < tabs.Count; i++)
        {
            rowHeights[i / columns] = Math.Max(rowHeights[i / columns], tabs[i].DesiredSize.Height);
        }
        var cell = (finalSize.Width - (columns - 1)) / columns;
        _columnLines.Clear();
        _rowLines.Clear();
        for (var c = 1; c < columns; c++)
        {
            _columnLines.Add(Math.Round(c * (cell + 1) - 1));
        }
        double y = 1;
        for (var r = 0; r < rows; r++)
        {
            if (r > 0)
            {
                _rowLines.Add(y);
                y += 1;
            }
            for (var c = 0; c < columns; c++)
            {
                var i = r * columns + c;
                if (i >= tabs.Count)
                {
                    break;
                }
                var left = c == 0 ? 0 : Math.Round(c * (cell + 1));
                var right = c == columns - 1 ? finalSize.Width : Math.Round((c + 1) * (cell + 1) - 1);
                tabs[i].Arrange(new Rect(left, y, Math.Max(0, right - left), rowHeights[r]));
            }
            y += rowHeights[r];
        }
        _underline.Arrange(new Rect(finalSize));
        _lines.Arrange(new Rect(finalSize));
        _lines.InvalidateVisual();
        PlaceUnderline(tabs);
        return finalSize;
    }

    private void PlaceUnderline(List<MetricsDashboardTab> tabs)
    {
        var index = SelectedIndex;
        if (index < 0 || index >= tabs.Count)
        {
            _underline.Hide(false);
            _placed = false;
            return;
        }
        var tab = tabs[index].Bounds;
        if (tab.Width <= 0)
        {
            return;
        }
        var thickness = UnderlineThickness;
        var target = new Rect(tab.X, tab.Bottom - thickness / 2, tab.Width, thickness);
        if (_underline.HasTarget && _underline.CurrentRect == target)
        {
            _selectionMoved = false;
            return;
        }
        // Only a new selection glides; a resize or a relayout (lg → md) puts the underline in place at once.
        _underline.MoveTo(target, AnalyticsSprings.Smooth, animate: _placed && _selectionMoved && IsLoaded);
        _placed = true;
        _selectionMoved = false;
    }

    private void DrawLines(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var pen = _palette.Pen(_palette.Border, 1, cap: PenLineCap.Flat);
        var size = Bounds.Size;
        context.DrawLine(pen, new Point(0, 0.5), new Point(size.Width, 0.5));
        context.DrawLine(pen, new Point(0, size.Height - 0.5), new Point(size.Width, size.Height - 0.5));
        foreach (var x in _columnLines)
        {
            context.DrawLine(pen, new Point(x + 0.5, 1), new Point(x + 0.5, size.Height - 1));
        }
        foreach (var y in _rowLines)
        {
            context.DrawLine(pen, new Point(0, y + 0.5), new Point(size.Width, y + 0.5));
        }
    }
}

/// <summary>
/// The header row: the titles on the left and the actions on the right; when both do not fit on one line (Arc's
/// <c>flex-wrap</c>) the actions move under the titles, left aligned, 12 below. With <see cref="StretchActions"/> (small
/// layout) the actions take the whole second row.
/// </summary>
internal sealed class MetricsDashboardHeaderPanel : Panel
{
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<MetricsDashboardHeaderPanel, double>(nameof(ColumnGap), 16);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<MetricsDashboardHeaderPanel, double>(nameof(RowGap), 12);

    public static readonly StyledProperty<bool> StretchActionsProperty =
        AvaloniaProperty.Register<MetricsDashboardHeaderPanel, bool>(nameof(StretchActions));

    static MetricsDashboardHeaderPanel()
    {
        AffectsMeasure<MetricsDashboardHeaderPanel>(ColumnGapProperty, RowGapProperty, StretchActionsProperty);
    }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    public bool StretchActions { get => GetValue(StretchActionsProperty); set => SetValue(StretchActionsProperty, value); }

    /// <summary>True when the actions sit on their own row.</summary>
    public bool IsWrapped { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return default;
        }
        var titles = Children[0];
        var actions = Children[1];
        actions.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        titles.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        var width = availableSize.Width;
        var oneRow = titles.DesiredSize.Width + ColumnGap + actions.DesiredSize.Width;
        IsWrapped = StretchActions || (double.IsFinite(width) && oneRow > width);
        if (!IsWrapped)
        {
            titles.Measure(new Size(Math.Max(0, width - ColumnGap - actions.DesiredSize.Width), availableSize.Height));
            return new Size(double.IsFinite(width) ? width : oneRow, Math.Max(titles.DesiredSize.Height, actions.DesiredSize.Height));
        }
        titles.Measure(new Size(width, availableSize.Height));
        if (StretchActions && double.IsFinite(width))
        {
            actions.Measure(new Size(width, availableSize.Height));
        }
        var w = double.IsFinite(width) ? width : Math.Max(titles.DesiredSize.Width, actions.DesiredSize.Width);
        return new Size(w, titles.DesiredSize.Height + RowGap + actions.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return finalSize;
        }
        var titles = Children[0];
        var actions = Children[1];
        if (!IsWrapped)
        {
            var height = finalSize.Height;
            var aw = actions.DesiredSize.Width;
            actions.Arrange(new Rect(finalSize.Width - aw, (height - actions.DesiredSize.Height) / 2, aw, actions.DesiredSize.Height));
            titles.Arrange(new Rect(0, (height - titles.DesiredSize.Height) / 2, Math.Max(0, finalSize.Width - aw - ColumnGap), titles.DesiredSize.Height));
            return finalSize;
        }
        titles.Arrange(new Rect(0, 0, finalSize.Width, titles.DesiredSize.Height));
        var width = StretchActions ? finalSize.Width : Math.Min(finalSize.Width, actions.DesiredSize.Width);
        actions.Arrange(new Rect(0, titles.DesiredSize.Height + RowGap, width, actions.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>
/// The two ranked lists: side by side (large) with a 1px line between them, or stacked (medium, small) with a line between;
/// a 1px line runs along the top.
/// </summary>
internal sealed class MetricsDashboardSplitPanel : Panel
{
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<MetricsDashboardSplitPanel, bool>(nameof(IsStacked));

    private readonly AnalyticsPalette _palette = new();
    private double _divider;

    static MetricsDashboardSplitPanel()
    {
        AffectsMeasure<MetricsDashboardSplitPanel>(IsStackedProperty);
    }

    private readonly MetricsDashboardLineLayer _lines;

    public MetricsDashboardSplitPanel()
    {
        _lines = new MetricsDashboardLineLayer(DrawLines) { IsHitTestVisible = false };
        Children.Add(_lines);
        ActualThemeVariantChanged += (_, _) =>
        {
            if (VisualRoot is not null)
            {
                _palette.Refresh(this);
                _lines.InvalidateVisual();
            }
        };
    }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    private List<Control> Sections() => Children.Where(c => c.IsVisible && c != _lines).ToList();

    protected override Size MeasureOverride(Size availableSize)
    {
        _lines.Measure(Size.Infinity);
        var visible = Sections();
        var width = availableSize.Width;
        if (IsStacked || visible.Count < 2)
        {
            double height = 1;
            double w = 0;
            for (var i = 0; i < visible.Count; i++)
            {
                visible[i].Measure(new Size(width, double.PositiveInfinity));
                height += visible[i].DesiredSize.Height + (i > 0 ? 1 : 0);
                w = Math.Max(w, visible[i].DesiredSize.Width);
            }
            return new Size(double.IsFinite(width) ? width : w, visible.Count == 0 ? 0 : height);
        }
        var half = double.IsFinite(width) ? (width - 1) / 2 : double.PositiveInfinity;
        visible[0].Measure(new Size(half, double.PositiveInfinity));
        visible[1].Measure(new Size(half, double.PositiveInfinity));
        var h = Math.Max(visible[0].DesiredSize.Height, visible[1].DesiredSize.Height);
        return new Size(double.IsFinite(width) ? width : visible[0].DesiredSize.Width * 2 + 1, 1 + h);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var visible = Sections();
        _lines.Arrange(new Rect(finalSize));
        _lines.InvalidateVisual();
        _divider = -1;
        if (IsStacked || visible.Count < 2)
        {
            double y = 1;
            for (var i = 0; i < visible.Count; i++)
            {
                if (i > 0)
                {
                    _divider = y;
                    y += 1;
                }
                visible[i].Arrange(new Rect(0, y, finalSize.Width, visible[i].DesiredSize.Height));
                y += visible[i].DesiredSize.Height;
            }
            return finalSize;
        }
        var half = Math.Round((finalSize.Width - 1) / 2);
        visible[0].Arrange(new Rect(0, 1, half, finalSize.Height - 1));
        visible[1].Arrange(new Rect(half + 1, 1, finalSize.Width - half - 1, finalSize.Height - 1));
        _divider = half;
        return finalSize;
    }

    private void DrawLines(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var pen = _palette.Pen(_palette.Border, 1, cap: PenLineCap.Flat);
        var size = Bounds.Size;
        if (size.Height <= 0)
        {
            return;
        }
        context.DrawLine(pen, new Point(0, 0.5), new Point(size.Width, 0.5));
        if (_divider < 0)
        {
            return;
        }
        if (IsStacked)
        {
            context.DrawLine(pen, new Point(0, _divider + 0.5), new Point(size.Width, _divider + 0.5));
        }
        else
        {
            context.DrawLine(pen, new Point(_divider + 0.5, 1), new Point(_divider + 0.5, size.Height));
        }
    }
}

/// <summary>
/// The Export button: download glyph and "Export"; while confirmed (<see cref="IsConfirmed"/>) a check and "Exported". Both
/// texts are stacked so the button keeps the wider one's width; they cross-fade in 160ms, and the glyphs swap with a
/// fade and a scale from .8 (160ms). Hover tints it SurfaceMuted, pressing scales it to .97 (160ms).
/// </summary>
internal sealed class MetricsDashboardExportButton : Button
{
    public static readonly StyledProperty<bool> IsConfirmedProperty =
        AvaloniaProperty.Register<MetricsDashboardExportButton, bool>(nameof(IsConfirmed));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MetricsDashboardExportButton, string?>(nameof(Text));

    public static readonly StyledProperty<string?> ConfirmedTextProperty =
        AvaloniaProperty.Register<MetricsDashboardExportButton, string?>(nameof(ConfirmedText));

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<MetricsDashboardExportButton, double>(nameof(GlyphSize), 14);

    public static readonly StyledProperty<double> GlyphGapProperty =
        AvaloniaProperty.Register<MetricsDashboardExportButton, double>(nameof(GlyphGap), 6);

    public bool IsConfirmed { get => GetValue(IsConfirmedProperty); set => SetValue(IsConfirmedProperty, value); }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public string? ConfirmedText { get => GetValue(ConfirmedTextProperty); set => SetValue(ConfirmedTextProperty, value); }

    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    public double GlyphGap { get => GetValue(GlyphGapProperty); set => SetValue(GlyphGapProperty, value); }

    protected override Type StyleKeyOverride => typeof(MetricsDashboardExportButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsConfirmedProperty || change.Property == TextProperty || change.Property == ConfirmedTextProperty)
        {
            PseudoClasses.Set(":confirmed", IsConfirmed);
            AutomationProperties.SetName(this, IsConfirmed ? ConfirmedText : Text);
        }
    }
}

/// <summary>
/// The 14px Compare check box: Surface with a BorderStrong edge, or ControlOn with a Background check when on (colors
/// 160ms); the focus ring is drawn around the box (Arc's ring sat on an invisible input and never showed).
/// </summary>
internal sealed class MetricsDashboardCompareBox : ToggleButton
{
    public static readonly StyledProperty<double> BoxSizeProperty =
        AvaloniaProperty.Register<MetricsDashboardCompareBox, double>(nameof(BoxSize), 14);

    public double BoxSize { get => GetValue(BoxSizeProperty); set => SetValue(BoxSizeProperty, value); }

    protected override Type StyleKeyOverride => typeof(MetricsDashboardCompareBox);

    protected override AutomationPeer OnCreateAutomationPeer() => new CheckPeer(this);

    private sealed class CheckPeer(MetricsDashboardCompareBox owner) : ToggleButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.CheckBox;
    }
}

/// <summary>A legend swatch, 10 × 2: solid accent, or the previous period's TextMuted dashes (3 on, 2 off).</summary>
internal sealed class MetricsDashboardSwatch : Control
{
    public static readonly StyledProperty<bool> IsDashedProperty =
        AvaloniaProperty.Register<MetricsDashboardSwatch, bool>(nameof(IsDashed));

    private readonly AnalyticsPalette _palette = new();

    static MetricsDashboardSwatch()
    {
        AffectsRender<MetricsDashboardSwatch>(IsDashedProperty);
    }

    public MetricsDashboardSwatch()
    {
        Width = 10;
        Height = 2;
        ActualThemeVariantChanged += (_, _) =>
        {
            if (VisualRoot is not null)
            {
                _palette.Refresh(this);
                InvalidateVisual();
            }
        };
    }

    public bool IsDashed { get => GetValue(IsDashedProperty); set => SetValue(IsDashedProperty, value); }

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var size = Bounds.Size;
        if (!IsDashed)
        {
            context.FillRectangle(_palette.Brush(_palette.Accent), new Rect(size));
            return;
        }
        var brush = _palette.Brush(_palette.TextMuted);
        for (double x = 0; x < size.Width; x += 5)
        {
            context.FillRectangle(brush, new Rect(x, 0, Math.Min(3, size.Width - x), size.Height));
        }
    }
}

/// <summary>Draws a panel's divider lines behind its children (<c>Panel.Render</c> is sealed).</summary>
internal sealed class MetricsDashboardLineLayer(Action<DrawingContext> draw) : Control
{
    public override void Render(DrawingContext context) => draw(context);

    protected override Size MeasureOverride(Size availableSize) => default;
}
