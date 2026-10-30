using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>How a budget change reaches the screen.</summary>
internal enum UsageBudgetMotion
{
    /// <summary>At once.</summary>
    Jump,

    /// <summary>A key or a binding: the snappy spring.</summary>
    Key,

    /// <summary>A drag: a stiff spring that only smooths the snapping.</summary>
    Drag,

    /// <summary>A metric switch: the same spring as the paths.</summary>
    Metric
}

/// <summary>Everything the plot needs to draw one metric.</summary>
internal sealed class UsagePlotData
{
    public UsageSeries Series { get; set; } = new(30, 0, default);

    public double AxisMin { get; set; }

    public double AxisMax { get; set; } = 1;

    public double BudgetMin { get; set; }

    public double BudgetMax { get; set; } = 1;

    public double BudgetStep { get; set; } = 1;

    /// <summary>The six tick labels, bottom first.</summary>
    public string[] TickTexts { get; set; } = [];

    public Func<int, string> DayText { get; set; } = d => d.ToString();

    public Func<double, string> ValueText { get; set; } = v => v.ToString("0.0");
}

/// <summary>The words of the inspect card.</summary>
internal readonly record struct UsagePanelContent(string Date, string Tag, string Value, string Detail);

/// <summary>
/// The chart of a <see cref="UsageForecast"/>, one self-drawn control: the grid and its labels, the measured line, the
/// dashed forecast, the likely-range cone, today, the budget line with its capsule, the alert ticks, the crossing marker
/// with its date pill and the inspect crosshair with its card. Lines are straight polylines (one vertex a day).
/// </summary>
/// <remarks>
/// Code-driven motion: every vertex of the line and of both cone edges has its own spring (the metric switch morphs them
/// all at once), the budget line has one, and the crossing marker and the alert ticks chase the intersection of the
/// displayed budget with the displayed line, so the springs have to advance in one order inside one frame loop: budget and
/// vertices first, then the markers that read them. A ControlTheme cannot express per-vertex springs or a spring that
/// follows another spring, so one <see cref="LiveLoop"/> advances them all.
/// </remarks>
internal sealed partial class UsageForecastPlot : Control
{
    private const double FadeFast = 0.16;

    #region Geometry properties (from the token)

    public static readonly StyledProperty<Thickness> PlotInsetsProperty =
        AvaloniaProperty.Register<UsageForecastPlot, Thickness>(nameof(PlotInsets), new Thickness(52, 30, 10, 30));
    public static readonly StyledProperty<double> ChartHeightProperty =
        AvaloniaProperty.Register<UsageForecastPlot, double>(nameof(ChartHeight), 264);
    public static readonly StyledProperty<double> AxisLabelWidthProperty =
        AvaloniaProperty.Register<UsageForecastPlot, double>(nameof(AxisLabelWidth), 42);
    public static readonly StyledProperty<double> HandleCapsuleHeightProperty =
        AvaloniaProperty.Register<UsageForecastPlot, double>(nameof(HandleCapsuleHeight), 26);
    public static readonly StyledProperty<double> HandleCapsuleInsetProperty =
        AvaloniaProperty.Register<UsageForecastPlot, double>(nameof(HandleCapsuleInset), 8);
    public static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<UsageForecastPlot, double>(nameof(PanelWidth), 176);
    public static readonly StyledProperty<double> PanelOffsetProperty =
        AvaloniaProperty.Register<UsageForecastPlot, double>(nameof(PanelOffset), 14);
    public static readonly StyledProperty<double> PanelRadiusProperty =
        AvaloniaProperty.Register<UsageForecastPlot, double>(nameof(PanelRadius), 14);
    public static readonly StyledProperty<double> CrossingRadiusProperty =
        AvaloniaProperty.Register<UsageForecastPlot, double>(nameof(CrossingRadius), 5);
    public static readonly StyledProperty<double> TodayDotRadiusProperty =
        AvaloniaProperty.Register<UsageForecastPlot, double>(nameof(TodayDotRadius), 4);
    public static readonly StyledProperty<double> ThresholdTickLengthProperty =
        AvaloniaProperty.Register<UsageForecastPlot, double>(nameof(ThresholdTickLength), 14);

