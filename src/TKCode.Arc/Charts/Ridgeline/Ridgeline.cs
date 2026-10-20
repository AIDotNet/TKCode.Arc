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
/// A ridgeline (joy plot, Arc "Ridgeline"): one smoothed density curve per group, overlapping like ridges and shaded by
/// median. Every curve is sampled on a shared 96-point grid, so a new dataset or domain morphs each ridge from its
/// on-screen shape on one 0.7s spring; the first view rolls the rows in from the top. Hover or ↑ / ↓ lifts a ridge
/// (a copy drawn above the rest) with its middle half and median; ← / → move a reading cursor along the values.
/// </summary>
public class Ridgeline : Control, IChartsBAccessible, Avalonia.Rendering.ICustomHitTest
{
    /// <summary>The whole box takes the pointer, not only the painted marks.</summary>
    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    /// <summary>Defines the <see cref="ItemsSource"/> property.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<Ridgeline, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Defines the <see cref="KeySelector"/> property.</summary>
    public static readonly StyledProperty<Func<object, string>?> KeySelectorProperty =
        AvaloniaProperty.Register<Ridgeline, Func<object, string>?>(nameof(KeySelector));

    /// <summary>Defines the <see cref="LabelSelector"/> property.</summary>
    public static readonly StyledProperty<Func<object, string>?> LabelSelectorProperty =
        AvaloniaProperty.Register<Ridgeline, Func<object, string>?>(nameof(LabelSelector));

    /// <summary>Defines the <see cref="ValuesSelector"/> property.</summary>
    public static readonly StyledProperty<Func<object, IEnumerable<double>>?> ValuesSelectorProperty =
        AvaloniaProperty.Register<Ridgeline, Func<object, IEnumerable<double>>?>(nameof(ValuesSelector));

    /// <summary>Defines the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Ridgeline, string?>(nameof(Label));

    /// <summary>Defines the <see cref="Unit"/> property.</summary>
    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<Ridgeline, string?>(nameof(Unit));

    /// <summary>Defines the <see cref="ValueFormatter"/> property.</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<Ridgeline, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>Defines the <see cref="DomainMinimum"/> property.</summary>
    public static readonly StyledProperty<double> DomainMinimumProperty =
        AvaloniaProperty.Register<Ridgeline, double>(nameof(DomainMinimum), double.NaN);

    /// <summary>Defines the <see cref="DomainMaximum"/> property.</summary>
    public static readonly StyledProperty<double> DomainMaximumProperty =
        AvaloniaProperty.Register<Ridgeline, double>(nameof(DomainMaximum), double.NaN);

    /// <summary>Defines the <see cref="Overlap"/> property.</summary>
    public static readonly StyledProperty<double> OverlapProperty =
        AvaloniaProperty.Register<Ridgeline, double>(nameof(Overlap), 2.4);

    /// <summary>Defines the <see cref="RowHeight"/> property.</summary>
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<Ridgeline, double>(nameof(RowHeight), 30);

    /// <summary>Defines the <see cref="Bandwidth"/> property.</summary>
    public static readonly StyledProperty<double> BandwidthProperty =
        AvaloniaProperty.Register<Ridgeline, double>(nameof(Bandwidth), double.NaN);

    /// <summary>Defines the <see cref="IsTinted"/> property.</summary>
    public static readonly StyledProperty<bool> IsTintedProperty =
        AvaloniaProperty.Register<Ridgeline, bool>(nameof(IsTinted), true);

