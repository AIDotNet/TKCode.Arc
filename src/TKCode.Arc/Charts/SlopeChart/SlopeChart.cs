using System.Collections;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A before / after comparison on two shared axes (Arc "Slope chart"). Lines draw in from the first column to the
/// second on first view; labels push apart instead of overlapping and connect back to their dots with thin leaders;
/// rank movement sits beside each end value. A new dataset slides every line, dot and label to its new place on the
/// shared morph spring. Hover or the arrow keys (in end-value order) bring one line forward and show a tooltip.
/// </summary>
public class SlopeChart : Control, IChartsBAccessible, Avalonia.Rendering.ICustomHitTest
{
    /// <summary>The whole box takes the pointer, not only the painted marks.</summary>
    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    /// <summary>Defines the <see cref="ItemsSource"/> property.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<SlopeChart, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Defines the <see cref="KeySelector"/> property.</summary>
    public static readonly StyledProperty<Func<object, string>?> KeySelectorProperty =
        AvaloniaProperty.Register<SlopeChart, Func<object, string>?>(nameof(KeySelector));

    /// <summary>Defines the <see cref="LabelSelector"/> property.</summary>
    public static readonly StyledProperty<Func<object, string>?> LabelSelectorProperty =
        AvaloniaProperty.Register<SlopeChart, Func<object, string>?>(nameof(LabelSelector));

    /// <summary>Defines the <see cref="StartSelector"/> property.</summary>
    public static readonly StyledProperty<Func<object, double>?> StartSelectorProperty =
        AvaloniaProperty.Register<SlopeChart, Func<object, double>?>(nameof(StartSelector));

    /// <summary>Defines the <see cref="EndSelector"/> property.</summary>
    public static readonly StyledProperty<Func<object, double>?> EndSelectorProperty =
        AvaloniaProperty.Register<SlopeChart, Func<object, double>?>(nameof(EndSelector));

    /// <summary>Defines the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SlopeChart, string?>(nameof(Label));

    /// <summary>Defines the <see cref="StartLabel"/> property.</summary>
    public static readonly StyledProperty<string> StartLabelProperty =
        AvaloniaProperty.Register<SlopeChart, string>(nameof(StartLabel), "Before");

    /// <summary>Defines the <see cref="EndLabel"/> property.</summary>
    public static readonly StyledProperty<string> EndLabelProperty =
        AvaloniaProperty.Register<SlopeChart, string>(nameof(EndLabel), "After");

    /// <summary>Defines the <see cref="ValueFormatter"/> property.</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<SlopeChart, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>Defines the <see cref="ChangeFormatter"/> property.</summary>
    public static readonly StyledProperty<Func<double, SlopeItem, string>?> ChangeFormatterProperty =
        AvaloniaProperty.Register<SlopeChart, Func<double, SlopeItem, string>?>(nameof(ChangeFormatter));

    /// <summary>Defines the <see cref="PlotHeight"/> property.</summary>
    public static readonly StyledProperty<double> PlotHeightProperty =
        AvaloniaProperty.Register<SlopeChart, double>(nameof(PlotHeight), double.NaN);

    /// <summary>Defines the <see cref="HighlightKey"/> property.</summary>
    public static readonly StyledProperty<string?> HighlightKeyProperty =
        AvaloniaProperty.Register<SlopeChart, string?>(nameof(HighlightKey));