    public Thickness PlotInsets { get => GetValue(PlotInsetsProperty); set => SetValue(PlotInsetsProperty, value); }
    public double ChartHeight { get => GetValue(ChartHeightProperty); set => SetValue(ChartHeightProperty, value); }
    public double AxisLabelWidth { get => GetValue(AxisLabelWidthProperty); set => SetValue(AxisLabelWidthProperty, value); }
    public double HandleCapsuleHeight { get => GetValue(HandleCapsuleHeightProperty); set => SetValue(HandleCapsuleHeightProperty, value); }
    public double HandleCapsuleInset { get => GetValue(HandleCapsuleInsetProperty); set => SetValue(HandleCapsuleInsetProperty, value); }
    public double PanelWidth { get => GetValue(PanelWidthProperty); set => SetValue(PanelWidthProperty, value); }
    public double PanelOffset { get => GetValue(PanelOffsetProperty); set => SetValue(PanelOffsetProperty, value); }
    public double PanelRadius { get => GetValue(PanelRadiusProperty); set => SetValue(PanelRadiusProperty, value); }
    public double CrossingRadius { get => GetValue(CrossingRadiusProperty); set => SetValue(CrossingRadiusProperty, value); }
    public double TodayDotRadius { get => GetValue(TodayDotRadiusProperty); set => SetValue(TodayDotRadiusProperty, value); }
    public double ThresholdTickLength { get => GetValue(ThresholdTickLengthProperty); set => SetValue(ThresholdTickLengthProperty, value); }

    #endregion

    private static readonly SpringSpec s_budgetDrag = SpringSpec.FromPhysical(700, 53);
    private static readonly SpringSpec s_panelFollow = SpringSpec.FromPhysical(600, 50);

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(512);
    private readonly LiveLoop _loop;

    private UsagePlotData _data = new();
    private UsageSeries Series => _data.Series;
    private AnalyticsSpringChannel[] _y = [];
    private AnalyticsSpringChannel _budgetY = new(0, 0.01, 0.05);
    private double _budget;
    private double _overshoot;
    private AnalyticsSpringChannel _crossX = new(0, 0.001, 0.01);
    private AnalyticsTween _crossAlpha = new(0);
    private bool _crossVisible;
    private string? _crossText;
    private double[] _thresholdPercents = [];
    private bool[] _thresholdEnabled = [];
    private string[] _thresholdTexts = [];
    private AnalyticsSpringChannel[] _thX = [];
    private AnalyticsTween[] _thWipe = [];
    private AnalyticsTween[] _thLabel = [];
    private bool[] _thShown = [];

    // The six y labels cross-fade in place.
    private string[] _tickNew = [];
    private string[] _tickOld = [];
    private AnalyticsTween[] _tickNewAlpha = [];
    private AnalyticsTween[] _tickOldAlpha = [];

    private int _inspectDay;
    private bool _inspectByKey;
    private bool _pointerInspect;
    private AnalyticsSpringChannel _panelX = new(0, 0.05, 0.5);
    private AnalyticsTween _panelAlpha = new(0);
    private UsagePanelContent _panel;
    private AnalyticsTween _handleHover = new(0);
    private bool _handleActive;
    private bool _dragging;

    private StreamGeometry? _actualGeo;
    private StreamGeometry? _forecastGeo;
    private StreamGeometry? _coneGeo;
    private bool _geoDirty = true;
    private Point[] _pa = [];
    private Point[] _pf = [];
    private Point[] _pu = [];
    private Point[] _pl = [];
    private int _paletteVersion = -1;
    private double _lastW;