    /// <summary>Defines the <see cref="ActiveId"/> property.</summary>
    public static readonly StyledProperty<string?> ActiveIdProperty =
        AvaloniaProperty.Register<Ridgeline, string?>(nameof(ActiveId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="EmptyLabel"/> property.</summary>
    public static readonly StyledProperty<string> EmptyLabelProperty =
        AvaloniaProperty.Register<Ridgeline, string>(nameof(EmptyLabel), "No data yet");

    internal static readonly StyledProperty<double> LiftProperty =
        AvaloniaProperty.Register<Ridgeline, double>(nameof(Lift), 6);

    internal static readonly StyledProperty<double> AxisHeightProperty =
        AvaloniaProperty.Register<Ridgeline, double>(nameof(AxisHeight), 28);

    internal static readonly StyledProperty<double> TickSpacingProperty =
        AvaloniaProperty.Register<Ridgeline, double>(nameof(TickSpacing), 72);

    internal const int Samples = 96;
    private const double TipGap = 14;

    private readonly ChartsBPalette _palette;
    private readonly ChartsBItemsWatcher _watcher;
    private readonly ChartsBMemberReader _reader = new();
    private readonly ChartsBReveal _reveal;
    private readonly ChartsBTooltip _tooltip;
    private readonly AvaloniaList<RidgelineSeries> _items = new();
    private readonly MotionValue<double> _progress;
    private readonly MotionValue<double> _lensLift;
    private readonly MotionValue<double> _lensOpacity;
    private readonly Dictionary<string, MotionValue<double>> _ridgeOpacity = new();
    private List<RidgelineSeries> _series = new();
    private Dictionary<string, RidgelineStats> _stats = new();
    private Dictionary<string, Shape> _targets = new();
    private Dictionary<string, Shape> _drawn = new();
    private Dictionary<string, Shape> _from = new();
    private double _lo;
    private double _hi = 1;
    private double _drawnLo;
    private double _drawnHi = 1;
    private double _fromLo;
    private double _fromHi = 1;
    private bool _firstMorph;
    private bool _morphing;
    private int _generation;
    private int _geometryStamp;
    private readonly Dictionary<string, (int Stamp, StreamGeometry Fill, StreamGeometry Line)> _geometry = new();
    private (double Value, double? PointerY)? _cursor;
    private string? _lensId;
    private BindingBase? _keyBinding;
    private BindingBase? _labelBinding;
    private BindingBase? _valuesBinding;
    private ChartsBAutomationPeer? _peer;
    private string? _tipSignature;
    private readonly List<(double Value, FormattedText Text)> _ticks = new();
    private readonly Dictionary<string, FormattedText> _rowLabels = new();
    private FormattedText? _empty;
    private FormattedText? _scaleLabel;
    private FormattedText? _scaleLo;
    private FormattedText? _scaleHi;
    private int _textVersion = -1;
    private double _textWidth = -1;

    /// <summary>A sampled curve plus its quartiles, in value units and row units.</summary>
    private sealed class Shape
    {
        public readonly double[] Xs = new double[Samples];
        public readonly double[] Ys = new double[Samples];
        public double Row;
        public double Q1;
        public double Median;
        public double Q3;
        public Color ColorFrom;
        public Color Color;

        public Shape Copy()
        {
            var copy = new Shape { Row = Row, Q1 = Q1, Median = Median, Q3 = Q3, ColorFrom = Color, Color = Color };
            Array.Copy(Xs, copy.Xs, Samples);
            Array.Copy(Ys, copy.Ys, Samples);
            return copy;
        }

        public double HeightAt(double x)
        {
            if (x <= Xs[0] || x >= Xs[Samples - 1])
            {
                return 0;
            }
            var i = 1;
            while (i < Samples - 1 && Xs[i] < x)
            {
                i++;
            }
            var span = Xs[i] - Xs[i - 1];
            var t = span == 0 ? 0 : (x - Xs[i - 1]) / span;
            return ChartsBMath.Mix(Ys[i - 1], Ys[i], t);
        }
    }

    /// <summary>Creates a ridgeline.</summary>
    public Ridgeline()
    {
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Cross);
        _palette = new ChartsBPalette(this, () =>
        {
            _textVersion = -1;
            RecolorTargets();
            InvalidateVisual();
        });
        _reveal = new ChartsBReveal(this, 0.3, _ => StartMorph());
        _tooltip = new ChartsBTooltip(this, ArcMotionPresets.Snappy, InvalidateVisual) { MinWidth = 170 };
        _progress = new MotionValue<double>(this, 1, MotionVectors.Double);
        _progress.Changed += OnProgress;
        _lensLift = Value(0);
        _lensOpacity = Value(0);
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

    static Ridgeline()
    {
        AffectsMeasure<Ridgeline>(OverlapProperty, RowHeightProperty, IsTintedProperty, LiftProperty, AxisHeightProperty);
        AffectsRender<Ridgeline>(TickSpacingProperty);
    }

    private MotionValue<double> Value(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    /// <summary>Series: <see cref="RidgelineSeries"/> instances, or any objects read through the selectors or member bindings; drawn top to bottom.</summary>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Inline series, used when <see cref="ItemsSource"/> is not set.</summary>
    [Content]
    public AvaloniaList<RidgelineSeries> Items => _items;

    /// <summary>Reads a series id (wins over <see cref="KeyMemberBinding"/>).</summary>
    public Func<object, string>? KeySelector
    {
        get => GetValue(KeySelectorProperty);
        set => SetValue(KeySelectorProperty, value);
    }

    /// <summary>Reads a series label.</summary>
    public Func<object, string>? LabelSelector
    {
        get => GetValue(LabelSelectorProperty);
        set => SetValue(LabelSelectorProperty, value);
    }

    /// <summary>Reads a series' raw observations.</summary>
    public Func<object, IEnumerable<double>>? ValuesSelector
    {
        get => GetValue(ValuesSelectorProperty);
        set => SetValue(ValuesSelectorProperty, value);
    }

    /// <summary>Binding (member path) that reads each series id.</summary>
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

    /// <summary>Binding that reads each series label.</summary>
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

    /// <summary>Binding that reads each series' observations (an enumerable of numbers).</summary>
    [AssignBinding]
    public BindingBase? ValuesMemberBinding
    {
        get => _valuesBinding;
        set
        {
            _valuesBinding = value;
            Rebuild();
        }
    }

    /// <summary>What is measured, such as "Daily highs in Zurich"; names the chart for assistive technology.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Unit after each value, such as "°C".</summary>
    public string? Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <summary>Formats values (default: grouped, one decimal at most, true minus sign, then <see cref="Unit"/>).</summary>
    public Func<double, string>? ValueFormatter
    {
        get => GetValue(ValueFormatterProperty);
        set => SetValue(ValueFormatterProperty, value);
    }

    /// <summary>Fixed lower end of the value axis (NaN: the data with a little room). A fixed domain keeps the axis still between datasets.</summary>
    public double DomainMinimum
    {
        get => GetValue(DomainMinimumProperty);
        set => SetValue(DomainMinimumProperty, value);
    }

    /// <summary>Fixed upper end of the value axis (NaN: automatic).</summary>
    public double DomainMaximum
    {
        get => GetValue(DomainMaximumProperty);
        set => SetValue(DomainMaximumProperty, value);
    }

    /// <summary>How far the tallest ridge may rise into the rows above, in row heights (default 2.4).</summary>
    public double Overlap
    {
        get => GetValue(OverlapProperty);
        set => SetValue(OverlapProperty, value);
    }

    /// <summary>Height of one row in pixels (default 30).</summary>
    public double RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    /// <summary>Smoothing in value units; NaN (default) uses Silverman's rule per series.</summary>
    public double Bandwidth
    {
        get => GetValue(BandwidthProperty);
        set => SetValue(BandwidthProperty, value);
    }

    /// <summary>Shades each ridge by its median, from a light tint to the full Series1 color, and shows the scale (default true).</summary>
    public bool IsTinted
    {
        get => GetValue(IsTintedProperty);
        set => SetValue(IsTintedProperty, value);
    }

    /// <summary>The lifted ridge (two-way).</summary>
    public string? ActiveId
    {
        get => GetValue(ActiveIdProperty);
        set => SetValue(ActiveIdProperty, value);
    }

    /// <summary>Message shown without observations.</summary>
    public string EmptyLabel
    {
        get => GetValue(EmptyLabelProperty);
        set => SetValue(EmptyLabelProperty, value);
    }

    /// <summary>Raised when the lifted ridge changes.</summary>
    public event EventHandler<RidgelineActiveIdChangedEventArgs>? ActiveIdChanged;

    internal double Lift
    {
        get => GetValue(LiftProperty);
        set => SetValue(LiftProperty, value);
    }

    internal double AxisHeight
    {
        get => GetValue(AxisHeightProperty);
        set => SetValue(AxisHeightProperty, value);
    }

    internal double TickSpacing
    {
        get => GetValue(TickSpacingProperty);
        set => SetValue(TickSpacingProperty, value);
    }

    /// <summary>The resolved series.</summary>
    public IReadOnlyList<RidgelineSeries> Series => _series;

    /// <summary>Value domain in use (target, after nice rounding).</summary>
    public (double Minimum, double Maximum) Domain => (_lo, _hi);

    internal bool IsMorphing => _morphing;

    internal double? CursorValue => _cursor?.Value;

    private bool IsEmpty => _series.Count == 0 || _series.All(s => s.Values.Count == 0);

    private bool Narrow => Bounds.Width > 0 && Bounds.Width < 420;

    private double Gutter => Narrow ? 36 : 52;

    private double Top => Math.Ceiling((Overlap - 1) * RowHeight) + Lift + 8;

    private double PlotBottom => Top + _series.Count * RowHeight + AxisHeight;

    private double PlotWidth => Math.Max(40, Bounds.Width - Gutter - 4);

    private double Baseline(double row) => Top + (row + 1) * RowHeight;

    private double TickStep => ChartsBMath.NiceStep(_hi - _lo, Math.Max(2, (int)Math.Floor(PlotWidth / TickSpacing)));

    // ---- statistics and text -----------------------------------------------------------------------------------

    private string Format(double value)
    {
        if (ValueFormatter is { } formatter)
        {
            return formatter(value);
        }
        return ChartsBMath.Grouped(value).Replace("-", "−") + Unit;
    }

    private string Describe(RidgelineSeries series)
    {
        var s = _stats[series.Id];
        return $"{series.Label}: median {Format(s.Median)}, middle half {Format(s.Q1)} to {Format(s.Q3)}, range {Format(s.Min)} to {Format(s.Max)}, {s.N} values";
    }

    /// <summary>Accessible summary: every ridge's median, middle half, range and count.</summary>
    public string Summary => IsEmpty ? $"{Label}. {EmptyLabel}." : $"{Label}. {string.Join("; ", _series.Select(Describe))}.";

    internal string? Reading
    {
        get
        {
            var series = ActiveId is { } id ? _series.FirstOrDefault(s => s.Id == id) : null;
            if (series is null)
            {
                return null;
            }
            var text = Describe(series);
            return _cursor is { } cursor
                ? $"{text}. {ChartsBMath.Percent(_stats[series.Id].FractionAtOrBelow(cursor.Value))} at or below {Format(cursor.Value)}"
                : text;
        }
    }

    /// <summary>Ridge tone: one hue from a light tint (low median) to full Series1 (high median).</summary>
    private Color Tone(double t) => ChartsBMath.MixIn(_palette.Series(0), (38 + ChartsBMath.Clamp(t, 0, 1) * 62) / 100, _palette.Surface);

    private Color RidgeColor(double median) => IsTinted ? Tone((median - _lo) / (_hi - _lo == 0 ? 1 : _hi - _lo)) : _palette.Series(0);

    // ---- data -------------------------------------------------------------------------------------------------

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            _watcher.SetSource(ItemsSource ?? _items);
        }
        else if (change.Property == KeySelectorProperty || change.Property == LabelSelectorProperty || change.Property == ValuesSelectorProperty ||
                 change.Property == DomainMinimumProperty || change.Property == DomainMaximumProperty || change.Property == BandwidthProperty)
        {
            Rebuild();
        }
        else if (change.Property == IsTintedProperty)
        {
            RecolorTargets();
            _textVersion = -1;
        }
        else if (change.Property == ActiveIdProperty)
        {
            if (change.GetNewValue<string?>() is null)
            {
                _cursor = null;
            }
            OnActiveChanged();
            var id = change.GetNewValue<string?>();
            ActiveIdChanged?.Invoke(this, new RidgelineActiveIdChangedEventArgs(id, _series.FirstOrDefault(s => s.Id == id)));
        }
        else if (change.Property == ValueFormatterProperty || change.Property == UnitProperty || change.Property == EmptyLabelProperty)
        {
            _textVersion = -1;
            _tipSignature = null;
            SyncTooltip();
            InvalidateVisual();
        }
        else if (change.Property == LabelProperty)
        {
            UpdateAccessibility();
        }
        else if (change.Property == BoundsProperty)
        {
            _geometryStamp++;
            if (Math.Abs(Bounds.Width - _textWidth) > 0.5)
            {
                _textVersion = -1;
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
        var series = new List<RidgelineSeries>();
        var seen = new HashSet<string>();
        foreach (var item in _watcher.Snapshot())
        {
            var resolved = Resolve(item);
            if (seen.Add(resolved.Id))
            {
                series.Add(resolved);
            }
        }
        _series = series;
        // Targets: one density per series on a shared domain, scaled so the tallest peak of all rises Overlap rows.
        _stats = series.ToDictionary(s => s.Id, s => RidgelineStats.Of(s.Values));
        var widths = series.ToDictionary(s => s.Id, s => double.IsNaN(Bandwidth) || Bandwidth <= 0 ? _stats[s.Id].Silverman() : Bandwidth);
        var filled = series.Where(s => _stats[s.Id].N > 0).ToList();
        var lo = !double.IsNaN(DomainMinimum) ? DomainMinimum : filled.Count > 0 ? filled.Min(s => _stats[s.Id].Min - 2.5 * widths[s.Id]) : double.NaN;
        var hi = !double.IsNaN(DomainMaximum) ? DomainMaximum : filled.Count > 0 ? filled.Max(s => _stats[s.Id].Max + 2.5 * widths[s.Id]) : double.NaN;
        if (!double.IsFinite(lo) || !double.IsFinite(hi) || lo == hi)
        {
            lo = 0;
            hi = 1;
        }
        if (double.IsNaN(DomainMinimum) && double.IsNaN(DomainMaximum))
        {
            var step = ChartsBMath.NiceStep(hi - lo, 5);
            lo = Math.Floor(lo / step) * step;
            hi = Math.Ceiling(hi / step) * step;
        }
        _lo = lo;
        _hi = hi;
        var targets = new Dictionary<string, Shape>();
        var peak = 1e-9;
        for (var row = 0; row < series.Count; row++)
        {
            var stats = _stats[series[row].Id];
            var shape = new Shape { Row = row, Q1 = stats.Q1, Median = stats.Median, Q3 = stats.Q3 };
            stats.Density(lo, hi, widths[series[row].Id], shape.Xs, shape.Ys);
            peak = Math.Max(peak, shape.Ys.Max());
            targets[series[row].Id] = shape;
        }
        foreach (var shape in targets.Values)
        {
            for (var i = 0; i < Samples; i++)
            {
                shape.Ys[i] /= peak;
            }
        }
        _targets = targets;
        RecolorTargets();
        Focusable = !IsEmpty;
        if (ActiveId is { } active && !targets.ContainsKey(active))
        {
            ActiveId = null;
        }
        _textVersion = -1;
        _tipSignature = null;
        if (_reveal.IsRevealed)
        {
            StartMorph();
        }
        SyncTooltip();
        UpdateAccessibility();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void RecolorTargets()
    {
        foreach (var shape in _targets.Values)
        {
            shape.Color = RidgeColor(shape.Median);
        }
        foreach (var (id, shape) in _drawn)
        {
            if (_targets.TryGetValue(id, out var target))
            {
                shape.Color = shape.ColorFrom = target.Color;
            }
        }
        InvalidateVisual();
    }

    private RidgelineSeries Resolve(object item)
    {
        if (item is RidgelineSeries typed && KeySelector is null && LabelSelector is null && ValuesSelector is null &&
            _keyBinding is null && _labelBinding is null && _valuesBinding is null)
        {
            return typed;
        }
        var source = item as RidgelineSeries;
        var id = KeySelector?.Invoke(item) ?? ChartsBMemberReader.ToText(_reader.Read(item, _keyBinding)) ?? source?.Id ?? item.ToString() ?? string.Empty;
        var label = LabelSelector?.Invoke(item) ?? ChartsBMemberReader.ToText(_reader.Read(item, _labelBinding)) ?? source?.Label ?? id;
        IEnumerable<double>? values = ValuesSelector?.Invoke(item);
        if (values is null && _valuesBinding is not null && _reader.Read(item, _valuesBinding) is IEnumerable raw)
        {
            values = raw.Cast<object?>().Select(ChartsBMemberReader.ToDouble);
        }
        return new RidgelineSeries(id, label, (values ?? source?.Values ?? []).ToArray());
    }

    /// <summary>Starts the morph from what is on screen to the targets (the first one is the staggered reveal).</summary>
    private void StartMorph()
    {
        var first = _drawn.Count == 0;
        _from = new Dictionary<string, Shape>();
        foreach (var (id, target) in _targets)
        {
            if (_drawn.TryGetValue(id, out var now))
            {
                var copy = now.Copy();
                copy.ColorFrom = now.Color;
                _from[id] = copy;
            }
            else
            {
                // A new ridge grows from its baseline.
                var flat = target.Copy();
                Array.Clear(flat.Ys);
                flat.Q1 = flat.Q3 = target.Median;
                flat.ColorFrom = target.Color;
                _from[id] = flat;
            }
        }
        _fromLo = first ? _lo : _drawnLo;
        _fromHi = first ? _hi : _drawnHi;
        _drawn = _from.ToDictionary(p => p.Key, p => p.Value.Copy());
        _firstMorph = first;
        if (!ArcMotion.IsFull(this) || !IsLoaded)
        {
            Finish();
            return;
        }
        _morphing = true;
        _progress.Jump(0);
        var spec = first ? ChartsBMath.Physical(1, 0) : ChartsBMath.Physical(0.7, 0.08);
        FinishWhenDone(_progress.AnimateTo(1, MotionSpec.Spring(spec)), ++_generation);
    }

    private async void FinishWhenDone(Task<bool> run, int generation)
    {
        if (await run && generation == _generation)
        {
            Finish();
        }
    }

    private void OnProgress(double t)
    {
        if (!_morphing)
        {
            return;
        }
        _drawnLo = ChartsBMath.Mix(_fromLo, _lo, t);
        _drawnHi = ChartsBMath.Mix(_fromHi, _hi, t);
        var count = _targets.Count;
        foreach (var (id, to) in _targets)
        {
            if (!_from.TryGetValue(id, out var from) || !_drawn.TryGetValue(id, out var output))
            {
                continue;
            }
            // The first reveal rolls the ridges in from the top row down.
            var local = _firstMorph ? ChartsBMath.Clamp((t - to.Row * 0.025) / (1 - Math.Min(0.5, count * 0.025)), 0, 1) : t;
            for (var i = 0; i < Samples; i++)
            {
                output.Xs[i] = ChartsBMath.Mix(from.Xs[i], to.Xs[i], local);
                output.Ys[i] = ChartsBMath.Mix(from.Ys[i], to.Ys[i], local);
            }
            output.Row = ChartsBMath.Mix(from.Row, to.Row, local);
            output.Q1 = ChartsBMath.Mix(from.Q1, to.Q1, local);
            output.Median = ChartsBMath.Mix(from.Median, to.Median, local);
            output.Q3 = ChartsBMath.Mix(from.Q3, to.Q3, local);
            output.Color = ChartsBMath.Lerp(from.ColorFrom, to.Color, ChartsBMath.Clamp(local, 0, 1));
        }
        _geometryStamp++;
        InvalidateVisual();
    }

    private void Finish()
    {
        _morphing = false;
        _drawn = _targets.ToDictionary(p => p.Key, p => p.Value.Copy());
        _drawnLo = _lo;
        _drawnHi = _hi;
        _geometryStamp++;
        InvalidateVisual();
    }

    // ---- interaction ------------------------------------------------------------------------------------------

    private void OnActiveChanged()
    {
        var active = ActiveId;
        var full = ArcMotion.IsFull(this) && IsLoaded;
        foreach (var series in _series)
        {
            if (!_ridgeOpacity.TryGetValue(series.Id, out var value))
            {
                value = Value(1);
                _ridgeOpacity[series.Id] = value;
            }
            var lifted = series.Id == active;
            var target = lifted ? 0 : active is not null ? 0.38 : 1;
            if (full)
            {
                _ = value.AnimateTo(target, MotionSpec.Tween(lifted ? ArcMotionPresets.Instant : ArcMotionPresets.Standard, ArcEasings.Standard));
            }
            else
            {
                value.Jump(target);
            }
        }
        if (active is not null)
        {
            _lensId = active;
        }
        // The lifted ridge is a copy drawn above the rest: it fades in and rises 6px on the CSS spring.
        if (full)
        {
            _ = _lensOpacity.AnimateTo(active is null ? 0 : 1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            _ = _lensLift.AnimateTo(active is null ? 0 : 1, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
        }
        else
        {
            _lensOpacity.Jump(active is null ? 0 : 1);
            _lensLift.Jump(active is null ? 0 : 1);
        }
        SyncTooltip();
        UpdateAccessibility();
        InvalidateVisual();
    }

    private void SetReading(string? id, (double Value, double? PointerY)? cursor)
    {
        _cursor = id is null ? null : cursor;
        if (ActiveId != id)
        {
            ActiveId = id;
        }
        else
        {
            SyncTooltip();
            UpdateAccessibility();
            InvalidateVisual();
        }
    }

    private double ValueAt(double x) => _drawnLo + ChartsBMath.Clamp((x - Gutter) / PlotWidth, 0, 1) * (_drawnHi - _drawnLo);

    private double XOf(double value) => Gutter + (value - _drawnLo) / (_drawnHi - _drawnLo == 0 ? 1 : _drawnHi - _drawnLo) * PlotWidth;

    private (string? Id, double Value) Pick(Point point)
    {
        var value = ValueAt(point.X);
        var lift = RowHeight * Overlap;
        Shape? best = null;
        string? bestId = null;
        foreach (var (id, shape) in _drawn)
        {
            var baseline = Baseline(shape.Row);
            var height = shape.HeightAt(value) * lift;
            if (point.Y <= baseline + 2 && point.Y >= baseline - height - 4 && (best is null || shape.Row > best.Row))
            {
                best = shape;
                bestId = id;
            }
        }
        if (bestId is null && _series.Count > 0)
        {
            var row = (int)ChartsBMath.Clamp(Math.Floor((point.Y - Top) / RowHeight), 0, _series.Count - 1);
            bestId = _series[row].Id;
        }
        return (bestId, value);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Track(e, e.GetPosition(this));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Track(e, e.GetPosition(this));
    }

    private void Track(PointerEventArgs e, Point point)
    {
        if (IsEmpty)
        {
            return;
        }
        if (point.X < Gutter - 8 || point.Y > PlotBottom - AxisHeight + 4)
        {
            if (e.Pointer.Type == PointerType.Mouse)
            {
                SetReading(null, null);
            }
            return;
        }
        var (id, value) = Pick(point);
        SetReading(id, (value, point.Y));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            SetReading(null, null);
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        SetReading(null, null);
    }

    /// <summary>↑ / ↓ choose a ridge, ← / → move the reading by one tick step, Home / End first and last, Escape puts it down.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || IsEmpty)
        {
            return;
        }
        var at = ActiveId is { } id ? _series.FindIndex(s => s.Id == id) : -1;
        void Choose(int index)
        {
            e.Handled = true;
            var next = _series[(int)ChartsBMath.Clamp(index, 0, _series.Count - 1)];
            SetReading(next.Id, (_cursor?.Value ?? _stats[next.Id].Median, null));
        }
        void Nudge(int direction)
        {
            e.Handled = true;
            if (ActiveId is null)
            {
                Choose(0);
                return;
            }
            var step = TickStep;
            var basis = _cursor?.Value ?? _stats[ActiveId].Median;
            var next = ChartsBMath.Clamp((Math.Round(basis / step) + direction) * step, _lo, _hi);
            SetReading(ActiveId, (next, null));
        }
        switch (e.Key)
        {
            case Key.Down:
                Choose(at + 1);
                break;
            case Key.Up:
                Choose(at < 0 ? 0 : at - 1);
                break;
            case Key.Home:
                Choose(0);
                break;
            case Key.End:
                Choose(_series.Count - 1);
                break;
            case Key.Right:
                Nudge(1);
                break;
            case Key.Left:
                Nudge(-1);
                break;
            case Key.Escape when ActiveId is not null:
                e.Handled = true;
                SetReading(null, null);
                break;
        }
    }

    // ---- tooltip & accessibility --------------------------------------------------------------------------------

    private void SyncTooltip()
    {
        var series = ActiveId is { } id ? _series.FirstOrDefault(s => s.Id == id) : null;
        if (series is null || !_targets.TryGetValue(series.Id, out var shape) || Bounds.Width <= 0)
        {
            _tooltip.Hide();
            InvalidateVisual();
            return;
        }
        var stats = _stats[series.Id];
        var signature = $"{series.Id}|{_cursor?.Value}";
        if (signature != _tipSignature)
        {
            _tipSignature = signature;
            var p = _palette;
            var xs = p.TypeXS;
            var rows = new List<ChartsBTipRow>
            {
                new() { Left = ChartsBText.Make(series.Label, p, xs, p.TextSecondary), MinHeight = xs * 1.5 },
                new()
                {
                    MarginTop = 2, MinHeight = p.TypeSM * 1.4,
                    Left = ChartsBText.Make("Median", p, xs, p.TextMuted),
                    Right = ChartsBText.Make(Format(stats.Median), p, p.TypeSM, p.Foreground, FontWeight.Medium)
                },
                new()
                {
                    MinHeight = xs * 1.5,
                    Left = ChartsBText.Make("Middle half", p, xs, p.TextMuted),
                    Right = ChartsBText.Make($"{Format(stats.Q1)} to {Format(stats.Q3)}", p, xs, p.Foreground)
                },
                new()
                {
                    MinHeight = xs * 1.5,
                    Left = ChartsBText.Make("Range", p, xs, p.TextMuted),
                    Right = ChartsBText.Make($"{Format(stats.Min)} to {Format(stats.Max)}", p, xs, p.Foreground)
                }
            };
            if (_cursor is { } cursor)
            {
                rows.Add(new ChartsBTipRow
                {
                    MarginTop = 4, MinHeight = xs * 1.5,
                    Left = ChartsBText.Make($"{ChartsBMath.Percent(stats.FractionAtOrBelow(cursor.Value))} at or below {Format(cursor.Value)}", p, xs, p.TextMuted)
                });
            }
            _tooltip.SetRows(rows);
        }
        // The tooltip trails the pointer or the keyboard cursor and stays inside the chart.
        var width = Bounds.Width;
        var size = _tooltip.Size;
        var ax = _cursor is { } c ? XOf(c.Value) : XOf(shape.Median);
        var ay = _cursor?.PointerY ?? Baseline(shape.Row) - shape.HeightAt(shape.Median) * RowHeight * Overlap - Lift;
        var left = ax + TipGap;
        if (left + size.Width > width)
        {
            left = ax - TipGap - size.Width;
        }
        var y = ay - size.Height - TipGap;
        if (y < 0)
        {
            y = ay + TipGap;
        }
        _tooltip.Show(new Point(ChartsBMath.Clamp(left, 0, Math.Max(0, width - size.Width)), ChartsBMath.Clamp(y, 0, Math.Max(0, PlotBottom - size.Height))));
        InvalidateVisual();
    }

    private void UpdateAccessibility()
    {
        AutomationProperties.SetName(this, Label);
        _peer?.Announce();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new ChartsBAutomationPeer(this, this);

    string IChartsBAccessible.ChartRoleDescription => "ridgeline chart";

    string IChartsBAccessible.ChartName => $"{Label}. Up and down arrows choose a ridge, left and right arrows move along the values.";

    string IChartsBAccessible.ChartHelp => Reading ?? Summary;

    // ---- layout & drawing -------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 560 : availableSize.Width;
        if (IsEmpty)
        {
            return new Size(width, Math.Max(160, PlotBottom));
        }
        var scale = IsTinted ? 12 + Math.Ceiling(_palette.TypeXS * 1.5) : 0;
        return new Size(width, PlotBottom + scale);
    }

    private void EnsureText()
    {
        var p = _palette;
        if (_textVersion == p.Version)
        {
            return;
        }
        _textVersion = p.Version;
        _textWidth = Bounds.Width;
        _ticks.Clear();
        var step = TickStep;
        for (var value = Math.Ceiling(_lo / step) * step; value <= _hi + 1e-9; value += step)
        {
            var rounded = Math.Round(value * 1e6) / 1e6;
            _ticks.Add((rounded, ChartsBText.Make(Format(rounded == 0 ? 0 : rounded), p, p.TypeXS, p.TextMuted)));
        }
        _rowLabels.Clear();
        foreach (var series in _series)
        {
            _rowLabels[series.Id] = ChartsBText.Trimmed(series.Label, p, p.TypeXS, p.TextSecondary, Gutter - 10);
        }
        _empty = ChartsBText.Make(EmptyLabel, p, p.TypeSM, p.TextMuted);
        _scaleLabel = ChartsBText.Make("Median", p, p.TypeXS, p.TextSecondary);
        _scaleLo = ChartsBText.Make(Format(_lo), p, p.TypeXS, p.TextMuted);
        _scaleHi = ChartsBText.Make(Format(_hi), p, p.TypeXS, p.TextMuted);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        if (width <= 0)
        {
            return;
        }
        EnsureText();
        var p = _palette;
        if (IsEmpty)
        {
            context.DrawText(_empty!, new Point((width - _empty!.Width) / 2, (Bounds.Height - _empty.Height) / 2));
            return;
        }
        using var fade = context.PushOpacity(_reveal.Fade);
        var top = Top;
        var axisLabelBottom = PlotBottom - AxisHeight + 22;
        // Ticks: positions follow the drawn domain every frame.
        var tickPen = p.Pen(p.BorderSubtle, 1);
        foreach (var (value, text) in _ticks)
        {
            var x = Math.Round(XOf(value)) + 0.5;
            if (x < Gutter - 1 || x > width)
            {
                continue;
            }
            context.DrawLine(tickPen, new Point(x, top - RowHeight * 0.5), new Point(x, axisLabelBottom - text.Height - 2));
            context.DrawText(text, new Point(x - text.Width / 2, axisLabelBottom - text.Height));
        }
        var active = ActiveId;
        if (_reveal.IsRevealed)
        {
            var lift = RowHeight * Overlap;
            foreach (var series in _series)
            {
                if (!_drawn.TryGetValue(series.Id, out var shape))
                {
                    continue;
                }
                var opacity = _ridgeOpacity.TryGetValue(series.Id, out var o) ? o.Value : 1;
                if (opacity <= 0.002)
                {
                    continue;
                }
                var ridge = shape.Color;
                var animated = _morphing;
                using (context.PushOpacity(opacity))
                {
                    var baseline = Baseline(shape.Row);
                    if (!_geometry.TryGetValue(series.Id, out var cached) || cached.Stamp != _geometryStamp)
                    {
                        cached = (_geometryStamp, Curve(shape, baseline, lift, true), Curve(shape, baseline, lift, false));
                        _geometry[series.Id] = cached;
                    }
                    context.DrawGeometry(Brush(ChartsBMath.MixIn(ridge, 0.34, p.Surface), animated), null, cached.Fill);
                    var mx = XOf(shape.Median);
                    context.DrawLine(PenOf(ChartsBMath.MixIn(ridge, 0.55, p.Surface), 1, animated), new Point(mx, baseline),
                        new Point(mx, baseline - shape.HeightAt(shape.Median) * lift));
                    context.DrawGeometry(null, PenOf(ridge, 1.5, animated), cached.Line);
                }
            }
            DrawLens(context, lift);
            if (_cursor is { } cursor && active is not null)
            {
                var x = XOf(cursor.Value);
                using (context.PushOpacity(0.35))
                {
                    context.DrawLine(p.Pen(p.Foreground, 1), new Point(x, top - RowHeight * 0.5), new Point(x, PlotBottom - AxisHeight));
                }
            }
        }
        foreach (var series in _series)
        {
            if (!_rowLabels.TryGetValue(series.Id, out var label) || !_targets.TryGetValue(series.Id, out var target))
            {
                continue;
            }
            var on = series.Id == active;
            label.SetForegroundBrush(p.Brush(on ? p.Foreground : p.TextSecondary));
            label.SetFontWeight(on ? FontWeight.Medium : FontWeight.Normal);
            context.DrawText(label, new Point(0, Baseline(target.Row) - 18 + (18 - label.Height) / 2));
        }
        if (IsTinted)
        {
            DrawScale(context, PlotBottom + 12);
        }
        _tooltip.Draw(context, p);
    }

    private IBrush Brush(Color color, bool animated) => animated ? new ImmutableSolidColorBrush(color) : _palette.Brush(color);

    private IPen PenOf(Color color, double thickness, bool animated)
        => animated ? new ImmutablePen(new ImmutableSolidColorBrush(color), thickness, null, PenLineCap.Round, PenLineJoin.Round)
            : _palette.Pen(color, thickness, PenLineCap.Round);

    private void DrawLens(DrawingContext context, double lift)
    {
        var opacity = _lensOpacity.Value;
        if (opacity <= 0.002 || _lensId is null || !_drawn.TryGetValue(_lensId, out var shape))
        {
            return;
        }
        var p = _palette;
        var ridge = shape.Color;
        var baseline = Baseline(shape.Row);
        using (context.PushTransform(Matrix.CreateTranslation(0, -Lift * _lensLift.Value)))
        using (context.PushOpacity(opacity))
        {
            context.DrawGeometry(Brush(ChartsBMath.MixIn(ridge, 0.18, p.Surface), true), null, Curve(shape, baseline, lift, true));
            context.DrawGeometry(Brush(ChartsBMath.MixIn(ridge, 0.48, p.Surface), true), null, Curve(shape, baseline, lift, true, shape.Q1, shape.Q3));
            var mx = XOf(shape.Median);
            context.DrawLine(p.Pen(p.Foreground, 1.5, PenLineCap.Round), new Point(mx, baseline), new Point(mx, baseline - shape.HeightAt(shape.Median) * lift));
            context.DrawGeometry(null, PenOf(ridge, 2.25, true), Curve(shape, baseline, lift, false));
        }
    }

    /// <summary>The ridge outline (open) or its filled area (closed), optionally between two values (the middle half).</summary>
    private StreamGeometry Curve(Shape shape, double baseline, double lift, bool closed, double from = double.NegativeInfinity,
                                 double to = double.PositiveInfinity)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        var started = false;
        void Push(double x, double y)
        {
            var point = new Point(XOf(x), baseline - y * lift);
            if (!started)
            {
                if (closed)
                {
                    ctx.BeginFigure(new Point(point.X, baseline), true);
                    ctx.LineTo(point);
                }
                else
                {
                    ctx.BeginFigure(point, false);
                }
                started = true;
            }
            else
            {
                ctx.LineTo(point);
            }
        }
        var bounded = double.IsFinite(from);
        if (bounded)
        {
            Push(from, shape.HeightAt(from));
        }
        for (var i = 0; i < Samples; i++)
        {
            if (shape.Xs[i] > from && shape.Xs[i] < to)
            {
                Push(shape.Xs[i], shape.Ys[i]);
            }
        }
        if (double.IsFinite(to))
        {
            Push(to, shape.HeightAt(to));
        }
        if (started)
        {
            if (closed)
            {
                var last = double.IsFinite(to) ? to : shape.Xs[Samples - 1];
                ctx.LineTo(new Point(XOf(last), baseline));
            }
            ctx.EndFigure(closed);
        }
        return geometry;
    }

    /// <summary>The median scale: stepped swatches from light to full Series1, right aligned under the axis.</summary>
    private void DrawScale(DrawingContext context, double y)
    {
        var p = _palette;
        var height = Math.Ceiling(p.TypeXS * 1.5);
        var widths = new[] { _scaleLabel!.WidthIncludingTrailingWhitespace + 4, _scaleLo!.WidthIncludingTrailingWhitespace, 5 * 18 + 4 * 2.0, _scaleHi!.WidthIncludingTrailingWhitespace };
        var x = Bounds.Width - widths.Sum() - 3 * 8;
        context.DrawText(_scaleLabel, new Point(x, y + (height - _scaleLabel.Height) / 2));
        x += widths[0] + 8;
        context.DrawText(_scaleLo, new Point(x, y + (height - _scaleLo.Height) / 2));
        x += widths[1] + 8;
        for (var i = 0; i < 5; i++)
        {
            context.DrawRectangle(p.Brush(Tone(i * 0.25)), null, new RoundedRect(new Rect(x + i * 20, y + (height - 10) / 2, 18, 10), 3));
        }
        x += widths[2] + 8;
        context.DrawText(_scaleHi, new Point(x, y + (height - _scaleHi.Height) / 2));
    }
}