    /// <summary>Defines the <see cref="ActiveKey"/> property.</summary>
    public static readonly StyledProperty<string?> ActiveKeyProperty =
        AvaloniaProperty.Register<SlopeChart, string?>(nameof(ActiveKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="IsRankVisible"/> property.</summary>
    public static readonly StyledProperty<bool> IsRankVisibleProperty =
        AvaloniaProperty.Register<SlopeChart, bool>(nameof(IsRankVisible), true);

    /// <summary>Defines the <see cref="EmptyLabel"/> property.</summary>
    public static readonly StyledProperty<string> EmptyLabelProperty =
        AvaloniaProperty.Register<SlopeChart, string>(nameof(EmptyLabel), "No data");

    internal static readonly StyledProperty<double> RowPitchProperty =
        AvaloniaProperty.Register<SlopeChart, double>(nameof(RowPitch), 44);

    internal static readonly StyledProperty<double> LabelGapProperty =
        AvaloniaProperty.Register<SlopeChart, double>(nameof(LabelGap), 20);

    internal static readonly StyledProperty<double> NarrowBreakpointProperty =
        AvaloniaProperty.Register<SlopeChart, double>(nameof(NarrowBreakpoint), 440);

    private const double Head = 28;
    private const double Pad = 14;
    private const double Hit = 22;

    private readonly ChartsBPalette _palette;
    private readonly ChartsBItemsWatcher _watcher;
    private readonly ChartsBMemberReader _reader = new();
    private readonly ChartsBReveal _reveal;
    private readonly ChartsBTooltip _tooltip;
    private readonly AvaloniaList<SlopeItem> _items = new();
    private readonly Dictionary<string, Row> _rows = new();
    private List<SlopeItem> _data = new();
    private List<Row> _byEnd = new();
    private List<Row> _drawOrder = new();
    private BindingBase? _keyBinding;
    private BindingBase? _labelBinding;
    private BindingBase? _startBinding;
    private BindingBase? _endBinding;
    private ChartsBAutomationPeer? _peer;
    private FormattedText? _startHead;
    private FormattedText? _endHead;
    private FormattedText? _empty;
    private int _textVersion = -1;
    private double _layoutWidth = -1;
    private string? _tipKey;

    /// <summary>Per item drawing state: target geometry, morphing geometry, reveal and emphasis.</summary>
    private sealed class Row
    {
        public required SlopeItem Item;
        public required MotionValue<SlopeGeometry> Geometry;
        public required MotionValue<double> Draw;
        public required MotionValue<double> StartDot;
        public required MotionValue<double> EndDot;
        public required MotionValue<double> StartLabel;
        public required MotionValue<double> EndLabel;
        public required MotionValue<double> Emphasis;
        public required MotionValue<double> Dim;
        public required MotionValue<double> RankRoll;
        public SlopeGeometry Target;
        public int Index;
        public int Rank0;
        public int Rank1;
        public int Order;
        public string RankText = "=";
        public string? RankPrevious;
        public int RankDirection = 1;
        public FormattedText? Name;
        public FormattedText? StartValue;
        public FormattedText? EndValue;
        public FormattedText? Rank;
        public FormattedText? RankOld;
        public int Version = -1;
        public bool Revealed;
    }

    /// <summary>Creates a slope chart.</summary>
    public SlopeChart()
    {
        Focusable = true;
        _palette = new ChartsBPalette(this, () =>
        {
            _textVersion = -1;
            InvalidateVisual();
        });
        _reveal = new ChartsBReveal(this, 0.3, OnReveal);
        _tooltip = new ChartsBTooltip(this, SpringSpec.FromPhysical(480, 42), InvalidateVisual) { MinWidth = 168, RowGap = 3 };
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
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    static SlopeChart()
    {
        AffectsMeasure<SlopeChart>(PlotHeightProperty, RowPitchProperty);
        AffectsRender<SlopeChart>(StartLabelProperty, EndLabelProperty, HighlightKeyProperty, IsRankVisibleProperty);
    }

    /// <summary>Data items: <see cref="SlopeItem"/> instances, or any objects read through the selectors or member bindings.</summary>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Inline items, used when <see cref="ItemsSource"/> is not set.</summary>
    [Content]
    public AvaloniaList<SlopeItem> Items => _items;

    /// <summary>Reads an item key (wins over <see cref="KeyMemberBinding"/>).</summary>
    public Func<object, string>? KeySelector
    {
        get => GetValue(KeySelectorProperty);
        set => SetValue(KeySelectorProperty, value);
    }

    /// <summary>Reads an item label.</summary>
    public Func<object, string>? LabelSelector
    {
        get => GetValue(LabelSelectorProperty);
        set => SetValue(LabelSelectorProperty, value);
    }

    /// <summary>Reads the first-period value.</summary>
    public Func<object, double>? StartSelector
    {
        get => GetValue(StartSelectorProperty);
        set => SetValue(StartSelectorProperty, value);
    }

    /// <summary>Reads the second-period value.</summary>
    public Func<object, double>? EndSelector
    {
        get => GetValue(EndSelectorProperty);
        set => SetValue(EndSelectorProperty, value);
    }

    /// <summary>Binding (member path) that reads each item's key.</summary>
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

    /// <summary>Binding that reads each item's label.</summary>
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

    /// <summary>Binding that reads each item's first-period value.</summary>
    [AssignBinding]
    public BindingBase? StartMemberBinding
    {
        get => _startBinding;
        set
        {
            _startBinding = value;
            Rebuild();
        }
    }

    /// <summary>Binding that reads each item's second-period value.</summary>
    [AssignBinding]
    public BindingBase? EndMemberBinding
    {
        get => _endBinding;
        set
        {
            _endBinding = value;
            Rebuild();
        }
    }

    /// <summary>What is measured, such as "Conversion rate by channel"; names the chart for assistive technology.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Heading of the first column, such as "Q1".</summary>
    public string StartLabel
    {
        get => GetValue(StartLabelProperty);
        set => SetValue(StartLabelProperty, value);
    }

    /// <summary>Heading of the second column, such as "Q2".</summary>
    public string EndLabel
    {
        get => GetValue(EndLabelProperty);
        set => SetValue(EndLabelProperty, value);
    }

    /// <summary>Formats values (default: grouped, at most one decimal).</summary>
    public Func<double, string>? ValueFormatter
    {
        get => GetValue(ValueFormatterProperty);
        set => SetValue(ValueFormatterProperty, value);
    }

    /// <summary>Formats the change in the tooltip, such as "+1.5 pts" (default: signed difference in value format).</summary>
    public Func<double, SlopeItem, string>? ChangeFormatter
    {
        get => GetValue(ChangeFormatterProperty);
        set => SetValue(ChangeFormatterProperty, value);
    }

    /// <summary>Plot height in pixels; NaN (default) means 44 pixels per item, at least 200.</summary>
    public double PlotHeight
    {
        get => GetValue(PlotHeightProperty);
        set => SetValue(PlotHeightProperty, value);
    }

    /// <summary>The item drawn in Series1, such as the one the story is about.</summary>
    public string? HighlightKey
    {
        get => GetValue(HighlightKeyProperty);
        set => SetValue(HighlightKeyProperty, value);
    }

    /// <summary>The item in focus (two-way): it comes forward while the others fade.</summary>
    public string? ActiveKey
    {
        get => GetValue(ActiveKeyProperty);
        set => SetValue(ActiveKeyProperty, value);
    }

    /// <summary>Shows rank movement beside each end value (default true).</summary>
    public bool IsRankVisible
    {
        get => GetValue(IsRankVisibleProperty);
        set => SetValue(IsRankVisibleProperty, value);
    }

    /// <summary>Message shown when there are no items.</summary>
    public string EmptyLabel
    {
        get => GetValue(EmptyLabelProperty);
        set => SetValue(EmptyLabelProperty, value);
    }

    /// <summary>Raised when the item in focus changes (pointer, keyboard or <see cref="ActiveKey"/>).</summary>
    public event EventHandler<SlopeActiveKeyChangedEventArgs>? ActiveKeyChanged;

    internal double RowPitch
    {
        get => GetValue(RowPitchProperty);
        set => SetValue(RowPitchProperty, value);
    }

    internal double LabelGap
    {
        get => GetValue(LabelGapProperty);
        set => SetValue(LabelGapProperty, value);
    }

    internal double NarrowBreakpoint
    {
        get => GetValue(NarrowBreakpointProperty);
        set => SetValue(NarrowBreakpointProperty, value);
    }

    /// <summary>The resolved items in data order.</summary>
    public IReadOnlyList<SlopeItem> Data => _data;

    /// <summary>Accessible summary in end-value order.</summary>
    public string Summary => _data.Count == 0
        ? $"{Label}. {EmptyLabel}."
        : $"{Label}, {StartLabel} to {EndLabel}. {string.Join(", ", _byEnd.Select(r => $"{r.Item.Label} {Format(r.Item.Start)} to {Format(r.Item.End)}"))}.";

    internal string? Reading
    {
        get
        {
            var row = ActiveRow;
            return row is null
                ? null
                : $"{row.Item.Label}, {StartLabel} {Format(row.Item.Start)}, {EndLabel} {Format(row.Item.End)}, {Change(row.Item)}, {RankText(row)}";
        }
    }

    /// <summary>Label positions after collision avoidance (start column, end column), in plot coordinates.</summary>
    internal (double Start, double End) LabelPositionOf(string key) => _rows.TryGetValue(key, out var row) ? (row.Target.L0, row.Target.L1) : (0, 0);

    internal double PlotHeightResolved => double.IsNaN(PlotHeight) ? Math.Max(200, _data.Count * RowPitch) : PlotHeight;

    private Row? ActiveRow => ActiveKey is { } key && _rows.TryGetValue(key, out var row) ? row : null;

    private bool Narrow => Bounds.Width < NarrowBreakpoint;

    private (double X0, double X1) Columns(double width)
    {
        var narrow = width < NarrowBreakpoint;
        var leftRoom = narrow ? width < 300 ? 116 : 132 : 168;
        var rightRoom = IsRankVisible ? narrow ? 76 : 96 : narrow ? 52 : 64;
        var x0 = (double)leftRoom;
        return (x0, Math.Max(x0 + 40, width - rightRoom));
    }

    private string Format(double value) => ValueFormatter?.Invoke(value) ?? ChartsBMath.Grouped(value);

    private string Change(SlopeItem item) => ChangeFormatter?.Invoke(item.End - item.Start, item)
                                             ?? $"{(item.End >= item.Start ? "+" : "−")}{Format(Math.Abs(item.End - item.Start))}";

    private static string RankText(Row row) => row.Rank0 == row.Rank1
        ? "same rank"
        : row.Rank1 < row.Rank0 ? $"up {row.Rank0 - row.Rank1} to rank {row.Rank1}" : $"down {row.Rank1 - row.Rank0} to rank {row.Rank1}";

    // ---- data -------------------------------------------------------------------------------------------------

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            _watcher.SetSource(ItemsSource ?? _items);
        }
        else if (change.Property == KeySelectorProperty || change.Property == LabelSelectorProperty ||
                 change.Property == StartSelectorProperty || change.Property == EndSelectorProperty)
        {
            Rebuild();
        }
        else if (change.Property == ActiveKeyProperty)
        {
            OnActiveChanged();
            ActiveKeyChanged?.Invoke(this, new SlopeActiveKeyChangedEventArgs(change.GetNewValue<string?>(), ActiveRow?.Item));
        }
        else if (change.Property == HighlightKeyProperty || change.Property == ValueFormatterProperty ||
                 change.Property == ChangeFormatterProperty || change.Property == IsRankVisibleProperty ||
                 change.Property == StartLabelProperty || change.Property == EndLabelProperty)
        {
            _textVersion = -1;
            _layoutWidth = -1;
            Relayout(animate: false);
            SyncTooltip(force: true);
            UpdateAccessibility();
        }
        else if (change.Property == LabelProperty)
        {
            UpdateAccessibility();
        }
        else if (change.Property == BoundsProperty)
        {
            if (Math.Abs(Bounds.Width - _layoutWidth) > 0.5)
            {
                _textVersion = -1;
                Relayout(animate: false);
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watcher.Attach();
        _reveal.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watcher.Detach();
        _reveal.Detach();
    }

    private void Rebuild()
    {
        var data = new List<SlopeItem>();
        var seen = new HashSet<string>();
        foreach (var item in _watcher.Snapshot())
        {
            var resolved = Resolve(item);
            if (seen.Add(resolved.Key))
            {
                data.Add(resolved);
            }
        }
        _data = data;
        foreach (var key in _rows.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            _rows.Remove(key);
        }
        for (var i = 0; i < data.Count; i++)
        {
            if (!_rows.TryGetValue(data[i].Key, out var row))
            {
                row = CreateRow(data[i]);
                _rows[data[i].Key] = row;
            }
            row.Item = data[i];
            row.Index = i;
            row.Version = -1;
        }
        Focusable = data.Count > 0;
        Relayout(animate: true);
        if (ActiveKey is { } active && !_rows.ContainsKey(active))
        {
            SetActive(null);
        }
        SyncTooltip(force: true);
        UpdateAccessibility();
        InvalidateMeasure();
    }

    private SlopeItem Resolve(object item)
    {
        if (item is SlopeItem typed && KeySelector is null && LabelSelector is null && StartSelector is null && EndSelector is null &&
            _keyBinding is null && _labelBinding is null && _startBinding is null && _endBinding is null)
        {
            return typed;
        }
        var source = item as SlopeItem;
        var key = KeySelector?.Invoke(item) ?? ChartsBMemberReader.ToText(_reader.Read(item, _keyBinding)) ?? source?.Key ?? item.ToString() ?? string.Empty;
        var label = LabelSelector?.Invoke(item) ?? ChartsBMemberReader.ToText(_reader.Read(item, _labelBinding)) ?? source?.Label ?? key;
        var start = StartSelector?.Invoke(item) ?? (_startBinding is null ? source?.Start ?? 0 : ChartsBMemberReader.ToDouble(_reader.Read(item, _startBinding)));
        var end = EndSelector?.Invoke(item) ?? (_endBinding is null ? source?.End ?? 0 : ChartsBMemberReader.ToDouble(_reader.Read(item, _endBinding)));
        return new SlopeItem(key, label, double.IsFinite(start) ? start : 0, double.IsFinite(end) ? end : 0) { Source = item };
    }

    private Row CreateRow(SlopeItem item)
    {
        var row = new Row
        {
            Item = item,
            Geometry = new MotionValue<SlopeGeometry>(this, default, SlopeGeometry.Vector),
            Draw = Value(0),
            StartDot = Value(0),
            EndDot = Value(0),
            StartLabel = Value(0),
            EndLabel = Value(0),
            Emphasis = Value(0),
            Dim = Value(0),
            RankRoll = Value(1)
        };
        return row;
    }

    private MotionValue<double> Value(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    /// <summary>Recomputes targets: shared value axis, ranks, spread labels; moves geometry on the morph spring.</summary>
    private void Relayout(bool animate)
    {
        var width = Bounds.Width;
        _layoutWidth = width;
        var plot = PlotHeightResolved;
        var lo = double.PositiveInfinity;
        var hi = double.NegativeInfinity;
        foreach (var item in _data)
        {
            lo = Math.Min(lo, Math.Min(item.Start, item.End));
            hi = Math.Max(hi, Math.Max(item.Start, item.End));
        }
        if (!double.IsFinite(lo))
        {
            lo = 0;
            hi = 1;
        }
        if (hi - lo < 1e-9)
        {
            lo -= 1;
            hi += 1;
        }
        double YOf(double value) => Pad + (1 - (value - lo) / (hi - lo)) * (plot - Pad * 2);
        var byStart = _data.OrderByDescending(d => d.Start).ToList();
        var byEndItems = _data.OrderByDescending(d => d.End).ToList();
        var l0 = Spread(_data.Select(d => YOf(d.Start)).ToArray(), LabelGap, 8, plot - 8);
        var l1 = Spread(_data.Select(d => YOf(d.End)).ToArray(), LabelGap, 8, plot - 8);
        var full = animate && _reveal.IsRevealed && ArcMotion.IsFull(this) && IsLoaded;
        for (var i = 0; i < _data.Count; i++)
        {
            var row = _rows[_data[i].Key];
            var target = new SlopeGeometry(YOf(_data[i].Start), YOf(_data[i].End), l0[i], l1[i]);
            var rank0 = byStart.IndexOf(_data[i]) + 1;
            var rank1 = byEndItems.IndexOf(_data[i]) + 1;
            var move = rank0 - rank1;
            var rankText = move == 0 ? "=" : Math.Abs(move).ToString(ChartsBMath.EnUs);
            if (row.Revealed && rankText != row.RankText && full)
            {
                row.RankPrevious = row.RankText;
                row.RankDirection = move >= 0 ? 1 : -1;
                row.RankRoll.Jump(0);
                _ = row.RankRoll.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
            }
            if (rankText != row.RankText)
            {
                row.Version = -1;
            }
            row.RankText = rankText;
            row.Rank0 = rank0;
            row.Rank1 = rank1;
            var first = row.Target == default && row.Geometry.Value == default;
            row.Target = target;
            if (full && !first)
            {
                _ = row.Geometry.AnimateTo(target, MotionSpec.Spring(ArcSpring.Morph));
            }
            else
            {
                row.Geometry.Jump(target);
            }
            if (_reveal.IsRevealed && !row.Revealed)
            {
                RevealRow(row, ArcMotion.IsFull(this) && IsLoaded);
            }
        }
        _byEnd = _data.Select(d => _rows[d.Key]).OrderBy(r => r.Rank1).ToList();
        for (var k = 0; k < _byEnd.Count; k++)
        {
            _byEnd[k].Order = k;
        }
        SortDrawOrder();
        InvalidateVisual();
    }

    /// <summary>Moves labels apart just enough to stop overlaps: touching labels form a group centred on where its members want to be.</summary>
    internal static double[] Spread(double[] wanted, double gap, double low, double high)
    {
        var order = wanted.Select((y, index) => (Y: y, Index: index)).OrderBy(e => e.Y).ToList();
        var groups = order.Select(e => (Members: new List<(double Y, int Index)> { e }, Top: e.Y)).ToList();
        for (var changed = true; changed;)
        {
            changed = false;
            var next = new List<(List<(double Y, int Index)> Members, double Top)>();
            foreach (var group in groups)
            {
                if (next.Count > 0 && next[^1].Top + next[^1].Members.Count * gap > group.Top)
                {
                    var members = next[^1].Members.Concat(group.Members).ToList();
                    var top = members.Select((m, k) => m.Y - k * gap).Average();
                    next[^1] = (members, top);
                    changed = true;
                }
                else
                {
                    next.Add(group);
                }
            }
            groups = next.Select(g => (g.Members, ChartsBMath.Clamp(g.Top, low, Math.Max(low, high - (g.Members.Count - 1) * gap)))).ToList();
        }
        var output = new double[wanted.Length];
        foreach (var group in groups)
        {
            for (var k = 0; k < group.Members.Count; k++)
            {
                output[group.Members[k].Index] = group.Top + k * gap;
            }
        }
        return output;
    }

    private void OnReveal(bool animate)
    {
        foreach (var row in _rows.Values)
        {
            RevealRow(row, animate);
        }
        InvalidateVisual();
    }

    /// <summary>Each line draws from its start to its end with a small stagger; dots and labels land as it does.</summary>
    private static void RevealRow(Row row, bool animate)
    {
        row.Revealed = true;
        if (!animate)
        {
            row.Draw.Jump(1);
            row.StartDot.Jump(1);
            row.EndDot.Jump(1);
            row.StartLabel.Jump(1);
            row.EndLabel.Jump(1);
            return;
        }
        var delay = row.Index * 0.05;
        _ = row.Draw.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromSeconds(0.7), ArcEasings.InOut, TimeSpan.FromSeconds(0.1 + delay)));
        _ = row.StartDot.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy, TimeSpan.FromSeconds(delay)));
        _ = row.EndDot.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy, TimeSpan.FromSeconds(0.7 + delay)));
        _ = row.StartLabel.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromSeconds(0.3), ArcEasings.Standard, TimeSpan.FromSeconds(delay)));
        _ = row.EndLabel.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromSeconds(0.3), ArcEasings.Standard, TimeSpan.FromSeconds(0.7 + delay)));
    }

    // ---- interaction ------------------------------------------------------------------------------------------

    private void SetActive(string? key)
    {
        if (ActiveKey != key)
        {
            ActiveKey = key;
        }
    }

    /// <summary>Data order, with the item in focus drawn last so it comes forward.</summary>
    private void SortDrawOrder()
        => _drawOrder = _byEnd.OrderBy(r => r.Item.Key == ActiveKey ? 1 : 0).ThenBy(r => r.Index).ToList();

    private void OnActiveChanged()
    {
        SortDrawOrder();
        var active = ActiveKey;
        var full = ArcMotion.IsFull(this) && IsLoaded;
        foreach (var row in _rows.Values)
        {
            var on = row.Item.Key == active;
            Tween(row.Emphasis, on ? 1 : 0, ArcMotionPresets.Fast, full);
            Tween(row.Dim, active is not null && !on ? 1 : 0, ArcMotionPresets.Standard, full);
        }
        SyncTooltip(force: false);
        UpdateAccessibility();
        InvalidateVisual();
    }

    private static void Tween(MotionValue<double> value, double target, TimeSpan duration, bool animate)
    {
        if (animate)
        {
            _ = value.AnimateTo(target, MotionSpec.Tween(duration, ArcEasings.Standard));
        }
        else
        {
            value.Jump(target);
        }
    }

    /// <summary>The line nearest the pointer, measured at the pointer's position along the plot (or by label beside the columns).</summary>
    private string? Nearest(Point point)
    {
        if (_data.Count == 0)
        {
            return null;
        }
        var (x0, x1) = Columns(Bounds.Width);
        var x = ChartsBMath.Clamp(point.X, x0, x1);
        var y = point.Y - Head;
        var t = (x - x0) / Math.Max(1, x1 - x0);
        Row? best = null;
        var distance = double.PositiveInfinity;
        foreach (var row in _rows.Values)
        {
            var g = row.Target;
            var inLine = Math.Abs(g.Y0 + (g.Y1 - g.Y0) * t - y);
            var onLabel = point.X < x0 ? Math.Abs(g.L0 - y) : point.X > x1 ? Math.Abs(g.L1 - y) : double.PositiveInfinity;
            var d = Math.Min(inLine, onLabel);
            if (d < distance)
            {
                distance = d;
                best = row;
            }
        }
        return distance <= Hit ? best?.Item.Key : null;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetCurrentPoint(this);
        if (e.Pointer.Type == PointerType.Mouse || point.Properties.IsLeftButtonPressed)
        {
            SetActive(Nearest(point.Position));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Pointer.Type != PointerType.Mouse)
        {
            e.Pointer.Capture(this);
            SetActive(Nearest(e.GetPosition(this)));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Type != PointerType.Mouse)
        {
            SetActive(null);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            SetActive(null);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (_byEnd.Count > 0 && ActiveKey is null && e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional)
        {
            SetActive(_byEnd[0].Item.Key);
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        SetActive(null);
    }

    /// <summary>↓ / → next item, ↑ / ← previous (in end-value order), Home / End first and last, Escape clears.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _byEnd.Count == 0)
        {
            return;
        }
        if (e.Key == Key.Escape && ActiveKey is not null)
        {
            e.Handled = true;
            SetActive(null);
            return;
        }
        var at = ActiveRow?.Order ?? -1;
        int? next = e.Key switch
        {
            Key.Down or Key.Right => at + 1,
            Key.Up or Key.Left => at < 0 ? 0 : at - 1,
            Key.Home => 0,
            Key.End => _byEnd.Count - 1,
            _ => null
        };
        if (next is null)
        {
            return;
        }
        e.Handled = true;
        SetActive(_byEnd[(int)ChartsBMath.Clamp(next.Value, 0, _byEnd.Count - 1)].Item.Key);
    }

    // ---- tooltip & accessibility --------------------------------------------------------------------------------

    private void SyncTooltip(bool force)
    {
        var row = ActiveRow;
        if (row is null || Bounds.Width <= 0)
        {
            _tooltip.Hide();
            InvalidateVisual();
            return;
        }
        if (force || _tipKey != row.Item.Key)
        {
            _tipKey = row.Item.Key;
            var p = _palette;
            var sm = p.TypeSM;
            _tooltip.SetRows(
            [
                new ChartsBTipRow { Left = ChartsBText.Make(row.Item.Label, p, sm, p.Foreground, FontWeight.Medium), MinHeight = sm * 1.5 },
                new ChartsBTipRow
                {
                    MarginTop = 2, MinHeight = sm * 1.3,
                    Left = ChartsBText.Make(StartLabel, p, sm, p.TextSecondary),
                    Right = ChartsBText.Make(Format(row.Item.Start), p, sm, p.Foreground, FontWeight.Medium)
                },
                new ChartsBTipRow
                {
                    MinHeight = sm * 1.3,
                    Left = ChartsBText.Make(EndLabel, p, sm, p.TextSecondary),
                    Right = ChartsBText.Make(Format(row.Item.End), p, sm, p.Foreground, FontWeight.Medium)
                },
                new ChartsBTipRow
                {
                    MarginTop = 2, MinHeight = p.TypeXS * 1.5,
                    Left = ChartsBText.Make($"{Change(row.Item)}, {RankText(row)}", p, p.TypeXS, p.TextMuted)
                }
            ]);
        }
        // The tooltip glides to the middle of the line in focus and stays inside the chart.
        var (x0, x1) = Columns(Bounds.Width);
        var size = _tooltip.Size;
        var mid = (row.Target.Y0 + row.Target.Y1) / 2;
        var cx = (x0 + x1) / 2;
        var above = mid - size.Height - 16 >= 0;
        var left = ChartsBMath.Clamp(cx - size.Width / 2, 0, Math.Max(0, Bounds.Width - size.Width));
        var top = ChartsBMath.Clamp(Head + (above ? mid - size.Height - 16 : mid + 16), 0, Math.Max(0, PlotHeightResolved + Head - size.Height));
        _tooltip.Show(new Point(left, top));
        InvalidateVisual();
    }

    private void UpdateAccessibility()
    {
        AutomationProperties.SetName(this, Label);
        _peer?.Announce();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new ChartsBAutomationPeer(this, this);

    string IChartsBAccessible.ChartRoleDescription => "slope chart";

    string IChartsBAccessible.ChartName => $"{Label}. Use up and down arrows to move between items in {EndLabel} order.";

    string IChartsBAccessible.ChartHelp => Reading ?? Summary;

    // ---- layout & drawing -------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 560 : availableSize.Width;
        return new Size(width, _data.Count == 0 ? 160 : Head + PlotHeightResolved);
    }

    private void EnsureText(double width)
    {
        if (_textVersion == _palette.Version && _startHead is not null)
        {
            return;
        }
        _textVersion = _palette.Version;
        var p = _palette;
        _startHead = ChartsBText.Make(StartLabel, p, p.TypeSM, p.TextMuted);
        _endHead = ChartsBText.Make(EndLabel, p, p.TypeSM, p.TextMuted);
        _empty = ChartsBText.Make(EmptyLabel, p, p.TypeSM, p.TextMuted);
        foreach (var row in _rows.Values)
        {
            row.Version = -1;
        }
    }

    private void EnsureRowText(Row row, double x0)
    {
        if (row.Version == _palette.Version && row.Name is not null)
        {
            return;
        }
        row.Version = _palette.Version;
        var p = _palette;
        var narrow = Narrow;
        var size = narrow ? p.TypeXS : p.TypeSM;
        var gap = narrow ? 6 : 8;
        var accent = row.Item.Key == HighlightKey;
        row.StartValue = ChartsBText.Make(Format(row.Item.Start), p, size, p.Foreground, accent ? FontWeight.Medium : FontWeight.Normal);
        row.EndValue = ChartsBText.Make(Format(row.Item.End), p, size, p.Foreground, accent ? FontWeight.Medium : FontWeight.Normal);
        var nameRoom = Math.Max(0, x0 - 10 - row.StartValue.WidthIncludingTrailingWhitespace - gap);
        row.Name = ChartsBText.Trimmed(row.Item.Label, p, size, p.TextSecondary, nameRoom);
        row.Rank = ChartsBText.Make(row.RankText, p, p.TypeXS, p.TextMuted);
        row.RankOld = row.RankPrevious is null ? null : ChartsBText.Make(row.RankPrevious, p, p.TypeXS, p.TextMuted);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        if (width <= 0)
        {
            return;
        }
        EnsureText(width);
        var p = _palette;
        if (_data.Count == 0)
        {
            context.DrawText(_empty!, new Point((width - _empty!.Width) / 2, (Bounds.Height - _empty.Height) / 2));
            return;
        }
        using var fade = context.PushOpacity(_reveal.Fade);
        var (x0, x1) = Columns(width);
        var plot = PlotHeightResolved;
        context.DrawText(_startHead!, new Point(x0 - _startHead!.Width / 2, (20 - _startHead.Height) / 2));
        context.DrawText(_endHead!, new Point(x1 - _endHead!.Width / 2, (20 - _endHead.Height) / 2));
        var axis = p.Pen(p.Border, 1);
        context.DrawLine(axis, new Point(Math.Round(x0) + 0.5, Head), new Point(Math.Round(x0) + 0.5, Head + plot));
        context.DrawLine(axis, new Point(Math.Round(x1) + 0.5, Head), new Point(Math.Round(x1) + 0.5, Head + plot));
        var neutral = ChartsBMath.MixIn(p.Foreground, 0.36, p.Surface);
        var leader = p.Pen(p.BorderStrong, 1);
        var active = ActiveKey;
        // Draw the item in focus last so it comes forward.
        foreach (var row in _drawOrder)
        {
            var g = row.Geometry.Value;
            var accent = row.Item.Key == HighlightKey;
            var ink = accent ? p.Series(0) : ChartsBMath.Lerp(neutral, p.Foreground, row.Emphasis.Value);
            var inkBrush = row.Emphasis.IsAnimating ? new ImmutableSolidColorBrush(ink) : p.Brush(ink);
            var dim = row.Dim.Value;
            using (context.PushOpacity(1 - 0.86 * dim))
            {
                context.DrawLine(leader, new Point(x0 - 6, Head + g.L0), new Point(x0 - 1, Head + g.Y0));
                context.DrawLine(leader, new Point(x1 + 6, Head + g.L1), new Point(x1 + 1, Head + g.Y1));
                var draw = row.Draw.Value;
                if (draw > 0.001)
                {
                    var a = new Point(x0, Head + g.Y0);
                    var b = new Point(x1, Head + g.Y1);
                    var pen = row.Emphasis.IsAnimating ? new ImmutablePen(inkBrush, 2, null, PenLineCap.Round) : p.Pen(ink, 2, PenLineCap.Round);
                    context.DrawLine(pen, a, ChartsBMath.Mix(a, b, draw));
                }
                var ring = p.Pen(p.Surface, 2);
                if (row.StartDot.Value > 0.01)
                {
                    var r = 4 * row.StartDot.Value;
                    context.DrawEllipse(inkBrush, ring, new Point(x0, Head + g.Y0), r, r);
                }
                if (row.EndDot.Value > 0.01)
                {
                    var r = 4 * row.EndDot.Value;
                    context.DrawEllipse(inkBrush, ring, new Point(x1, Head + g.Y1), r, r);
                }
            }
            DrawLabels(context, row, x0, x1, g, accent, dim);
        }
        _tooltip.Draw(context, p);
    }

    private void DrawLabels(DrawingContext context, Row row, double x0, double x1, SlopeGeometry g, bool accent, double dim)
    {
        EnsureRowText(row, x0);
        var p = _palette;
        var gap = Narrow ? 6 : 8;
        var labelOpacity = 1 - 0.7 * dim;
        var nameColor = accent ? p.Foreground : ChartsBMath.Lerp(p.TextSecondary, p.Foreground, row.Emphasis.Value);
        row.Name!.SetForegroundBrush(p.Brush(nameColor));
        if (row.StartLabel.Value > 0.001)
        {
            using (context.PushOpacity(row.StartLabel.Value * labelOpacity))
            {
                var right = x0 - 10;
                var value = row.StartValue!;
                var valueX = right - value.WidthIncludingTrailingWhitespace;
                context.DrawText(value, new Point(valueX, Head + g.L0 - value.Height / 2));
                var name = row.Name!;
                context.DrawText(name, new Point(valueX - gap - name.WidthIncludingTrailingWhitespace, Head + g.L0 - name.Height / 2));
            }
        }
        if (row.EndLabel.Value > 0.001)
        {
            using (context.PushOpacity(row.EndLabel.Value * labelOpacity))
            {
                var value = row.EndValue!;
                var x = x1 + 10;
                context.DrawText(value, new Point(x, Head + g.L1 - value.Height / 2));
                if (IsRankVisible)
                {
                    DrawRank(context, row, x + value.WidthIncludingTrailingWhitespace + gap, Head + g.L1);
                }
            }
        }
    }

    /// <summary>Rank change: an arrow glyph (up, down) and the number of places, rolling in the direction of the move.</summary>
    private void DrawRank(DrawingContext context, Row row, double x, double centerY)
    {
        var p = _palette;
        var move = row.Rank0 - row.Rank1;
        var color = move > 0 ? p.TextSecondary : p.TextMuted;
        if (move != 0)
        {
            var pen = p.Pen(color, 1.3, PenLineCap.Round);
            var top = centerY - 4;
            if (move > 0)
            {
                context.DrawLine(pen, new Point(x + 4, top + 7), new Point(x + 4, top + 1));
                context.DrawLine(pen, new Point(x + 1.5, top + 3.5), new Point(x + 4, top + 1));
                context.DrawLine(pen, new Point(x + 4, top + 1), new Point(x + 6.5, top + 3.5));
            }
            else
            {
                context.DrawLine(pen, new Point(x + 4, top + 1), new Point(x + 4, top + 7));
                context.DrawLine(pen, new Point(x + 1.5, top + 4.5), new Point(x + 4, top + 7));
                context.DrawLine(pen, new Point(x + 4, top + 7), new Point(x + 6.5, top + 4.5));
            }
            x += 10;
        }
        var rank = row.Rank!;
        rank.SetForegroundBrush(p.Brush(color));
        var roll = row.RankRoll.Value;
        var shift = 6 * row.RankDirection;
        using (context.PushClip(new Rect(x - 1, centerY - rank.Height / 2 - 1, Math.Max(rank.Width, row.RankOld?.Width ?? 0) + 2, rank.Height + 2)))
        {
            if (roll < 0.999 && row.RankOld is { } old)
            {
                using (context.PushOpacity(1 - roll))
                {
                    old.SetForegroundBrush(p.Brush(color));
                    context.DrawText(old, new Point(x, centerY - old.Height / 2 - shift * roll));
                }
            }
            using (context.PushOpacity(ChartsBMath.Clamp(roll, 0, 1)))
            {
                context.DrawText(rank, new Point(x, centerY - rank.Height / 2 + shift * (1 - roll)));
            }
        }
    }
}

/// <summary>Morphing geometry of one slope line: dot positions and spread label positions.</summary>
internal readonly record struct SlopeGeometry(double Y0, double Y1, double L0, double L1)
{
    public static readonly IMotionVector<SlopeGeometry> Vector = new SlopeGeometryVector();

    private sealed class SlopeGeometryVector : IMotionVector<SlopeGeometry>
    {
        public int Dimensions => 4;

        public void Write(SlopeGeometry value, Span<double> components)
        {
            components[0] = value.Y0;
            components[1] = value.Y1;
            components[2] = value.L0;
            components[3] = value.L1;
        }

        public SlopeGeometry Read(ReadOnlySpan<double> components) => new(components[0], components[1], components[2], components[3]);
    }
}