    static UsageForecastPlot()
    {
        FocusableProperty.OverrideDefaultValue<UsageForecastPlot>(true);
        AffectsMeasure<UsageForecastPlot>(ChartHeightProperty);
        AffectsRender<UsageForecastPlot>(PlotInsetsProperty, AxisLabelWidthProperty, HandleCapsuleHeightProperty, HandleCapsuleInsetProperty,
            PanelWidthProperty, PanelOffsetProperty, PanelRadiusProperty, CrossingRadiusProperty, TodayDotRadiusProperty,
            ThresholdTickLengthProperty);
    }

    public UsageForecastPlot()
    {
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        ActualThemeVariantChanged += (_, _) => InvalidatePalette();
        ResourcesChanged += (_, _) => InvalidatePalette();
        Cursor = new Avalonia.Input.Cursor(StandardCursorType.Cross);
    }

    /// <summary>Raised when the pointer or a key asks for another inspected day (null: none); the block decides.</summary>
    public event Action<int?, bool>? InspectRequested;

    /// <summary>Raised when a drag or a key changes the budget: the new value and whether it is final.</summary>
    public event Action<double, bool>? BudgetEdited;

    /// <summary>The handle over the budget band (set by the chart panel).</summary>
    public UsageBudgetHandle? Handle { get; set; }

    /// <summary>Supplies the words of the card for a day.</summary>
    public Func<int, UsagePanelContent>? PanelProvider { get; set; }

    /// <summary>The current data.</summary>
    public UsagePlotData Data => _data;

    /// <summary>The budget the plot shows or heads for.</summary>
    public double Budget => _budget;

    /// <summary>The day under the crosshair, 0 for none.</summary>
    public int InspectDay => _inspectDay;

    /// <summary>The day the keys act from: the crosshair or today.</summary>
    public int KeyDay => _inspectDay > 0 ? _inspectDay : Math.Max(1, Series.Today);

    /// <summary>True while the budget line is dragged.</summary>
    public bool IsDragging => _dragging;

    /// <summary>True while anything animates (tests).</summary>
    internal bool IsMoving => _loop.IsActive;

    /// <summary>The y of the budget line now (tests).</summary>
    internal double DisplayedBudgetY => _budgetY.Value;

    /// <summary>The y the budget line heads for (tests).</summary>
    internal double TargetBudgetY => _budgetY.Target;

    /// <summary>The day the crossing marker shows now (tests).</summary>
    internal double DisplayedCrossingX => _crossX.Value;

    /// <summary>Whether the crossing marker is visible (tests).</summary>
    internal bool IsCrossingVisible => _crossVisible;

    /// <summary>The opacity of the crossing marker and its pill (tests).</summary>
    internal double CrossingAlpha => _crossAlpha.Value;

    /// <summary>The opacity of the inspect card (tests).</summary>
    internal double PanelAlpha => _panelAlpha.Value;

    /// <summary>The card's content (tests).</summary>
    internal UsagePanelContent PanelContent => _panel;

    /// <summary>The displayed y of a vertex (tests); band 0 line, 1 upper edge, 2 lower edge.</summary>
    internal double DisplayedY(int band, int day) => _y.Length == 0 ? double.NaN : _y[band * (Series.Days + 1) + day].Value;

    /// <summary>How much of a threshold's tick is drawn (tests).</summary>
    internal double ThresholdWipe(int index) => index < _thWipe.Length ? _thWipe[index].Value : 0;

    /// <summary>The day a threshold's tick sits at now (tests).</summary>
    internal double ThresholdX(int index) => index < _thX.Length ? _thX[index].Value : 0;

    /// <summary>The label of tick slot <paramref name="index"/> that is fading in (tests).</summary>
    internal string TickText(int index) => index < _tickNew.Length ? _tickNew[index] : string.Empty;

    /// <summary>The opacity of the outgoing label of a tick slot (tests).</summary>
    internal double OutgoingTickAlpha(int index) => index < _tickOldAlpha.Length ? _tickOldAlpha[index].Value : 0;

    /// <summary>The pixel scale of the budget: pixels per unit.</summary>
    public double PixelsPerUnit => (Bottom - Top) / Math.Max(1e-9, _data.AxisMax - _data.AxisMin);

    #region Geometry

    private double Left => PlotInsets.Left;

