using System.Text;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>What clicking a DonutChart legend row does.</summary>
public enum DonutLegendAction
{
    /// <summary>Shows or hides the segment; the rest of the ring redistributes.</summary>
    Toggle,

    /// <summary>Pins the segment as the selected one (<see cref="DonutChart.ActiveKey"/>).</summary>
    Select
}

/// <summary>
/// Arc's donut chart: a few parts of one whole. The legend and the ring show and hide segments together, the
/// center reads the active segment (or the total at rest) and rolls like a drum between readouts, small parts
/// fold into "Other", and new data moves every arc from where it is on screen, keeping its velocity. The first
/// time the chart is seen all segments sweep out of the top together, each trailing edge a beat behind.
/// <para>
/// Arc interpolates start and end angles rather than paths; every angle is a <see cref="MotionValue{T}"/>
/// written into one self-drawn ring, which ControlTheme transitions cannot express.
/// </para>
/// </summary>
/// <example><code>&lt;arc:DonutChart Label="Visits by source" Unit="visits" ItemsSource="{Binding Sources}" /&gt;</code></example>
[TemplatePart(RowsPart, typeof(Panel))]
[TemplatePart(CenterLabelPart, typeof(ArcContentSwap))]
[TemplatePart(CenterValuePart, typeof(ArcContentSwap))]
[TemplatePart(CenterMetaPart, typeof(ArcContentSwap))]
public class DonutChart : ChartBase
{
    public const string RowsPart = "PART_Rows";
    public const string CenterPart = "PART_Center";
    public const string CenterLabelPart = "PART_CenterLabel";
    public const string CenterValuePart = "PART_CenterValue";
    public const string CenterMetaPart = "PART_CenterMeta";
    internal const string OtherKey = "__other";

    private const double Lift = 4;
    private const double GrowBy = 2;
    private const double RingMargin = 7;
    private const double Gap = 3;
    private const double Corner = 4;
    private static readonly SpringSpec Reveal = SpringSpec.FromVisual(0.72, 0);
    private static readonly SpringSpec Settle = SpringSpec.FromVisual(0.5, 0);
    private static readonly SpringSpec Pop = SpringSpec.FromVisual(0.26, 0.12);

    /// <summary>Center label at rest, above the total.</summary>
    public static readonly StyledProperty<string> TotalLabelProperty =
        AvaloniaProperty.Register<DonutChart, string>(nameof(TotalLabel), "Total");

    /// <summary>Label of the segment that holds the grouped small parts.</summary>
    public static readonly StyledProperty<string> OtherLabelProperty =
        AvaloniaProperty.Register<DonutChart, string>(nameof(OtherLabel), "Other");

    /// <summary>Ring diameter in pixels; the ring scales down to fit narrower containers.</summary>
    public static readonly StyledProperty<double> DiameterProperty =
        AvaloniaProperty.Register<DonutChart, double>(nameof(Diameter), 208);

    /// <summary>Ring thickness in pixels.</summary>
    public static readonly StyledProperty<double> RingThicknessProperty =
        AvaloniaProperty.Register<DonutChart, double>(nameof(RingThickness), 24);

    /// <summary>Segments below this share of the total join "Other" (only when at least two would).</summary>
    public static readonly StyledProperty<double> GroupBelowProperty =
        AvaloniaProperty.Register<DonutChart, double>(nameof(GroupBelow), 0.04);

    /// <summary>The most segments drawn, counting "Other".</summary>
    public static readonly StyledProperty<int> MaxSegmentsProperty =
        AvaloniaProperty.Register<DonutChart, int>(nameof(MaxSegments), 6);

