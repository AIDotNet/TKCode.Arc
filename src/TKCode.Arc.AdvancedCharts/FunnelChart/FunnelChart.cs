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

namespace TKCode.Arc;

/// <summary>
/// A tapered conversion funnel with the drop-off at each step. Every step is a column with its name, count, retention
/// and drop-off; the band's thickness follows the count. <see cref="View"/> shows the total, a gapped split by segment,
/// or one segment rescaled to the full band, and the band morphs between them on a spring. Dots stream through the band
/// and leave at each step with that step's real drop-off. Below 420 pixels of width the funnel runs top to bottom.
/// </summary>
public partial class FunnelChart : Control, IChartsBAccessible, Avalonia.Rendering.ICustomHitTest
{
    /// <summary>The whole box takes the pointer, not only the painted marks.</summary>
    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    /// <summary>The steps from first to last. Inline in XAML, or assign any list; replace or change the list to update the chart.</summary>
    public static readonly DirectProperty<FunnelChart, IList<FunnelStage>> StagesProperty =
        AvaloniaProperty.RegisterDirect<FunnelChart, IList<FunnelStage>>(nameof(Stages), o => o.Stages, (o, v) => o.Stages = v);

    /// <summary>Optional parts of the audience; each stage then lists one count per segment in <see cref="FunnelStage.Values"/>.</summary>
    public static readonly DirectProperty<FunnelChart, IList<FunnelSegment>> SegmentsProperty =
        AvaloniaProperty.RegisterDirect<FunnelChart, IList<FunnelSegment>>(nameof(Segments), o => o.Segments, (o, v) => o.Segments = v);

    /// <summary>What the funnel measures, such as "Signups in March"; names the chart for assistive technology.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<FunnelChart, string?>(nameof(Label));

    /// <summary>Unit after each count, such as "users" (default: none).</summary>
    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<FunnelChart, string>(nameof(Unit), string.Empty);

    /// <summary><c>total</c> (default), <c>split</c> for a gapped band per segment, or a segment key that isolates and rescales that segment. Unknown names show the total.</summary>
    public static readonly StyledProperty<string> ViewProperty =
        AvaloniaProperty.Register<FunnelChart, string>(nameof(View), FunnelModel.TotalView);

    /// <summary>Streams dots through the band; they leave at each step with its real drop-off. Off under reduced motion (default true).</summary>
    public static readonly StyledProperty<bool> IsFlowEnabledProperty =
        AvaloniaProperty.Register<FunnelChart, bool>(nameof(IsFlowEnabled), true);

    /// <summary>Which way the funnel runs; <c>Auto</c> (default) goes top to bottom below 420 pixels of width.</summary>
    public static readonly StyledProperty<FunnelOrientation> OrientationProperty =
        AvaloniaProperty.Register<FunnelChart, FunnelOrientation>(nameof(Orientation));

    /// <summary>Thickness of the band at the first step when it runs left to right (default 200).</summary>
    public static readonly StyledProperty<double> BandHeightProperty =
        AvaloniaProperty.Register<FunnelChart, double>(nameof(BandHeight), 200);