    private double Right => Math.Max(Left + 1, Bounds.Width - PlotInsets.Right);

    private double Top => PlotInsets.Top;

    private double Bottom => Math.Max(Top + 1, Bounds.Height - PlotInsets.Bottom);

    /// <summary>The x of day <paramref name="day"/> (0 at the left edge of the plot).</summary>
    public double X(double day) => Left + day * (Right - Left) / Math.Max(1, Series.Days);

    /// <summary>The y of a value on the fixed axis.</summary>
    public double Y(double value) => Bottom - (value - _data.AxisMin) / Math.Max(1e-12, _data.AxisMax - _data.AxisMin) * (Bottom - Top);

    private double ValueOfY(double y) => _data.AxisMin + (Bottom - y) / (Bottom - Top) * (_data.AxisMax - _data.AxisMin);

    private double BudgetTargetY()
    {
        var y = Y(Math.Clamp(_budget, _data.BudgetMin, Math.Max(_data.BudgetMin, _data.BudgetMax)));
        if (_overshoot > 0)
        {
            y -= UsageForecastEngine.Rubber(_overshoot);
        }
        else if (_overshoot < 0)
        {
            y += UsageForecastEngine.Rubber(-_overshoot);
        }
        return y;
    }

    #endregion

    #region Data

    /// <summary>Shows another metric (morph at Full motion) or the first one.</summary>
    public void SetData(UsagePlotData data, bool animate, double budget)
    {
        var old = _data;
        _budget = budget;
        _overshoot = 0;
        _data = data;
        var n = data.Series.Days;
        var count = 3 * (n + 1);
        var full = animate && ArcMotion.IsFull(this) && this.IsAttachedToVisualTree();
        var sameShape = _y.Length == count && old.Series.Days == n && Bounds.Width > 0;
        if (_y.Length != count)
        {
            _y = new AnalyticsSpringChannel[count];
            for (var i = 0; i < count; i++)
            {
                _y[i] = new AnalyticsSpringChannel(0, 0.02, 0.1);
            }
                sameShape = false;
        }
        _pa = new Point[data.Series.Today + 1];
        _pf = new Point[n - data.Series.Today + 1];
        _pu = new Point[_pf.Length];
        _pl = new Point[_pf.Length];
        UpdateTicks(data.TickTexts, full && sameShape);
        _geoDirty = true;
        if (Bounds.Width <= 0)
        {
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }
        Retarget(full && sameShape, UsageBudgetMotion.Metric);
    }

    private void UpdateTicks(string[] texts, bool animate)
    {
        if (_tickNew.Length != texts.Length)
        {
            _tickNew = (string[])texts.Clone();
            _tickOld = new string[texts.Length];
            _tickNewAlpha = new AnalyticsTween[texts.Length];
            _tickOldAlpha = new AnalyticsTween[texts.Length];
            for (var i = 0; i < texts.Length; i++)
            {
                _tickNewAlpha[i] = new AnalyticsTween(1);
                _tickOldAlpha[i] = new AnalyticsTween(0);
                _tickOld[i] = string.Empty;
            }
            return;
        }
        for (var i = 0; i < texts.Length; i++)
        {
            if (_tickNew[i] == texts[i])
            {
                continue;
            }
            if (animate)
            {
                // Old text leaves in 160ms while the new one arrives in 240ms (the measured cross-fade).
                _tickOld[i] = _tickNew[i];
                _tickOldAlpha[i].Jump(_tickNewAlpha[i].Value);
                _tickOldAlpha[i].Start(0, ArcMotionPresets.Fast, AnalyticsEasings.EaseOut);
                _tickNew[i] = texts[i];
                _tickNewAlpha[i].Jump(0);
                _tickNewAlpha[i].Start(1, ArcMotionPresets.Standard, AnalyticsEasings.Enter);
            }
            else
            {
                _tickNew[i] = texts[i];
                _tickNewAlpha[i].Jump(1);
                _tickOldAlpha[i].Jump(0);
            }
        }
    }

