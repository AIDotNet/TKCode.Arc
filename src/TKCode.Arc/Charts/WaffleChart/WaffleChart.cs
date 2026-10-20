using System.Collections;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A unit chart (Arc "Waffle chart"): a rows × columns grid where every cell is one share of the whole (10 × 10 = one
/// cell per percent), filled column by column from the bottom-left corner. Cells are persistent: when the data changes
/// they stay put where they can and the rest fly, staggered, to their new block while dipping and changing color. The
/// legend rolls each share to its new value; hovering or focusing a category dims the others, clicking pins it.
/// Arrow keys walk the grid cell by cell, Page Up / Page Down jump between categories, Escape clears the reading.
/// </summary>
public class WaffleChart : Control, IChartsBAccessible
{
    /// <summary>Defines the <see cref="ItemsSource"/> property.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<WaffleChart, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Defines the <see cref="KeySelector"/> property.</summary>
    public static readonly StyledProperty<Func<object, string>?> KeySelectorProperty =
        AvaloniaProperty.Register<WaffleChart, Func<object, string>?>(nameof(KeySelector));

    /// <summary>Defines the <see cref="LabelSelector"/> property.</summary>
    public static readonly StyledProperty<Func<object, string>?> LabelSelectorProperty =
        AvaloniaProperty.Register<WaffleChart, Func<object, string>?>(nameof(LabelSelector));

    /// <summary>Defines the <see cref="ValueSelector"/> property.</summary>
    public static readonly StyledProperty<Func<object, double>?> ValueSelectorProperty =
        AvaloniaProperty.Register<WaffleChart, Func<object, double>?>(nameof(ValueSelector));

    /// <summary>Defines the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<WaffleChart, string?>(nameof(Label));

    /// <summary>Defines the <see cref="Unit"/> property.</summary>
    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<WaffleChart, string?>(nameof(Unit));

    /// <summary>Defines the <see cref="ValueFormatter"/> property.</summary>
    public static readonly StyledProperty<Func<double, WaffleCategory, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<WaffleChart, Func<double, WaffleCategory, string>?>(nameof(ValueFormatter));

    /// <summary>Defines the <see cref="Rows"/> property.</summary>
    public static readonly StyledProperty<int> RowsProperty =
        AvaloniaProperty.Register<WaffleChart, int>(nameof(Rows), 10, validate: v => v >= 1);

    /// <summary>Defines the <see cref="Columns"/> property.</summary>
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<WaffleChart, int>(nameof(Columns), 10, validate: v => v >= 1);

    /// <summary>Defines the <see cref="AccentKey"/> property.</summary>
    public static readonly StyledProperty<string?> AccentKeyProperty =
        AvaloniaProperty.Register<WaffleChart, string?>(nameof(AccentKey));

    /// <summary>Defines the <see cref="ActiveKey"/> property.</summary>
    public static readonly StyledProperty<string?> ActiveKeyProperty =
        AvaloniaProperty.Register<WaffleChart, string?>(nameof(ActiveKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="IsLegendVisible"/> property.</summary>
    public static readonly StyledProperty<bool> IsLegendVisibleProperty =
        AvaloniaProperty.Register<WaffleChart, bool>(nameof(IsLegendVisible), true);

    /// <summary>Defines the <see cref="Decimals"/> property.</summary>
    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<WaffleChart, int>(nameof(Decimals));

    /// <summary>Defines the <see cref="EmptyLabel"/> property.</summary>
    public static readonly StyledProperty<string> EmptyLabelProperty =
        AvaloniaProperty.Register<WaffleChart, string>(nameof(EmptyLabel), "No data");

    internal static readonly StyledProperty<double> CellGapProperty =
        AvaloniaProperty.Register<WaffleChart, double>(nameof(CellGap), 3);

    internal static readonly StyledProperty<double> GridMaxSizeProperty =
        AvaloniaProperty.Register<WaffleChart, double>(nameof(GridMaxSize), 300);

    internal static readonly StyledProperty<double> SideLegendBreakpointProperty =
        AvaloniaProperty.Register<WaffleChart, double>(nameof(SideLegendBreakpoint), 420);

    /// <summary>Value that turns the accent off: <c>AccentKey="{x:Static arc:WaffleChart.NoAccent}"</c> keeps every category neutral.</summary>
    public const string NoAccent = "";

    private static readonly double[] s_shades = [0.40, 0.20, 0.30, 0.14];

    private readonly ChartsBPalette _palette;
    private readonly ChartsBItemsWatcher _watcher;
    private readonly ChartsBMemberReader _reader = new();
    private readonly WaffleGrid _grid;
    private readonly ChartsBOverlay _overlay;
    private readonly List<WaffleLegendItem> _legend = new();
    private readonly AvaloniaList<WaffleCategory> _items = new();
    private List<WaffleCategory> _data = new();
    private int[] _counts = [];
    private double _total;
    private string? _preview;
    private IDisposable? _previewClear;
    private BindingBase? _keyBinding;
    private BindingBase? _labelBinding;
    private BindingBase? _valueBinding;
    private bool _sideLegend;

    /// <summary>Creates a waffle chart.</summary>
    public WaffleChart()
    {
        _palette = new ChartsBPalette(this, OnPaletteChanged);
        _grid = new WaffleGrid(this);
        _overlay = new ChartsBOverlay(context => _grid.DrawTooltip(context, _grid.Bounds.Position));
        VisualChildren.Add(_grid);
        LogicalChildren.Add(_grid);
        VisualChildren.Add(_overlay);
        _watcher = new ChartsBItemsWatcher(Rebuild);
        _items.CollectionChanged += (_, _) =>
        {
            // While attached the watcher reports inline changes itself.
            if (ItemsSource is null && !_watcher.IsSubscribed)
            {
                Rebuild();
            }
        };
        _watcher.SetSource(_items);
    }

    static WaffleChart()
    {
        AffectsMeasure<WaffleChart>(IsLegendVisibleProperty, RowsProperty, ColumnsProperty, CellGapProperty, GridMaxSizeProperty,
            SideLegendBreakpointProperty);
    }

    /// <summary>Data items: <see cref="WaffleCategory"/> instances, or any objects read through the selectors or member bindings.</summary>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Inline categories, used when <see cref="ItemsSource"/> is not set.</summary>
    [Content]
    public AvaloniaList<WaffleCategory> Items => _items;

    /// <summary>Reads a category key from a data item (wins over <see cref="KeyMemberBinding"/>).</summary>
    public Func<object, string>? KeySelector
    {
        get => GetValue(KeySelectorProperty);
        set => SetValue(KeySelectorProperty, value);
    }

    /// <summary>Reads a category label from a data item.</summary>
    public Func<object, string>? LabelSelector
    {
        get => GetValue(LabelSelectorProperty);
        set => SetValue(LabelSelectorProperty, value);
    }

    /// <summary>Reads a category value from a data item.</summary>
    public Func<object, double>? ValueSelector
    {
        get => GetValue(ValueSelectorProperty);
        set => SetValue(ValueSelectorProperty, value);
    }

    /// <summary>Binding (a member path such as <c>{Binding Region}</c>) that reads the key of each item.</summary>
    [AssignBinding]
    public BindingBase? KeyMemberBinding
    {
        get => _keyBinding;
        set
        {
            _keyBinding = value;
            Rebuild();
        }
    }

    /// <summary>Binding that reads the label of each item (defaults to the key).</summary>
    [AssignBinding]
    public BindingBase? LabelMemberBinding
    {
        get => _labelBinding;
        set
        {
            _labelBinding = value;
            Rebuild();
        }
    }

    /// <summary>Binding that reads the value of each item.</summary>
    [AssignBinding]
    public BindingBase? ValueMemberBinding
    {
        get => _valueBinding;
        set
        {
            _valueBinding = value;
            Rebuild();
        }
    }

    /// <summary>What the whole is, such as "Electricity generation, 2023"; names the chart for assistive technology.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Unit after each raw value in the tooltip, such as "TWh".</summary>
    public string? Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <summary>Formats raw values (default: grouped, at most one decimal).</summary>
    public Func<double, WaffleCategory, string>? ValueFormatter
    {
        get => GetValue(ValueFormatterProperty);
        set => SetValue(ValueFormatterProperty, value);
    }

    /// <summary>Grid rows (default 10); rows × columns cells make the whole.</summary>
    public int Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <summary>Grid columns (default 10).</summary>
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>The category painted in Series1. Null (default) means the first category; <see cref="NoAccent"/> keeps all neutral.</summary>
    public string? AccentKey
    {
        get => GetValue(AccentKeyProperty);
        set => SetValue(AccentKeyProperty, value);
    }

    /// <summary>The pinned category (two-way). Others dim while one is pinned, hovered or focused.</summary>
    public string? ActiveKey
    {
        get => GetValue(ActiveKeyProperty);
        set => SetValue(ActiveKeyProperty, value);
    }

    /// <summary>Shows the category list with rolling shares (default true).</summary>
    public bool IsLegendVisible
    {
        get => GetValue(IsLegendVisibleProperty);
        set => SetValue(IsLegendVisibleProperty, value);
    }

    /// <summary>Decimal places for shares (default 0).</summary>
    public int Decimals
    {
        get => GetValue(DecimalsProperty);
        set => SetValue(DecimalsProperty, value);
    }

    /// <summary>Message shown when there is nothing to draw.</summary>
    public string EmptyLabel
    {
        get => GetValue(EmptyLabelProperty);
        set => SetValue(EmptyLabelProperty, value);
    }

    /// <summary>Raised when the pinned category changes (legend click, Enter or Space).</summary>
    public event EventHandler<WaffleActiveKeyChangedEventArgs>? ActiveKeyChanged;

    internal double CellGap
    {
        get => GetValue(CellGapProperty);
        set => SetValue(CellGapProperty, value);
    }

    internal double GridMaxSize
    {
        get => GetValue(GridMaxSizeProperty);
        set => SetValue(GridMaxSizeProperty, value);
    }

    internal double SideLegendBreakpoint
    {
        get => GetValue(SideLegendBreakpointProperty);
        set => SetValue(SideLegendBreakpointProperty, value);
    }

    /// <summary>The resolved categories in fill order.</summary>
    public IReadOnlyList<WaffleCategory> Categories => _data;

    /// <summary>Whole cells per category (largest remainder, always adding up to the grid).</summary>
    public IReadOnlyList<int> CellCounts => _counts;

    internal ChartsBPalette Palette => _palette;
    internal IReadOnlyList<WaffleLegendItem> LegendItems => _legend;
    internal WaffleGrid Grid => _grid;
    internal ChartsBOverlay Overlay => _overlay;
    internal double Total => _total;
    internal bool IsEmpty => _data.Count == 0 || _total <= 0;
    internal int CellCount => Math.Max(1, Rows * Columns);

    /// <summary>Key in focus: the reading under the pointer or keyboard cursor, a legend preview, else the pinned key.</summary>
    internal string? FocusKey
    {
        get
        {
            var key = _grid.PointerKey ?? _preview ?? ActiveKey;
            return key is not null && _data.Any(d => d.Key == key) ? key : null;
        }
    }

    internal string? EffectiveAccent => AccentKey is null ? _data.FirstOrDefault()?.Key : AccentKey.Length == 0 ? null : AccentKey;

    /// <summary>Share of the whole in percent.</summary>
    internal double ShareOf(WaffleCategory item) => _total > 0 ? Math.Max(0, item.Value) / _total * 100 : 0;

    internal string ShareText(double share) => share.ToString("F" + Math.Max(0, Decimals), ChartsBMath.EnUs) + "%";

    internal string FormatValue(WaffleCategory item)
    {
        var text = ValueFormatter?.Invoke(item.Value, item) ?? ChartsBMath.Grouped(item.Value);
        return string.IsNullOrEmpty(Unit) ? text : $"{text} {Unit}";
    }

    internal Color ColorOf(string? key)
    {
        if (key is null)
        {
            return Colors.Transparent;
        }
        var item = _data.FirstOrDefault(d => d.Key == key);
        if (item is null)
        {
            return Colors.Transparent;
        }
        if (item.Color is { } color)
        {
            return color;
        }
        var accent = EffectiveAccent;
        if (key == accent)
        {
            return _palette.Series(0);
        }
        var rank = RankOf(key, accent);
        return rank < 3
            ? _palette.Series(rank + 1)
            : ChartsBMath.MixIn(_palette.Foreground, s_shades[(rank - 3) % s_shades.Length], _palette.Surface);
    }

    /// <summary>A color-free second channel: neutral categories cycle through square, round and framed cells.</summary>
    internal ChartsBSwatchShape ShapeOf(string? key)
    {
        var accent = EffectiveAccent;
        if (key is null || key == accent)
        {
            return ChartsBSwatchShape.Square;
        }
        var rank = RankOf(key, accent);
        return rank < 0 ? ChartsBSwatchShape.Square : (ChartsBSwatchShape)(rank % 3);
    }

    private int RankOf(string key, string? accent)
    {
        var rank = 0;
        foreach (var item in _data)
        {
            if (item.Key == accent)
            {
                continue;
            }
            if (item.Key == key)
            {
                return rank;
            }
            rank++;
        }
        return -1;
    }

    internal void SetPreview(string? key)
    {
        if (_preview == key)
        {
            return;
        }
        _preview = key;
        OnFocusChanged();
    }

    internal void ClearPreviewSoon()
    {
        _previewClear?.Dispose();
        _previewClear = Avalonia.Threading.DispatcherTimer.RunOnce(() => SetPreview(null), TimeSpan.FromMilliseconds(60));
    }

    internal void CancelPreviewClear()
    {
        _previewClear?.Dispose();
        _previewClear = null;
    }

    internal void TogglePin(string key)
    {
        var next = ActiveKey == key ? null : key;
        ActiveKey = next;
    }

    internal void OnFocusChanged()
    {
        var focus = FocusKey;
        var pinned = ActiveKey;
        foreach (var item in _legend)
        {
            item.SetState(focus == item.Key, focus is not null && focus != item.Key, pinned == item.Key);
        }
        _grid.OnFocusKeyChanged(focus);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            _watcher.SetSource(ItemsSource ?? _items);
        }
        else if (change.Property == KeySelectorProperty || change.Property == LabelSelectorProperty ||
                 change.Property == ValueSelectorProperty || change.Property == RowsProperty || change.Property == ColumnsProperty)
        {
            Rebuild();
        }
        else if (change.Property == AccentKeyProperty)
        {
            _grid.OnColorsChanged(animate: true);
            SyncLegend();
        }
        else if (change.Property == ActiveKeyProperty)
        {
            var key = change.GetNewValue<string?>();
            OnFocusChanged();
            ActiveKeyChanged?.Invoke(this, new WaffleActiveKeyChangedEventArgs(key, _data.FirstOrDefault(d => d.Key == key)));
        }
        else if (change.Property == DecimalsProperty || change.Property == UnitProperty || change.Property == ValueFormatterProperty)
        {
            SyncLegend();
            _grid.RefreshReading();
        }
        else if (change.Property == LabelProperty || change.Property == EmptyLabelProperty)
        {
            AutomationProperties.SetName(this, Label);
            _grid.RefreshReading();
            _grid.InvalidateVisual();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watcher.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watcher.Detach();
    }

    private void OnPaletteChanged()
    {
        _grid?.OnColorsChanged(animate: false);
        foreach (var item in _legend)
        {
            item.InvalidateText();
        }
        _overlay?.InvalidateVisual();
    }

    /// <summary>Reads the items, allocates whole cells and hands them to the grid, which moves as few as possible.</summary>
    private void Rebuild()
    {
        var data = new List<WaffleCategory>();
        foreach (var item in _watcher.Snapshot())
        {
            data.Add(Resolve(item));
        }
        _data = data;
        _total = data.Sum(d => Math.Max(0, d.Value));
        _counts = Allocate(data, _total, CellCount);
        SyncLegend();
        _grid?.OnDataChanged();
        UpdateAccessibility();
        InvalidateMeasure();
    }

    private WaffleCategory Resolve(object item)
    {
        if (item is WaffleCategory category && KeySelector is null && LabelSelector is null && ValueSelector is null &&
            _keyBinding is null && _labelBinding is null && _valueBinding is null)
        {
            return category;
        }
        var typed = item as WaffleCategory;
        var key = KeySelector?.Invoke(item) ?? ChartsBMemberReader.ToText(_reader.Read(item, _keyBinding)) ?? typed?.Key ?? item.ToString() ?? string.Empty;
        var label = LabelSelector?.Invoke(item) ?? ChartsBMemberReader.ToText(_reader.Read(item, _labelBinding)) ?? typed?.Label ?? key;
        var value = ValueSelector?.Invoke(item) ?? (_valueBinding is null ? typed?.Value ?? 0 : ChartsBMemberReader.ToDouble(_reader.Read(item, _valueBinding)));
        return new WaffleCategory(key, label, double.IsFinite(value) ? value : 0, typed?.Color) { Source = item };
    }

    /// <summary>Whole cells per category by largest remainder, so the counts always add up to the grid.</summary>
    internal static int[] Allocate(IReadOnlyList<WaffleCategory> data, double total, int cells)
    {
        var counts = new int[data.Count];
        if (total <= 0)
        {
            return counts;
        }
        var exact = new double[data.Count];
        var left = cells;
        for (var i = 0; i < data.Count; i++)
        {
            exact[i] = Math.Max(0, data[i].Value) / total * cells;
            counts[i] = (int)Math.Floor(exact[i]);
            left -= counts[i];
        }
        var order = Enumerable.Range(0, data.Count).OrderByDescending(i => exact[i] - Math.Floor(exact[i])).ToArray();
        for (var k = 0; left > 0 && k < order.Length; k++, left--)
        {
            counts[order[k]]++;
        }
        return counts;
    }

    private void SyncLegend()
    {
        var show = IsLegendVisible && !IsEmpty;
        var wanted = show ? _data : [];
        while (_legend.Count > wanted.Count)
        {
            var last = _legend[^1];
            _legend.RemoveAt(_legend.Count - 1);
            VisualChildren.Remove(last);
            LogicalChildren.Remove(last);
        }
        for (var i = 0; i < wanted.Count; i++)
        {
            if (i >= _legend.Count)
            {
                var item = new WaffleLegendItem(this);
                _legend.Add(item);
                // Before the overlay, so the tooltip stays on top.
                VisualChildren.Insert(VisualChildren.Count - 1, item);
                LogicalChildren.Add(item);
            }
            _legend[i].Bind(wanted[i], ShareOf(wanted[i]));
        }
        OnFocusChanged();
    }

    private void UpdateAccessibility()
    {
        AutomationProperties.SetName(this, Label);
        _grid?.RefreshReading();
    }

    string IChartsBAccessible.ChartRoleDescription => "waffle chart";

    string IChartsBAccessible.ChartName =>
        $"{Label}. {CellCount} cells. Use arrow keys to move between cells, Page Up and Page Down to jump between categories.";

    string IChartsBAccessible.ChartHelp => _grid.Reading ?? Summary;

    /// <summary>Accessible summary, such as "Energy mix. Coal 35%, Gas 22%."</summary>
    public string Summary => IsEmpty
        ? $"{Label}. {EmptyLabel}."
        : $"{Label}. {string.Join(", ", _data.Select(d => $"{d.Label} {ShareText(ShareOf(d))}"))}.";

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 560 : availableSize.Width;
        var legend = _legend.Count > 0;
        _sideLegend = legend && width >= SideLegendBreakpoint;
        double gridWidth;
        double legendWidth;
        if (_sideLegend)
        {
            gridWidth = Math.Min(232, Math.Min(GridMaxSize, width));
            legendWidth = Math.Max(0, width - 232 - 24);
        }
        else
        {
            gridWidth = Math.Min(GridMaxSize, width);
            legendWidth = Math.Min(320, width + 20);
        }
        _grid.Measure(new Size(gridWidth, double.PositiveInfinity));
        var legendHeight = 0.0;
        foreach (var item in _legend)
        {
            item.Measure(new Size(legendWidth, double.PositiveInfinity));
            legendHeight += item.DesiredSize.Height;
        }
        legendHeight += Math.Max(0, _legend.Count - 1) * 2;
        _overlay.Measure(Size.Infinity);
        var height = _sideLegend
            ? Math.Max(_grid.DesiredSize.Height, legendHeight)
            : _grid.DesiredSize.Height + (legend ? 24 + legendHeight : 0);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        if (_sideLegend)
        {
            var gridWidth = _grid.DesiredSize.Width;
            var bodyHeight = finalSize.Height;
            _grid.Arrange(new Rect((232 - gridWidth) / 2, (bodyHeight - _grid.DesiredSize.Height) / 2, gridWidth, _grid.DesiredSize.Height));
            var legendWidth = Math.Max(0, width - 232 - 24);
            var legendHeight = _legend.Sum(i => i.DesiredSize.Height) + Math.Max(0, _legend.Count - 1) * 2;
            var y = (bodyHeight - legendHeight) / 2;
            foreach (var item in _legend)
            {
                item.Arrange(new Rect(256, y, legendWidth, item.DesiredSize.Height));
                y += item.DesiredSize.Height + 2;
            }
        }
        else
        {
            var gridWidth = _grid.DesiredSize.Width;
            _grid.Arrange(new Rect((width - gridWidth) / 2, 0, gridWidth, _grid.DesiredSize.Height));
            var legendWidth = Math.Min(320, width + 20);
            var y = _grid.DesiredSize.Height + 24;
            foreach (var item in _legend)
            {
                item.Arrange(new Rect((width - legendWidth) / 2, y, legendWidth, item.DesiredSize.Height));
                y += item.DesiredSize.Height + 2;
            }
        }
        _overlay.Arrange(new Rect(finalSize));
        return finalSize;
    }
}