    /// <summary>Formats counts (default: grouped, one decimal at most, then <see cref="Unit"/>).</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<FunnelChart, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>The step being read, or null (two-way). A number outside the stages clears it.</summary>
    public static readonly StyledProperty<int?> ActiveIndexProperty =
        AvaloniaProperty.Register<FunnelChart, int?>(nameof(ActiveIndex), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Message shown when the first step of the current view is zero or there are no stages (default "No data").</summary>
    public static readonly StyledProperty<string> EmptyLabelProperty =
        AvaloniaProperty.Register<FunnelChart, string>(nameof(EmptyLabel), "No data");

    internal static readonly StyledProperty<double> SplitGapProperty =
        AvaloniaProperty.Register<FunnelChart, double>(nameof(SplitGap), 6);

    internal static readonly StyledProperty<double> VerticalRowHeightProperty =
        AvaloniaProperty.Register<FunnelChart, double>(nameof(VerticalRowHeight), 100);

    internal static readonly StyledProperty<double> HeaderGapProperty =
        AvaloniaProperty.Register<FunnelChart, double>(nameof(HeaderGap), 10);

    internal static readonly StyledProperty<double> DotRadiusProperty =
        AvaloniaProperty.Register<FunnelChart, double>(nameof(DotRadius), 2.5);

    internal static readonly StyledProperty<double> HighlightRadiusProperty =
        AvaloniaProperty.Register<FunnelChart, double>(nameof(HighlightRadius), 12);

    /// <summary>Below this width <see cref="FunnelOrientation.Auto"/> runs top to bottom.</summary>
    internal const double VerticalBelowWidth = 420;

    private static readonly SpringSpec s_morphSpring = ChartsBMath.Physical(0.7, 0.08);
    private static readonly SpringSpec s_revealSpring = ChartsBMath.Physical(0.9, 0);
    private static readonly SpringSpec s_rollSpring = ChartsBMath.Physical(0.5, 0);

    private readonly ChartsBPalette _palette;
    private readonly ChartsBReveal _reveal;
    private readonly ChartsBTooltip _tooltip;
    private readonly ChartsBItemsWatcher _stageWatcher;
    private readonly ChartsBItemsWatcher _segmentWatcher;
    private readonly ChartSpringArray _band;
    private readonly MotionValue<double> _highlightAt;
    private readonly MotionValue<double> _highlightOpacity;
    private readonly MotionValue<double> _roll;
    private readonly FunnelFlowSim _sim = new();
    private readonly ChartsBOverlay _flow;
    private readonly ChartsBOverlay _overlay;
    private readonly LiveLoop _flowLoop;
    private IList<FunnelStage> _stages = new AvaloniaList<FunnelStage>();
    private IList<FunnelSegment> _segments = new AvaloniaList<FunnelSegment>();
    private FunnelModel _model = new([], []);
    private double[] _targets = [0];
    private ChartsBAutomationPeer? _peer;
    private string? _tipSignature;
    private Rect? _tipBox;
    private bool _touchDown;
    private bool _attached;
    private readonly bool _ready;

    /// <summary>Creates a funnel chart.</summary>
    public FunnelChart()
    {
        Focusable = false;
        _palette = new ChartsBPalette(this, OnPaletteChanged);
        _overlay = new ChartsBOverlay(context => _tooltip!.Draw(context, _palette));
        _flow = new ChartsBOverlay(DrawDots);
        _tooltip = new ChartsBTooltip(this, ArcMotionPresets.Snappy, _overlay.InvalidateVisual) { MinWidth = 180 };
        _reveal = new ChartsBReveal(this, 0.3, OnReveal);
        _band = new ChartSpringArray(this);
        _band.Changed += OnBandMoved;
        _highlightAt = Value(0);
        _highlightOpacity = Value(0);
        _roll = Value(1);
        // Owned by the chart: the dot surface is a visual child only, so it does not inherit the motion level.
        _flowLoop = new LiveLoop(this, StepFlow);
        _flowLoop.RunningChanged += _ => _flow.InvalidateVisual();
        _stageWatcher = new ChartsBItemsWatcher(Rebuild);
        _segmentWatcher = new ChartsBItemsWatcher(Rebuild);
        VisualChildren.Add(_flow);
        VisualChildren.Add(_overlay);
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        _ready = true;
        _stageWatcher.SetSource((IEnumerable)_stages);
        _segmentWatcher.SetSource((IEnumerable)_segments);
    }

    static FunnelChart()
    {
        AffectsMeasure<FunnelChart>(BandHeightProperty, OrientationProperty, VerticalRowHeightProperty, HeaderGapProperty);
        AffectsRender<FunnelChart>(HighlightRadiusProperty);
    }

    private MotionValue<double> Value(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    /// <inheritdoc cref="StagesProperty"/>
    public IList<FunnelStage> Stages
    {
        get => _stages;
        set
        {
            value ??= new AvaloniaList<FunnelStage>();
            if (SetAndRaise(StagesProperty, ref _stages, value))
            {
                _stageWatcher.SetSource((IEnumerable)value);
            }
        }
    }

    /// <inheritdoc cref="SegmentsProperty"/>
    public IList<FunnelSegment> Segments
    {
        get => _segments;
        set
        {
            value ??= new AvaloniaList<FunnelSegment>();
            if (SetAndRaise(SegmentsProperty, ref _segments, value))
            {
                _segmentWatcher.SetSource((IEnumerable)value);
            }
        }
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="UnitProperty"/>
    public string Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <inheritdoc cref="ViewProperty"/>
    public string View
    {
        get => GetValue(ViewProperty);
        set => SetValue(ViewProperty, value);
    }

    /// <inheritdoc cref="IsFlowEnabledProperty"/>
    public bool IsFlowEnabled
    {
        get => GetValue(IsFlowEnabledProperty);
        set => SetValue(IsFlowEnabledProperty, value);
    }

    /// <inheritdoc cref="OrientationProperty"/>
    public FunnelOrientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <inheritdoc cref="BandHeightProperty"/>
    public double BandHeight
    {
        get => GetValue(BandHeightProperty);
        set => SetValue(BandHeightProperty, value);
    }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, string>? ValueFormatter
    {
        get => GetValue(ValueFormatterProperty);
        set => SetValue(ValueFormatterProperty, value);
    }

    /// <inheritdoc cref="ActiveIndexProperty"/>
    public int? ActiveIndex
    {
        get => GetValue(ActiveIndexProperty);
        set => SetValue(ActiveIndexProperty, value);
    }

    /// <inheritdoc cref="EmptyLabelProperty"/>
    public string EmptyLabel
    {
        get => GetValue(EmptyLabelProperty);
        set => SetValue(EmptyLabelProperty, value);
    }

    /// <summary>Raised when the step being read changes.</summary>
    public event EventHandler<FunnelActiveIndexChangedEventArgs>? ActiveIndexChanged;

    internal double SplitGap
    {
        get => GetValue(SplitGapProperty);
        set => SetValue(SplitGapProperty, value);
    }

    internal double VerticalRowHeight
    {
        get => GetValue(VerticalRowHeightProperty);
        set => SetValue(VerticalRowHeightProperty, value);
    }

    internal double HeaderGap
    {
        get => GetValue(HeaderGapProperty);
        set => SetValue(HeaderGapProperty, value);
    }

    internal double DotRadius
    {
        get => GetValue(DotRadiusProperty);
        set => SetValue(DotRadiusProperty, value);
    }

    internal double HighlightRadius
    {
        get => GetValue(HighlightRadiusProperty);
        set => SetValue(HighlightRadiusProperty, value);
    }

    internal FunnelModel Model => _model;

    internal string ResolvedView => _model.ResolveView(View);

    internal bool IsEmptyView => _model.IsEmpty(ResolvedView);

    internal bool IsMorphing => _band.IsAnimating;

    internal bool IsFlowActive => _flowLoop.IsActive;

    internal bool IsFlowRunning => _flowLoop.IsRunning;

    internal Rect? TooltipBox => _tipBox;

    internal bool IsTooltipOpen => _tooltip.IsOpen;

    internal bool IsHighlightShown => _highlightOpacity.Target > 0.5;

    // ---- text -------------------------------------------------------------------------------------------------

    private string Format(double value)
    {
        if (ValueFormatter is { } formatter)
        {
            return formatter(value);
        }
        var text = ChartsBMath.Grouped(value);
        var unit = Unit;
        if (string.IsNullOrEmpty(unit))
        {
            return text;
        }
        return char.IsLetter(unit[0]) ? text + " " + unit : text + unit;
    }

    private static string StageName(FunnelStage stage) => string.IsNullOrEmpty(stage.Label) ? stage.Key : stage.Label;

    private string Describe(int index)
    {
        var view = ResolvedView;
        var count = Format(_model.CountAt(view, index));
        var previous = ChartsBMath.Percent(_model.FromPrevious(view, index), 1);
        var first = ChartsBMath.Percent(_model.FromFirst(view, index), 1);
        return $"Step {index + 1} of {_model.StageCount}, {StageName(_model.Stages[index])}: {count}, {previous} of the previous step, {first} of the first step";
    }

    /// <summary>Accessible summary: every step's count with both retention rates.</summary>
    public string Summary
    {
        get
        {
            if (IsEmptyView)
            {
                return $"{Label}. {EmptyLabel}.";
            }
            var steps = new string[_model.StageCount];
            for (var i = 0; i < steps.Length; i++)
            {
                steps[i] = Describe(i);
            }
            return $"{Label}. {string.Join("; ", steps)}.";
        }
    }

    /// <summary>The spoken reading of the active step, or null.</summary>
    internal string? Reading => ActiveIndex is { } index && index >= 0 && index < _model.StageCount && !IsEmptyView ? Describe(index) : null;

    // ---- data -------------------------------------------------------------------------------------------------

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ViewProperty)
        {
            ShowCurrentData(s_morphSpring);
        }
        else if (change.Property == IsFlowEnabledProperty)
        {
            UpdateFlow();
        }
        else if (change.Property == ActiveIndexProperty)
        {
            OnActiveIndexChanged(change.GetNewValue<int?>());
        }
        else if (change.Property == ValueFormatterProperty || change.Property == UnitProperty)
        {
            _tipSignature = null;
            SyncNumbers(roll: false);
            SyncTooltip();
            UpdateAccessibility();
            InvalidateVisual();
        }
        else if (change.Property == EmptyLabelProperty)
        {
            _textVersion = -1;
            InvalidateVisual();
        }
        else if (change.Property == LabelProperty)
        {
            UpdateAccessibility();
        }
        else if (change.Property == SplitGapProperty)
        {
            RefreshTargets();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _stageWatcher.Attach();
        _segmentWatcher.Attach();
        // Inline stages and changes made while detached were not observed.
        Rebuild();
        _reveal.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        _stageWatcher.Detach();
        _segmentWatcher.Detach();
        _reveal.Detach();
    }

    private void Rebuild()
    {
        if (!_ready)
        {
            return;
        }
        var stages = new List<FunnelStage>();
        foreach (var item in _stageWatcher.Snapshot())
        {
            if (item is FunnelStage stage)
            {
                stages.Add(stage);
            }
        }
        var segments = new List<FunnelSegment>();
        foreach (var item in _segmentWatcher.Snapshot())
        {
            if (item is FunnelSegment segment)
            {
                segments.Add(segment);
            }
        }
        var previous = _model;
        _model = new FunnelModel(stages, segments);
        ReshapeBand(previous);
        ShowCurrentData(s_morphSpring);
        InvalidateMeasure();
    }

    /// <summary>Moves the springs onto the new grid, keeping the position and speed of stages and segments that survive.</summary>
    private void ReshapeBand(FunnelModel previous)
    {
        var model = _model;
        _targets = new double[model.SpringLength];
        _band.Reshape(model.SpringLength, (oldValues, oldVelocities, values, velocities) =>
        {
            if (oldValues.Length == 0)
            {
                return;
            }
            var oldStages = previous.StageCount;
            for (var k = 0; k < model.LayerCount; k++)
            {
                var oldLayer = OldLayer(previous, model, k);
                if (oldLayer < 0)
                {
                    continue;
                }
                for (var i = 0; i < model.StageCount; i++)
                {
                    var oldStage = OldStage(previous, model, i);
                    if (oldStage < 0)
                    {
                        continue;
                    }
                    var from = FunnelModel.SpringIndex(oldLayer, oldStage, oldStages);
                    var to = FunnelModel.SpringIndex(k, i, model.StageCount);
                    values[to] = oldValues[from];
                    values[to + 1] = oldValues[from + 1];
                    velocities[to] = oldVelocities[from];
                    velocities[to + 1] = oldVelocities[from + 1];
                }
            }
            values[^1] = oldValues[^1];
            velocities[^1] = oldVelocities[^1];
        });
        EnsurePaths();
    }

    private static int OldLayer(FunnelModel previous, FunnelModel model, int layer)
    {
        if (model.Segments.Count == 0 || previous.Segments.Count == 0)
        {
            return layer == 0 && previous.LayerCount > 0 ? 0 : -1;
        }
        for (var k = 0; k < previous.Segments.Count; k++)
        {
            if (previous.Segments[k].Key == model.Segments[layer].Key)
            {
                return k;
            }
        }
        return -1;
    }

    private static int OldStage(FunnelModel previous, FunnelModel model, int stage)
    {
        for (var i = 0; i < previous.StageCount; i++)
        {
            if (previous.Stages[i].Key == model.Stages[stage].Key)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>The view or the numbers changed: the band morphs, the numbers roll, and everything that reads them follows.</summary>
    private void ShowCurrentData(SpringSpec spring)
    {
        if (!_ready)
        {
            return;
        }
        Focusable = !IsEmptyView;
        if (_attached && ActiveIndex is { } active && (IsEmptyView || active < 0 || active >= _model.StageCount))
        {
            SetCurrentValue(ActiveIndexProperty, null);
        }
        _model.FillTargets(_targets, View, GapFraction);
        if (_reveal.IsRevealed)
        {
            _band.AnimateTo(_targets, spring);
        }
        _tipSignature = null;
        SyncNumbers(roll: ArcMotion.IsFull(this) && IsLoaded && _reveal.IsRevealed);
        // An index set before the stages (XAML order) becomes valid here.
        SyncHighlight();
        SyncTooltip();
        UpdateFlow();
        UpdateAccessibility();
        InvalidateVisual();
    }

    /// <summary>The gap or the band's thickness changed: the targets follow without a morph (unless one is running).</summary>
    private void RefreshTargets()
    {
        _model.FillTargets(_targets, View, GapFraction);
        if (!_reveal.IsRevealed)
        {
            return;
        }
        if (_band.IsAnimating)
        {
            _band.AnimateTo(_targets, s_morphSpring);
        }
        else
        {
            _band.Jump(_targets);
        }
    }

    /// <summary>The first view: the band grows out of its centre line.</summary>
    private void OnReveal(bool animate)
    {
        _model.FillTargets(_targets, View, GapFraction);
        if (animate)
        {
            _band.AnimateTo(_targets, s_revealSpring);
        }
        else
        {
            _band.Jump(_targets);
        }
        UpdateFlow();
    }

    private void OnPaletteChanged()
    {
        if (!_ready)
        {
            return;
        }
        _textVersion = -1;
        _tipSignature = null;
        if (ActiveIndex is not null)
        {
            SyncTooltip();
        }
        InvalidateMeasure();
        InvalidateVisual();
        _flow.InvalidateVisual();
    }

    private void OnBandMoved()
    {
        UpdateGeometry();
        InvalidateVisual();
    }

    // ---- flow -------------------------------------------------------------------------------------------------

    private void UpdateFlow()
    {
        var view = ResolvedView;
        _sim.SetModel(_model, view);
        if (IsFlowEnabled && _reveal.IsRevealed && !IsEmptyView)
        {
            _sim.WarmUp();
            _flowLoop.Start();
        }
        else
        {
            _flowLoop.Stop();
        }
        _flow.InvalidateVisual();
    }

    private bool StepFlow(double seconds)
    {
        // A long pause (a hidden tab) continues the stream, it does not teleport every dot.
        _sim.Step(Math.Min(seconds, 0.05));
        _flow.InvalidateVisual();
        return true;
    }

    // ---- interaction ------------------------------------------------------------------------------------------

    private void OnActiveIndexChanged(int? index)
    {
        if (_attached && index is { } i && (IsEmptyView || i < 0 || i >= _model.StageCount))
        {
            SetCurrentValue(ActiveIndexProperty, null);
            return;
        }
        var valid = IsValidIndex(index);
        SyncHighlight();
        SyncTooltip();
        UpdateAccessibility();
        InvalidateVisual();
        var view = ResolvedView;
        ActiveIndexChanged?.Invoke(this, valid
            ? new FunnelActiveIndexChangedEventArgs(index, _model.Stages[index!.Value], _model.CountAt(view, index.Value),
                _model.FromPrevious(view, index.Value), _model.FromFirst(view, index.Value))
            : new FunnelActiveIndexChangedEventArgs(null, null, 0, 0, 0));
    }

    private bool IsValidIndex(int? index) => index is { } at && at >= 0 && at < _model.StageCount && !IsEmptyView;

    /// <summary>Glides the column highlight to the active step, or fades it out. The first show appears in place.</summary>
    private void SyncHighlight()
    {
        var index = ActiveIndex;
        var valid = IsValidIndex(index);
        var full = ArcMotion.IsFull(this) && IsLoaded;
        if (valid)
        {
            var wasShown = _highlightOpacity.Target > 0.001 || _highlightOpacity.Value > 0.001;
            if (full && wasShown)
            {
                _ = _highlightAt.AnimateTo(index!.Value, MotionSpec.Spring(ArcMotionPresets.Snappy));
            }
            else
            {
                _highlightAt.Jump(index!.Value);
            }
        }
        if (full)
        {
            _ = _highlightOpacity.AnimateTo(valid ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        else
        {
            _highlightOpacity.Jump(valid ? 1 : 0);
        }
    }

    private void SetActive(int? index)
    {
        if (ActiveIndex != index)
        {
            SetCurrentValue(ActiveIndexProperty, index);
        }
    }

    private int IndexAt(Point point)
    {
        var position = _vertical ? point.Y : point.X;
        var extent = _columnExtent <= 0 ? 1 : _columnExtent;
        return (int)ChartsBMath.Clamp(Math.Floor(position / extent), 0, Math.Max(0, _model.StageCount - 1));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        // Touch moves arrive only while the finger is down; a vertical drag then goes to the page's scroll gesture.
        if (e.Pointer.Type != PointerType.Touch || _touchDown)
        {
            Track(e.GetPosition(this));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Neither handled nor captured, so a surrounding ScrollViewer still sees the gesture.
        _touchDown = e.Pointer.Type == PointerType.Touch;
        Track(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _touchDown = false;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        // Still down but no longer ours: the page's scroll gesture took over, so the reading is over.
        if (_touchDown)
        {
            _touchDown = false;
            SetActive(null);
        }
    }

    private void Track(Point point)
    {
        if (IsEmptyView)
        {
            return;
        }
        SetActive(IndexAt(point));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            SetActive(null);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.NavigationMethod == NavigationMethod.Tab && ActiveIndex is null && !IsEmptyView)
        {
            SetActive(0);
        }
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        SetActive(null);
        InvalidateVisual();
    }

    /// <summary>Tab reads the first step; Right / Down next; Left / Up previous; Home / End first and last; Escape clears.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || IsEmptyView)
        {
            return;
        }
        var last = _model.StageCount - 1;
        switch (e.Key)
        {
            case Key.Right:
            case Key.Down:
                SetActive(ActiveIndex is { } next ? Math.Min(next + 1, last) : 0);
                e.Handled = true;
                break;
            case Key.Left:
            case Key.Up:
                SetActive(ActiveIndex is { } previous ? Math.Max(previous - 1, 0) : 0);
                e.Handled = true;
                break;
            case Key.Home:
                SetActive(0);
                e.Handled = true;
                break;
            case Key.End:
                SetActive(last);
                e.Handled = true;
                break;
            case Key.Escape when ActiveIndex is not null:
                SetActive(null);
                e.Handled = true;
                break;
        }
    }

    // ---- tooltip & accessibility --------------------------------------------------------------------------------

    private void SyncTooltip()
    {
        if (ActiveIndex is not { } index || index < 0 || index >= _model.StageCount || IsEmptyView || _size.Width <= 0)
        {
            _tipBox = null;
            _tooltip.Hide();
            return;
        }
        var view = ResolvedView;
        var signature = $"{index}|{view}|{_palette.Version}";
        if (signature != _tipSignature)
        {
            _tipSignature = signature;
            var p = _palette;
            var xs = p.TypeXS;
            var rows = new List<ChartsBTipRow>
            {
                new() { Left = ChartsBText.Make(StageName(_model.Stages[index]), p, xs, p.TextSecondary), MinHeight = xs * 1.5 },
                new()
                {
                    MarginTop = 2, MinHeight = p.TypeSM * 1.4,
                    Left = ChartsBText.Make("Count", p, xs, p.TextMuted),
                    Right = ChartsBText.Make(Format(_model.CountAt(view, index)), p, p.TypeSM, p.Foreground, FontWeight.Medium)
                },
                new()
                {
                    MinHeight = xs * 1.5,
                    Left = ChartsBText.Make("From previous", p, xs, p.TextMuted),
                    Right = ChartsBText.Make(ChartsBMath.Percent(_model.FromPrevious(view, index), 1), p, xs, p.Foreground)
                },
                new()
                {
                    MinHeight = xs * 1.5,
                    Left = ChartsBText.Make("From first", p, xs, p.TextMuted),
                    Right = ChartsBText.Make(ChartsBMath.Percent(_model.FromFirst(view, index), 1), p, xs, p.Foreground)
                }
            };
            if (index > 0)
            {
                rows.Add(new ChartsBTipRow
                {
                    MinHeight = xs * 1.5,
                    Left = ChartsBText.Make("Dropped", p, xs, p.TextMuted),
                    Right = ChartsBText.Make("↓ " + Format(_model.DropOff(view, index)), p, xs, p.Foreground)
                });
            }
            if (view == FunnelModel.SplitView)
            {
                for (var k = 0; k < _model.Segments.Count; k++)
                {
                    rows.Add(new ChartsBTipRow
                    {
                        MarginTop = k == 0 ? 4 : 0, MinHeight = xs * 1.5, Swatch = p.Series(k % 4),
                        Left = ChartsBText.Make(_model.Segments[k].Label, p, xs, p.TextMuted),
                        Right = ChartsBText.Make(Format(_model.LayerCountAt(k, index)), p, xs, p.Foreground)
                    });
                }
            }
            _tooltip.SetRows(rows);
        }
        var box = PlaceTooltip(index, _tooltip.Size);
        _tipBox = box;
        _tooltip.Show(box.Position);
    }

    /// <summary>Beside the column, flipped to the other side near an edge, and always inside the chart.</summary>
    private Rect PlaceTooltip(int index, Size size)
    {
        const double gap = 10;
        var column = ColumnRect(index);
        double x;
        double y;
        if (_vertical)
        {
            x = 0;
            y = column.Top - size.Height - gap;
            if (y < 0)
            {
                y = column.Bottom + gap;
            }
        }
        else
        {
            x = column.Right + gap;
            if (x + size.Width > _size.Width)
            {
                x = column.Left - gap - size.Width;
            }
            y = _bandCenter - size.Height / 2;
        }
        return new Rect(
            ChartsBMath.Clamp(x, 0, Math.Max(0, _size.Width - size.Width)),
            ChartsBMath.Clamp(y, 0, Math.Max(0, _size.Height - size.Height)),
            size.Width, size.Height);
    }

    private void UpdateAccessibility()
    {
        if (!_ready)
        {
            return;
        }
        AutomationProperties.SetName(this, Reading ?? Label);
        _peer?.Announce();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new ChartsBAutomationPeer(this, this);

    string IChartsBAccessible.ChartRoleDescription => "funnel chart";

    string IChartsBAccessible.ChartName => $"{Label}. Arrow keys move between the steps.";

    string IChartsBAccessible.ChartHelp => Reading ?? Summary;
}