    /// <summary>Sets the budget; moves the line with <paramref name="motion"/>.</summary>
    public void SetBudget(double value, UsageBudgetMotion motion)
    {
        _budget = value;
        if (Bounds.Width <= 0 || _y.Length == 0)
        {
            return;
        }
        MoveBudget(motion);
    }

    private void MoveBudget(UsageBudgetMotion motion)
    {
        var target = BudgetTargetY();
        var full = ArcMotion.IsFull(this) && this.IsAttachedToVisualTree();
        if (!full || motion == UsageBudgetMotion.Jump)
        {
            _budgetY.Jump(target);
            SnapMarkers();
        }
        else
        {
            var spec = motion switch
            {
                UsageBudgetMotion.Drag => s_budgetDrag,
                UsageBudgetMotion.Metric => AnalyticsSprings.Smooth,
                _ => AnalyticsSprings.Snappy
            };
            _budgetY.SpringTo(target, spec);
            _loop.Start();
        }
        PlaceHandle();
        InvalidateVisual();
    }

    /// <summary>Sets what the alert ticks mark: percents, which are on, and their labels.</summary>
    public void SetThresholds(double[] percents, bool[] enabled, string[] texts)
    {
        var changed = _thresholdPercents.Length != percents.Length;
        _thresholdPercents = percents;
        _thresholdEnabled = enabled;
        _thresholdTexts = texts;
        if (changed)
        {
            _thX = new AnalyticsSpringChannel[percents.Length];
            _thWipe = new AnalyticsTween[percents.Length];
            _thLabel = new AnalyticsTween[percents.Length];
            _thShown = new bool[percents.Length];
            for (var i = 0; i < percents.Length; i++)
            {
                _thX[i] = new AnalyticsSpringChannel(0, 0.001, 0.01);
            }
        }
        if (Bounds.Width > 0 && _y.Length > 0)
        {
            UpdateMarkers(snap: changed);
            Kick();
        }
        InvalidateVisual();
    }

    /// <summary>The text of the crossing pill (the day the budget is reached).</summary>
    public void SetCrossingText(string? text)
    {
        _crossText = text ?? _crossText;
        InvalidateVisual();
    }

    /// <summary>Shows the crosshair and card at <paramref name="day"/> (0 hides them).</summary>
    public void SetInspect(int day)
    {
        var full = ArcMotion.GetEffectiveLevel(this);
        var n = Series.Days;
        day = day <= 0 ? 0 : Math.Clamp(day, 1, n);
        var wasShown = _inspectDay > 0;
        _inspectDay = day;
        if (day == 0)
        {
            _inspectByKey = false;
            _pointerInspect = false;
            if (full == ArcMotionLevel.Off)
            {
                _panelAlpha.Jump(0);
            }
            else
            {
                _panelAlpha.Start(0, ArcMotionPresets.Fast, AnalyticsEasings.EaseOut);
            }
        }
        else
        {
            _panel = PanelProvider?.Invoke(day) ?? default;
            var target = PanelTargetX(day);
            if (!wasShown && _panelAlpha.Value < 0.01)
            {
                _panelX.Jump(target);
            }
            else if (full == ArcMotionLevel.Full)
            {
                _panelX.SpringTo(target, s_panelFollow);
            }
            else
            {
                _panelX.Jump(target);
            }
            if (full == ArcMotionLevel.Off)
            {
                _panelAlpha.Jump(1);
            }
            else
            {
                _panelAlpha.Start(1, ArcMotionPresets.Fast, AnalyticsEasings.Enter);
            }
        }
        Kick();
        InvalidateVisual();
    }

    /// <summary>Redraws the card's words (the data or the texts changed).</summary>
    public void RefreshPanel()
    {
        if (_inspectDay > 0)
        {
            _panel = PanelProvider?.Invoke(_inspectDay) ?? default;
            InvalidateVisual();
        }
    }

    private double PanelTargetX(int day)
    {
        var x = X(day);
        var right = x + PanelOffset;
        return right + PanelWidth > Bounds.Width ? x - PanelOffset - PanelWidth : right;
    }