    /// <summary>The selected (pinned) segment key. Hover and focus preview other segments without changing it. Two-way.</summary>
    public static readonly StyledProperty<string?> ActiveKeyProperty =
        AvaloniaProperty.Register<DonutChart, string?>(nameof(ActiveKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Keys of hidden segments. A hidden segment closes and the rest of the ring redistributes. Two-way.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> HiddenKeysProperty =
        AvaloniaProperty.Register<DonutChart, IReadOnlyList<string>?>(nameof(HiddenKeys), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>What clicking a legend row does.</summary>
    public static readonly StyledProperty<DonutLegendAction> LegendActionProperty =
        AvaloniaProperty.Register<DonutChart, DonutLegendAction>(nameof(LegendAction));

    /// <summary>The synced legend beside (at least 460px wide) or below the ring.</summary>
    public static new readonly StyledProperty<bool> ShowLegendProperty =
        AvaloniaProperty.Register<DonutChart, bool>(nameof(ShowLegend), true);

    /// <summary>Raised when the selected segment changes.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ActiveKeyChangedEvent =
        RoutedEvent.Register<DonutChart, RoutedEventArgs>(nameof(ActiveKeyChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the legend shows or hides a segment.</summary>
    public static readonly RoutedEvent<ChartHiddenSeriesChangedEventArgs> HiddenKeysChangedEvent =
        RoutedEvent.Register<DonutChart, ChartHiddenSeriesChangedEventArgs>(nameof(HiddenKeysChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="TotalLabelProperty"/>
    public string TotalLabel { get => GetValue(TotalLabelProperty); set => SetValue(TotalLabelProperty, value); }

    /// <inheritdoc cref="OtherLabelProperty"/>
    public string OtherLabel { get => GetValue(OtherLabelProperty); set => SetValue(OtherLabelProperty, value); }

    /// <inheritdoc cref="DiameterProperty"/>
    public double Diameter { get => GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }

    /// <inheritdoc cref="RingThicknessProperty"/>
    public double RingThickness { get => GetValue(RingThicknessProperty); set => SetValue(RingThicknessProperty, value); }

    /// <inheritdoc cref="GroupBelowProperty"/>
    public double GroupBelow { get => GetValue(GroupBelowProperty); set => SetValue(GroupBelowProperty, value); }

    /// <inheritdoc cref="MaxSegmentsProperty"/>
    public int MaxSegments { get => GetValue(MaxSegmentsProperty); set => SetValue(MaxSegmentsProperty, value); }

    /// <inheritdoc cref="ActiveKeyProperty"/>
    public string? ActiveKey { get => GetValue(ActiveKeyProperty); set => SetValue(ActiveKeyProperty, value); }

    /// <inheritdoc cref="HiddenKeysProperty"/>
    public IReadOnlyList<string>? HiddenKeys { get => GetValue(HiddenKeysProperty); set => SetValue(HiddenKeysProperty, value); }

    /// <inheritdoc cref="LegendActionProperty"/>
    public DonutLegendAction LegendAction { get => GetValue(LegendActionProperty); set => SetValue(LegendActionProperty, value); }

    /// <inheritdoc cref="ShowLegendProperty"/>
    public bool ShowLegend { get => GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }

    /// <inheritdoc cref="ActiveKeyChangedEvent"/>
    public event EventHandler<RoutedEventArgs>? ActiveKeyChanged
    {
        add => AddHandler(ActiveKeyChangedEvent, value);
        remove => RemoveHandler(ActiveKeyChangedEvent, value);
    }

    /// <inheritdoc cref="HiddenKeysChangedEvent"/>
    public event EventHandler<ChartHiddenSeriesChangedEventArgs>? HiddenKeysChanged
    {
        add => AddHandler(HiddenKeysChangedEvent, value);
        remove => RemoveHandler(HiddenKeysChangedEvent, value);
    }

    /// <summary>A slice of the ring (a data item, or "Other" holding the grouped members).</summary>
    internal sealed class Slice
    {
        public required string Key { get; init; }
        public required string Label { get; init; }
        public double Value { get; init; }
        public Color? Color { get; init; }
        public int DataIndex { get; init; } = -1;
        public List<(string Key, string Label, double Value)>? Members { get; init; }
    }

    private sealed class Segment
    {
        public required MotionValue<double> Start { get; init; }
        public required MotionValue<double> End { get; init; }
        public required MotionValue<double> LiftValue { get; init; }
        public required MotionValue<double> Dim { get; init; }
        public StreamGeometry? Geometry { get; set; }
        public (double, double, double, double) GeometryKey { get; set; }
    }

    private Slice[] _slices = [];
    private List<Slice> _drawn = new();
    private readonly Dictionary<string, Segment> _segments = new();
    private readonly Dictionary<string, (double Start, double End)> _targets = new();
    private readonly Dictionary<string, int> _paletteOrder = new();
    private string? _preview;
    private MotionValue<double>? _track;
    private bool _swept;
    private DonutChartLegend? _rows;
    private ArcContentSwap? _centerLabel;
    private ArcContentSwap? _centerValue;
    private ArcContentSwap? _centerMeta;
    private Control? _center;
    private int _readoutIndex;

    static DonutChart()
    {
        EmptyLabelProperty.OverrideDefaultValue<DonutChart>("No data yet");
        CategoryLabelProperty.OverrideDefaultValue<DonutChart>("Segment");
        AffectsMeasure<DonutChart>(DiameterProperty, RingThicknessProperty);
    }

    private protected override double RevealThreshold => 0.35;

    internal IReadOnlyList<Slice> Slices => _slices;

    internal IReadOnlyList<string> HiddenList => HiddenKeys ?? Array.Empty<string>();

    internal bool IsSliceHidden(string key)
    {
        foreach (var hidden in HiddenList)
        {
            if (hidden == key)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The sum of the visible segments.</summary>
    public double Total
    {
        get
        {
            EnsureData();
            double total = 0;
            foreach (var slice in _slices)
            {
                if (!IsSliceHidden(slice.Key))
                {
                    total += slice.Value;
                }
            }
            return total;
        }
    }

    /// <summary>The segment the center reads: hover or keyboard preview first, then the selection.</summary>
    internal string? CurrentKey => IsVisibleSlice(_preview) ? _preview : IsVisibleSlice(ActiveKey) ? ActiveKey : null;

    private bool IsVisibleSlice(string? key)
    {
        if (key is null || IsSliceHidden(key))
        {
            return false;
        }
        foreach (var slice in _slices)
        {
            if (slice.Key == key)
            {
                return true;
            }
        }
        return false;
    }

    private MotionValue<double> TrackOpacity => _track ??= CreateValue(1);

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidatePlots();
        return value;
    }

    private protected override void OnTemplateApplied(INameScope scope)
    {
        _rows = scope.Find<DonutChartLegend>(RowsPart);
        if (_rows is not null)
        {
            _rows.Owner = this;
        }
        _center = scope.Find<Control>(CenterPart);
        _centerLabel = scope.Find<ArcContentSwap>(CenterLabelPart);
        _centerValue = scope.Find<ArcContentSwap>(CenterValuePart);
        _centerMeta = scope.Find<ArcContentSwap>(CenterMetaPart);
        UpdateRingRole();
        SyncRows();
        UpdateCenter();
    }

    /// <summary>With a legend the ring is decoration (the legend is the keyboard path); without one it is a focusable group.</summary>
    private void UpdateRingRole()
    {
        if (MainPlot is { } plot)
        {
            plot.Focusable = !ShowLegend;
            AutomationProperties.SetAccessibilityView(plot, ShowLegend ? AccessibilityView.Raw : AccessibilityView.Content);
        }
    }

    #region Data

    private protected override void OnDataChanged(ChartData previous, ChartData current)
    {
        _slices = SliceData(current);
        // Colors follow a key from the first time it is seen, so a segment keeps its color through every dataset.
        for (var i = 0; i < current.Count; i++)
        {
            if (!_paletteOrder.ContainsKey(current.Keys[i]))
            {
                _paletteOrder[current.Keys[i]] = _paletteOrder.Count;
            }
        }
        // Leaving segments stay drawn in their old place while they close, then drop out.
        var next = new List<Slice>(_slices);
        for (var index = 0; index < _drawn.Count; index++)
        {
            var item = _drawn[index];
            if (next.Exists(s => s.Key == item.Key))
            {
                continue;
            }
            var insertAt = 0;
            for (var before = index - 1; before >= 0; before--)
            {
                var found = next.FindIndex(s => s.Key == _drawn[before].Key);
                if (found >= 0)
                {
                    insertAt = found + 1;
                    break;
                }
            }
            next.Insert(insertAt, new Slice { Key = item.Key, Label = item.Label, Value = 0, Color = item.Color, DataIndex = item.DataIndex, Members = item.Members });
        }
        _drawn = next;
        SyncRows();
        Layout();
        UpdateCenter();
    }

    /// <summary>Small parts join one "Other" segment, but only when at least two would; grouping a single part hides it for nothing.</summary>
    private Slice[] SliceData(ChartData data)
    {
        var values = data.Series.Length > 0 ? data.Series[0].Values : [];
        var parts = new List<Slice>();
        double total = 0;
        for (var i = 0; i < data.Count; i++)
        {
            var value = i < values.Length ? values[i] : 0;
            if (value > 0)
            {
                parts.Add(new Slice { Key = data.Keys[i], Label = data.Labels[i], Value = value, Color = data.PointColors[i], DataIndex = i });
                total += value;
            }
        }
        if (total <= 0)
        {
            return [];
        }
        var ranked = parts.OrderByDescending(p => p.Value).ToList();
        var keep = new HashSet<string>(StringComparer.Ordinal);
        for (var rank = 0; rank < ranked.Count; rank++)
        {
            if (ranked[rank].Value / total >= GroupBelow && rank < MaxSegments - 1)
            {
                keep.Add(ranked[rank].Key);
            }
        }
        var rest = parts.Where(p => !keep.Contains(p.Key)).ToList();
        if (rest.Count < 2)
        {
            return parts.ToArray();
        }
        var result = parts.Where(p => keep.Contains(p.Key)).ToList();
        result.Add(new Slice
        {
            Key = OtherKey,
            Label = OtherLabel,
            Value = rest.Sum(p => p.Value),
            Members = rest.Select(p => (p.Key, p.Label, p.Value)).ToList()
        });
        return result.ToArray();
    }

    /// <summary>Turns of the ring for each slice in order; a hidden slice closes to a point where it sits.</summary>
    private void ComputeTargets()
    {
        _targets.Clear();
        double total = 0;
        foreach (var slice in _slices)
        {
            if (!IsSliceHidden(slice.Key))
            {
                total += slice.Value;
            }
        }
        if (total <= 0)
        {
            total = 1;
        }
        double at = 0;
        foreach (var slice in _slices)
        {
            var end = at + (IsSliceHidden(slice.Key) ? 0 : slice.Value / total);
            _targets[slice.Key] = (at, end);
            at = end;
        }
    }

    /// <summary>New data, or a segment shown or hidden, moves every arc from wherever it is on screen, keeping its velocity.</summary>
    private void Layout()
    {
        ComputeTargets();
        var shown = IsRevealed || MotionLevel != ArcMotionLevel.Full;
        if (!shown)
        {
            InvalidatePlots();
            return;
        }
        var level = MotionLevel;
        var first = !_swept;
        _swept = true;
        _ = TrackOpacity.AnimateTo(_slices.Length == 0 ? 1 : 0, level == ArcMotionLevel.Full
            ? MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.Standard)
            : MotionSpec.Instant);
        for (var index = 0; index < _drawn.Count; index++)
        {
            var item = _drawn[index];
            var leaving = !_targets.ContainsKey(item.Key);
            (double Start, double End) to;
            if (_targets.TryGetValue(item.Key, out var target))
            {
                to = target;
            }
            else
            {
                var place = PlaceBefore(index);
                to = (place, place);
            }
            if (!_segments.TryGetValue(item.Key, out var segment))
            {
                // A new segment opens from the edge of the one before it, as it is right now.
                double from = 0;
                if (!first)
                {
                    for (var before = index - 1; before >= 0; before--)
                    {
                        if (_segments.TryGetValue(_drawn[before].Key, out var prior))
                        {
                            from = prior.End.Value;
                            break;
                        }
                    }
                }
                segment = new Segment
                {
                    Start = CreateValue(from),
                    End = CreateValue(from),
                    LiftValue = CreateValue(0),
                    Dim = CreateValue(0)
                };
                _segments[item.Key] = segment;
            }
            if (level != ArcMotionLevel.Full)
            {
                segment.Start.Jump(to.Start);
                segment.End.Jump(to.End);
                if (leaving)
                {
                    Drop(item.Key);
                }
                continue;
            }
            if (first)
            {
                // One sweep: every segment leaves the top together, each trailing edge a beat behind the one before.
                _ = segment.Start.AnimateTo(to.Start, MotionSpec.Spring(Reveal));
                _ = segment.End.AnimateTo(to.End, MotionSpec.Spring(Reveal, TimeSpan.FromSeconds(0.04 + index * ArcMotionPresets.StaggerItem.TotalSeconds)));
                continue;
            }
            _ = segment.Start.AnimateTo(to.Start, MotionSpec.Spring(Settle));
            var end = segment.End.AnimateTo(to.End, MotionSpec.Spring(Settle));
            if (leaving)
            {
                var key = item.Key;
                _ = DropWhenClosed(end, key);
            }
        }
        UpdateLift();
        InvalidatePlots();
    }

    private double PlaceBefore(int index)
    {
        for (var before = index - 1; before >= 0; before--)
        {
            if (_targets.TryGetValue(_drawn[before].Key, out var arc))
            {
                return arc.End;
            }
        }
        return 0;
    }

    private async Task DropWhenClosed(Task<bool> closing, string key)
    {
        if (await closing)
        {
            Drop(key);
        }
    }

    private void Drop(string key)
    {
        if (Array.Exists(_slices, s => s.Key == key))
        {
            return;
        }
        _segments.Remove(key);
        _drawn.RemoveAll(s => s.Key == key);
        InvalidatePlots();
    }

    private protected override void OnRevealed() => Layout();

    /// <summary>The active segment slides out along its middle and thickens; the one it replaces settles back.</summary>
    private void UpdateLift()
    {
        var current = CurrentKey;
        var full = MotionLevel == ArcMotionLevel.Full;
        foreach (var (key, segment) in _segments)
        {
            var to = key == current ? 1 : 0;
            if (Math.Abs(segment.LiftValue.Target - to) > 1e-9)
            {
                _ = segment.LiftValue.AnimateTo(to, full ? MotionSpec.Spring(Pop) : MotionSpec.Instant);
            }
            var dim = current is not null && key != current ? 1 : 0;
            if (Math.Abs(segment.Dim.Target - dim) > 1e-9)
            {
                _ = segment.Dim.AnimateTo(dim, MotionLevel == ArcMotionLevel.Off ? MotionSpec.Instant : MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
            }
        }
    }

    #endregion

    #region Selection and visibility

    internal void Select(string? key)
    {
        SetCurrentValue(ActiveKeyProperty, key);
    }

    internal void SetPreview(string? key)
    {
        if (_preview == key)
        {
            return;
        }
        _preview = key;
        OnCurrentChanged();
    }

    private void OnCurrentChanged()
    {
        UpdateLift();
        UpdateCenter();
        _rows?.Refresh();
        var current = CurrentKey;
        var slice = current is null ? null : Array.Find(_slices, s => s.Key == current);
        SetActive(slice is { DataIndex: >= 0 } ? slice.DataIndex : null);
        InvalidatePlots();
    }

    internal void ToggleHidden(string key)
    {
        var hidden = IsSliceHidden(key);
        var visible = _slices.Count(s => !IsSliceHidden(s.Key));
        if (!hidden && visible <= 1)
        {
            return;
        }
        var next = hidden ? HiddenList.Where(k => k != key).ToList() : HiddenList.Append(key).ToList();
        SetCurrentValue(HiddenKeysProperty, next.AsReadOnly());
        RaiseEvent(new ChartHiddenSeriesChangedEventArgs(HiddenKeysChangedEvent, next.AsReadOnly()));
        var slice = Array.Find(_slices, s => s.Key == key);
        if (slice is not null)
        {
            Announcement = $"{slice.Label} {(hidden ? "shown" : "hidden")}. {TotalLabel} {FormatWithUnit(Total)}.";
        }
    }

    internal void ActivateRow(string key)
    {
        if (LegendAction == DonutLegendAction.Toggle)
        {
            ToggleHidden(key);
        }
        else
        {
            Select(ActiveKey == key ? null : key);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActiveKeyProperty)
        {
            OnCurrentChanged();
            RaiseEvent(new RoutedEventArgs(ActiveKeyChangedEvent));
        }
        else if (change.Property == HiddenKeysProperty)
        {
            Layout();
            UpdateCenter();
            _rows?.Refresh();
            RefreshText();
        }
        else if (change.Property == GroupBelowProperty || change.Property == MaxSegmentsProperty || change.Property == OtherLabelProperty)
        {
            OnDataChanged(Data, Data);
            RefreshText();
        }
        else if (change.Property == TotalLabelProperty)
        {
            UpdateCenter();
            RefreshText();
        }
        else if (change.Property == ShowLegendProperty)
        {
            UpdateRingRole();
            SyncRows();
        }
        else if (change.Property == DiameterProperty || change.Property == RingThicknessProperty)
        {
            InvalidatePlotLayout();
            InvalidatePlots();
        }
    }

    #endregion

    #region Center readout and legend

    private protected override void OnThemeInvalidated()
    {
        if (_rows is null)
        {
            return;
        }
        foreach (var row in _rows.Children)
        {
            row.InvalidateMeasure();
            row.InvalidateVisual();
        }
    }

    private void SyncRows()
    {
        if (_rows is null)
        {
            return;
        }
        _rows.IsVisible = ShowLegend && _slices.Length > 0;
        _rows.Sync(_slices);
    }

    /// <summary>One readout per segment plus the total; the active one rolls in from the side it sits on (drum).</summary>
    private void UpdateCenter()
    {
        var total = Total;
        var current = CurrentKey;
        var index = 0;
        Slice? active = null;
        for (var i = 0; i < _slices.Length; i++)
        {
            if (_slices[i].Key == current)
            {
                index = i + 1;
                active = _slices[i];
            }
        }
        var direction = index >= _readoutIndex ? 1 : -1;
        _readoutIndex = index;
        string label, value, meta;
        if (active is not null)
        {
            label = active.Label;
            value = FormatValue(active.Value);
            meta = $"{Share(active.Value / (total <= 0 ? 1 : total))} of {TotalLabel.ToLower(Culture)}";
        }
        else
        {
            label = total > 0 ? TotalLabel : EmptyLabel;
            value = total > 0 ? FormatValue(total) : " ";
            meta = string.IsNullOrEmpty(Unit) ? " " : Unit;
        }
        Set(_centerLabel, label, direction);
        Set(_centerValue, value, direction);
        Set(_centerMeta, meta, direction);
    }

    private static void Set(ArcContentSwap? swap, string text, int direction)
    {
        if (swap is null)
        {
            return;
        }
        swap.Direction = direction;
        if (swap.Content is TextBlock block && block.Text == text)
        {
            return;
        }
        swap.Content = new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center };
    }

    internal static string Share(double share) => share > 0 && share < 0.01
        ? "<1%"
        : share.ToString("0%", Culture);

    internal Color SliceColor(Slice slice)
    {
        EnsurePalette();
        if (slice.Color is { } color)
        {
            return color;
        }
        if (slice.Key == OtherKey)
        {
            return Palette.OtherColor;
        }
        return Palette.SeriesColor(_paletteOrder.TryGetValue(slice.Key, out var order) ? order : 0, ChartNeutralRamp.Donut);
    }

    internal string Describe(Slice slice)
    {
        var total = Total;
        var text = $"{slice.Label}, {FormatWithUnit(slice.Value)}, {Share(slice.Value / (total <= 0 ? 1 : total))}";
        if (slice.Members is { } members)
        {
            text += ". Includes " + string.Join(", ", members.Select(m => m.Label));
        }
        return text;
    }

    internal static string? MembersText(Slice slice)
    {
        if (slice.Members is not { } members)
        {
            return null;
        }
        return members.Count > 2
            ? $"{members[0].Label}, {members[1].Label} and {members.Count - 2} more"
            : string.Join(" and ", members.Select(m => m.Label));
    }

    private protected override string BuildSummary()
    {
        var total = Total;
        if (total <= 0)
        {
            return $"{Label}. {EmptyLabel}.";
        }
        var builder = new StringBuilder();
        builder.Append(Label).Append(". ").Append(TotalLabel).Append(' ').Append(FormatWithUnit(total)).Append('.');
        foreach (var slice in _slices)
        {
            if (!IsSliceHidden(slice.Key))
            {
                builder.Append(' ').Append(Describe(slice)).Append('.');
            }
        }
        return builder.ToString();
    }

    private protected override string BuildDataTable()
    {
        var total = Total;
        if (total <= 0)
        {
            return string.Empty;
        }
        var builder = new StringBuilder("Segment\tValue\tShare");
        foreach (var slice in _slices)
        {
            if (IsSliceHidden(slice.Key))
            {
                continue;
            }
            if (slice.Members is { } members)
            {
                foreach (var member in members)
                {
                    builder.Append('\n').Append(member.Label).Append(" (").Append(OtherLabel).Append(")\t")
                           .Append(FormatWithUnit(member.Value)).Append('\t').Append(Share(member.Value / total));
                }
            }
            else
            {
                builder.Append('\n').Append(slice.Label).Append('\t').Append(FormatWithUnit(slice.Value)).Append('\t').Append(Share(slice.Value / total));
            }
        }
        return builder.ToString();
    }

    #endregion

    #region Ring input

    private (double Scale, double C, double Outer, double Inner) Geometry(Size size)
    {
        var d = Diameter;
        var scale = Math.Min(1, Math.Min(size.Width, size.Height) / d);
        var c = d / 2;
        var outer = c - RingMargin;
        var inner = Math.Max(8, outer - RingThickness);
        return (scale, c, outer, inner);
    }

    internal override double MeasurePlotHeight(ChartPlot plot, double width) => Math.Min(Diameter, width);

    internal override double MeasurePlotWidth(ChartPlot plot, double available) => Math.Min(Diameter, available);

    internal override void OnPlotResized(ChartPlot plot, Size size)
    {
        if (_center is not null)
        {
            var (scale, _, _, inner) = Geometry(size);
            _center.MaxWidth = Math.Max(0, (inner * 2 - 16) * scale);
        }
    }

    /// <summary>One pointer handler for the whole ring: the angle picks the segment, so crossing a gap never drops the hover.</summary>
    private string? Hit(ChartPlot plot, Point point, out bool inside)
    {
        inside = false;
        var size = plot.Bounds.Size;
        var (scale, c, _, inner) = Geometry(size);
        var offset = (size.Width - Diameter * scale) / 2;
        var x = (point.X - offset) / scale - c;
        var y = point.Y / scale - c;
        var radius = Math.Sqrt(x * x + y * y);
        if (radius < inner - 6 || radius > c)
        {
            return null;
        }
        inside = true;
        var turn = (Math.Atan2(y, x) + Math.PI / 2) / (Math.PI * 2);
        if (turn < 0)
        {
            turn += 1;
        }
        foreach (var (key, arc) in _targets)
        {
            if (arc.End > arc.Start && turn >= arc.Start && turn < arc.End && !IsSliceHidden(key))
            {
                return key;
            }
        }
        return null;
    }

    internal override void OnPlotPointerMoved(ChartPlot plot, PointerEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse)
        {
            return;
        }
        var key = Hit(plot, e.GetPosition(plot), out var inside);
        plot.Cursor = key is not null ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
        if (key is not null || !inside)
        {
            SetPreview(key);
        }
    }

    internal override void OnPlotPointerExited(ChartPlot plot, PointerEventArgs e) => SetPreview(null);

    internal override void OnPlotPointerReleased(ChartPlot plot, PointerReleasedEventArgs e)
    {
        if (Hit(plot, e.GetPosition(plot), out _) is { } key)
        {
            Select(ActiveKey == key ? null : key);
        }
    }

    internal override void OnPlotKeyDown(ChartPlot plot, KeyEventArgs e)
    {
        var visible = _slices.Where(s => !IsSliceHidden(s.Key)).ToList();
        if (visible.Count == 0)
        {
            return;
        }
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            SetPreview(null);
            Select(null);
            return;
        }
        var current = CurrentKey;
        if (e.Key is Key.Enter or Key.Space && current is not null)
        {
            e.Handled = true;
            Select(ActiveKey == current ? null : current);
            return;
        }
        var index = visible.FindIndex(s => s.Key == current);
        int? next = e.Key switch
        {
            Key.Down or Key.Right => index + 1,
            Key.Up or Key.Left => index < 0 ? 0 - 1 + visible.Count : index - 1,
            Key.Home => 0,
            Key.End => visible.Count - 1,
            _ => null
        };
        if (next is not { } target)
        {
            return;
        }
        e.Handled = true;
        var item = visible[((target % visible.Count) + visible.Count) % visible.Count];
        SetPreview(item.Key);
        Announcement = Describe(item);
    }

    internal override void OnPlotFocusChanged(ChartPlot plot, bool focused, bool keyboard)
    {
        if (!focused)
        {
            SetPreview(null);
        }
        plot.InvalidateVisual();
    }

    internal override AutomationControlType GetPlotControlType(ChartPlot plot) => AutomationControlType.Group;

    internal override string GetPlotAutomationName(ChartPlot plot) => $"{Label}. Use the arrow keys to read each segment.";

    internal override string GetPlotValueText(ChartPlot plot)
    {
        var current = CurrentKey;
        var slice = current is null ? null : Array.Find(_slices, s => s.Key == current);
        return slice is null ? Summary : Describe(slice);
    }

    #endregion

    #region Rendering

    private protected override void RenderPlot(ChartPlot plot, DrawingContext context, Size size)
    {
        var palette = Palette;
        var (scale, c, outer, inner) = Geometry(size);
        var offset = (size.Width - Diameter * scale) / 2;
        var corner = Math.Min(Corner, RingThickness / 4);
        using var transform = context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset, 0));
        var center = new Point(c, c);
        var track = _track?.Value ?? 1;
        if (track > 0.001)
        {
            using (context.PushOpacity(track))
            {
                var radius = (outer + inner) / 2;
                context.DrawEllipse(null, palette.Pen(palette.SurfaceMuted, outer - inner), center, radius, radius);
            }
        }
        foreach (var item in _drawn)
        {
            if (!_segments.TryGetValue(item.Key, out var segment))
            {
                continue;
            }
            var start = segment.Start.Value;
            var end = segment.End.Value;
            var lift = Math.Max(0, segment.LiftValue.Value);
            var r = outer + lift * GrowBy;
            var key = (Math.Round(start, 5), Math.Round(end, 5), Math.Round(lift, 4), outer * 1000 + inner);
            if (segment.Geometry is null || segment.GeometryKey != key)
            {
                segment.Geometry = ChartGeometry.Sector(center, r, inner - lift * GrowBy * 0.5, start, end, Gap, corner);
                segment.GeometryKey = key;
            }
            if (segment.Geometry is null)
            {
                continue;
            }
            var fill = Math.Clamp((ChartGeometry.SectorBreadth(r, start, end, Gap) - 1) / 3, 0, 1);
            var middle = (start + end) / 2 * Math.PI * 2 - Math.PI / 2;
            var distance = lift * Lift;
            using (context.PushTransform(Matrix.CreateTranslation(Math.Cos(middle) * distance, Math.Sin(middle) * distance)))
            using (context.PushOpacity(fill * (1 - 0.55 * segment.Dim.Value)))
            {
                context.DrawGeometry(palette.Brush(SliceColor(item)), null, segment.Geometry);
            }
        }
        if (plot.IsFocusVisible)
        {
            context.DrawEllipse(null, palette.Pen(palette.FocusRing, 2), center, c - 1, c - 1);
        }
    }

    #endregion
}