    internal void SetHandleHover(bool hovering, bool dragging)
    {
        _dragging = dragging;
        var active = hovering || dragging;
        if (active == _handleActive)
        {
            return;
        }
        _handleActive = active;
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _handleHover.Jump(active ? 1 : 0);
        }
        else
        {
            _handleHover.Start(active ? 1 : 0, ArcMotionPresets.Fast, AnalyticsEasings.Standard);
        }
        Kick();
        InvalidateVisual();
    }

    /// <summary>Called by the handle while it is dragged: the value (snapped, in range) and the pixels beyond the range.</summary>
    internal void DragTo(double value, double overshootPixels)
    {
        _overshoot = overshootPixels;
        BudgetEdited?.Invoke(value, false);
        if (Math.Abs(value - _budget) < 1e-9)
        {
            // Same value: only the squeeze changed.
            MoveBudget(UsageBudgetMotion.Drag);
        }
    }

    /// <summary>Called by the handle when the drag or key ends.</summary>
    internal void CommitBudget(double value)
    {
        _overshoot = 0;
        BudgetEdited?.Invoke(value, true);
        MoveBudget(UsageBudgetMotion.Key);
    }

    private void PlaceHandle()
    {
        Handle?.Place(Math.Round(_budgetY.Value));
    }

    #endregion

    #region Targets and markers

    private void Retarget(bool animate, UsageBudgetMotion budgetMotion)
    {
        var n = Series.Days;
        var stride = n + 1;
        for (var d = 0; d <= n; d++)
        {
            Set(d, Y(Series.Line[d]));
            Set(stride + d, Y(Series.Upper[d]));
            Set(2 * stride + d, Y(Series.Lower[d]));
        }

        void Set(int index, double target)
        {
            if (animate)
            {
                _y[index].SpringTo(target, AnalyticsSprings.Smooth);
            }
            else
            {
                _y[index].Jump(target);
            }
        }

        _geoDirty = true;
        MoveBudget(animate ? budgetMotion : UsageBudgetMotion.Jump);
        if (!animate)
        {
            SnapMarkers();
        }
        if (_inspectDay > 0)
        {
            RefreshPanel();
            _panelX.Jump(PanelTargetX(_inspectDay));
        }
        Kick();
        InvalidateVisual();
    }

    private void SnapMarkers()
    {
        UpdateMarkers(snap: true);
    }

    private double? PixelCross(int band, double levelY)
    {
        var n = Series.Days;
        var o = band * (n + 1);
        if (_y.Length < o + n + 1)
        {
            return null;
        }
        if (_y[o].Value <= levelY)
        {
            return 0;
        }
        for (var d = 1; d <= n; d++)
        {
            var b = _y[o + d].Value;
            if (b <= levelY)
            {
                var a = _y[o + d - 1].Value;
                return d - 1 + (a - levelY) / Math.Max(1e-9, a - b);
            }
        }
        return null;
    }

    /// <summary>The displayed line's y at the fractional day <paramref name="x"/>.</summary>
    private double LineYAt(double x)
    {
        var n = Series.Days;
        if (_y.Length < n + 1)
        {
            return 0;
        }
        x = Math.Clamp(x, 0, n);
        var i = (int)Math.Floor(x);
        if (i >= n)
        {
            return _y[n].Value;
        }
        var t = x - i;
        return _y[i].Value + (_y[i + 1].Value - _y[i].Value) * t;
    }

    /// <summary>
    /// Points the crossing marker and the alert ticks at the intersections of the displayed budget with the displayed line.
    /// Returns true while any of them still has to move.
    /// </summary>
    private bool UpdateMarkers(bool snap)
    {
        if (_y.Length == 0 || Bounds.Width <= 0)
        {
            return false;
        }
        var full = ArcMotion.IsFull(this) && !snap;
        var moving = false;
        var by = _budgetY.Value;
        var cross = PixelCross(0, by);
        var wantVisible = cross is not null;
        var targetX = cross ?? Series.Days;
        moving |= Aim(ref _crossX, targetX, full);
        if (wantVisible != _crossVisible || snap)
        {
            _crossVisible = wantVisible;
            if (snap || !full)
            {
                _crossAlpha.Jump(wantVisible ? 1 : 0);
            }
            else
            {
                // Measured: the pill fades out over about 400ms (halving every 33ms).
                _crossAlpha.Start(wantVisible ? 1 : 0, TimeSpan.FromMilliseconds(400), AnalyticsEasings.EaseOut);
                moving = true;
            }
        }
        var budgetValue = ValueOfY(by);
        for (var k = 0; k < _thresholdPercents.Length; k++)
        {
            var levelY = Y(_thresholdPercents[k] / 100 * budgetValue);
            var x = PixelCross(0, levelY);
            var inside = levelY >= Top - 0.5 && levelY <= Bottom + 0.5;
            var shown = x is > 0.001 && inside && _thresholdEnabled.Length > k && _thresholdEnabled[k];
            if (x is { } tx)
            {
                if (snap || !_thShown[k] && _thWipe[k].Value <= 0.001)
                {
                    _thX[k].Jump(tx);
                }
                else
                {
                    moving |= Aim(ref _thX[k], tx, full);
                }
            }
            if (shown != _thShown[k] || snap)
            {
                _thShown[k] = shown;
                if (snap || !full)
                {
                    _thWipe[k].Jump(shown ? 1 : 0);
                    _thLabel[k].Jump(shown ? 1 : 0);
                }
                else
                {
                    // Measured: the tick draws itself bottom up in 700ms and is wiped the same way; its label fades in
                    // after 80ms (160ms) and out at once (135ms).
                    _thWipe[k].Start(shown ? 1 : 0, TimeSpan.FromMilliseconds(700), AnalyticsEasings.CssSpring);
                    _thLabel[k].Start(shown ? 1 : 0, TimeSpan.FromMilliseconds(shown ? 160 : 135), AnalyticsEasings.EaseOut,
                        shown ? TimeSpan.FromMilliseconds(80) : default);
                    moving = true;
                }
            }
        }
        return moving;
    }

    private static bool Aim(ref AnalyticsSpringChannel channel, double target, bool animate)
    {
        if (Math.Abs(channel.Target - target) > 1e-6)
        {
            if (animate)
            {
                channel.SpringTo(target, AnalyticsSprings.Smooth);
            }
            else
            {
                channel.Jump(target);
            }
        }
        return channel.IsMoving;
    }

    #endregion

    #region Frame loop

    /// <summary>
    /// Runs the frame loop while something animates. With motion off every change has jumped already: one step here
    /// settles what is left, so the loop does not start.
    /// </summary>
    private void Kick()
    {
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off || Advance(0))
        {
            _loop.Start();
        }
    }

    private bool Advance(double dt)
    {
        var full = ArcMotion.IsFull(this);
        var moving = false;
        var geometry = false;
        for (var i = 0; i < _y.Length; i++)
        {
            ref var c = ref _y[i];
            if (!c.IsMoving)
            {
                continue;
            }
            if (full)
            {
                c.Step(dt);
            }
            else
            {
                c.Jump(c.Target);
            }
            geometry = true;
            moving |= c.IsMoving;
        }
        if (_budgetY.IsMoving)
        {
            if (full)
            {
                _budgetY.Step(dt);
            }
            else
            {
                _budgetY.Jump(_budgetY.Target);
            }
            geometry = true;
            moving |= _budgetY.IsMoving;
            PlaceHandle();
        }

        // The markers read the line and the budget as they are now, so they come after them.
        moving |= UpdateMarkers(snap: false);
        moving |= Step(ref _crossX, dt, full);
        for (var k = 0; k < _thX.Length; k++)
        {
            moving |= Step(ref _thX[k], dt, full);
            moving |= _thWipe[k].Advance(dt);
            moving |= _thLabel[k].Advance(dt);
        }
        moving |= Step(ref _panelX, dt, full);
        moving |= _panelAlpha.Advance(dt);
        moving |= _crossAlpha.Advance(dt);
        moving |= _handleHover.Advance(dt);
        for (var i = 0; i < _tickNew.Length; i++)
        {
            moving |= _tickNewAlpha[i].Advance(dt);
            moving |= _tickOldAlpha[i].Advance(dt);
        }
        if (geometry)
        {
            _geoDirty = true;
        }
        InvalidateVisual();
        return moving;
    }

    private static bool Step(ref AnalyticsSpringChannel channel, double dt, bool full)
    {
        if (!channel.IsMoving)
        {
            return false;
        }
        if (full)
        {
            channel.Step(dt);
        }
        else
        {
            channel.Jump(channel.Target);
        }
        return channel.IsMoving;
    }

    #endregion

    #region Palette

    private void InvalidatePalette()
    {
        _paletteVersion = -1;
        _text.Clear();
        InvalidateVisual();
    }

    private void EnsurePalette()
    {
        if (!this.IsAttachedToVisualTree() || (_paletteVersion >= 0 && _palette.IsReady))
        {
            return;
        }
        _palette.Refresh(this);
        _paletteVersion = _palette.Version;
    }

    #endregion

    #region Layout and input

    protected override Size MeasureOverride(Size availableSize) => new(0, ChartHeight);

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.NewSize.Width > 0 && _y.Length > 0)
        {
            Retarget(false, UsageBudgetMotion.Jump);
            if (_inspectDay > 0)
            {
                _panelX.Jump(PanelTargetX(_inspectDay));
            }
        }
        _lastW = e.NewSize.Width;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _paletteVersion = -1;
    }

    private int DayAt(double x)
    {
        var dx = (Right - Left) / Math.Max(1, Series.Days);
        return Math.Clamp((int)Math.Round((x - Left) / dx, MidpointRounding.AwayFromZero), 1, Series.Days);
    }

    private bool InPlot(Point p) => p.X >= Left - 0.5 && p.X <= Right + 0.5 && p.Y >= 0 && p.Y <= Bottom;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        if (!InPlot(p) || Series.Days < 1)
        {
            if (_pointerInspect)
            {
                _pointerInspect = false;
                InspectRequested?.Invoke(null, false);
            }
            return;
        }
        var day = DayAt(p.X);
        if (day != _inspectDay || !_pointerInspect)
        {
            _pointerInspect = true;
            InspectRequested?.Invoke(day, false);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Pointer.Type != PointerType.Mouse)
        {
            OnPointerMoved(e);
        }
        Focus();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_pointerInspect)
        {
            _pointerInspect = false;
            InspectRequested?.Invoke(null, false);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        // Measured: a keyboard focus shows today's card at once.
        if (e.NavigationMethod == NavigationMethod.Tab && _inspectDay == 0 && Series.Days > 0)
        {
            _inspectByKey = true;
            InspectRequested?.Invoke(Math.Max(1, Series.Today), true);
        }
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (_inspectByKey)
        {
            _inspectByKey = false;
            InspectRequested?.Invoke(null, true);
        }
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || Series.Days < 1)
        {
            return;
        }
        var day = KeyDay;
        int? next = e.Key switch
        {
            Key.Left or Key.Down => day - 1,
            Key.Right or Key.Up => day + 1,
            Key.PageUp => day + 7,
            Key.PageDown => day - 7,
            Key.Home => 1,
            Key.End => Series.Days,
            _ => null
        };
        if (e.Key == Key.Escape)
        {
            if (_inspectDay > 0)
            {
                _inspectByKey = false;
                _pointerInspect = false;
                InspectRequested?.Invoke(null, true);
                e.Handled = true;
            }
            return;
        }
        if (next is { } value)
        {
            _inspectByKey = true;
            _pointerInspect = false;
            InspectRequested?.Invoke(Math.Clamp(value, 1, Series.Days), true);
            e.Handled = true;
        }
    }

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new UsageSliderPeer(this, this);

    #endregion
}
