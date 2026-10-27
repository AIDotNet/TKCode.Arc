using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>Texts, formatters and labels the chart needs from its block.</summary>
internal sealed class RevenueChartOptions
{
    public Func<double, string> Axis { get; set; } = v => AnalyticsFormat.Compact(v, 3, "$");

    public Func<double, string> Value { get; set; } = v => AnalyticsFormat.Compact(v, 3, "$");

    public Func<DateOnly, string> Month { get; set; } = m => RevenueData.DefaultMonthLabel(m, AnalyticsFormat.DefaultCulture);

    public Func<DateOnly, string> MonthYear { get; set; } = m => RevenueData.DefaultMonthYear(m, AnalyticsFormat.DefaultCulture);

    public string NewLabel { get; set; } = "New";

    public string ExpansionLabel { get; set; } = "Expansion";

    public string ChurnLabel { get; set; } = "Churn";

    public string NetNewLabel { get; set; } = "Net new";

    public string EmptyText { get; set; } = "Turn on a series to see revenue";

    /// <summary>The readout's comparison row; {0} is the previous month.</summary>
    public string PreviousRowFormat { get; set; } = "vs {0}";

    /// <summary>The spoken text of a month; {0} month, {1} new, {2} expansion, {3} churn, {4} net new.</summary>
    public string ScrubFormat { get; set; } = "{0}: new {1}, expansion {2}, churn {3}, net new {4}";
}

/// <summary>
/// The stacked revenue chart: new and expansion layers above the zero line, churn below it, y gridlines and labels, a month
/// axis that slides with the range, previous-period dashes, the crosshair with its three dots and the glide-following
/// readout panel. One self-drawn control: every path, tick and label is a coordinate computed from the springs of the
/// frame (x window from the shared <see cref="RevenueViewport"/>, y domain, series factors, compare factor).
/// </summary>
/// <remarks>
/// Why code-driven: the x domain, the y domain, three series factors and the compare factor all move on their own springs
/// and every path is rebuilt from them each frame; the readout follows the pointer on a spring; ControlTheme transitions
/// animate properties, not coordinates inside one drawing.
/// </remarks>
internal sealed partial class RevenueChartView : Control
{
    #region Geometry properties (from the token)

    public static readonly StyledProperty<double> PlotHeightProperty = AvaloniaProperty.Register<RevenueChartView, double>(nameof(PlotHeight), 256);
    public static readonly StyledProperty<double> CompactPlotHeightProperty = AvaloniaProperty.Register<RevenueChartView, double>(nameof(CompactPlotHeight), 216);
    public static readonly StyledProperty<double> CompactWidthProperty = AvaloniaProperty.Register<RevenueChartView, double>(nameof(CompactWidth), 600);
    public static readonly StyledProperty<double> LeftInsetProperty = AvaloniaProperty.Register<RevenueChartView, double>(nameof(LeftInset), 48);
    public static readonly StyledProperty<double> BottomInsetProperty = AvaloniaProperty.Register<RevenueChartView, double>(nameof(BottomInset), 28);
    public static readonly StyledProperty<double> FocusRadiusProperty = AvaloniaProperty.Register<RevenueChartView, double>(nameof(FocusRadius), 12);
    public static readonly StyledProperty<double> PanelOffsetProperty = AvaloniaProperty.Register<RevenueChartView, double>(nameof(PanelOffset), 14);
    public static readonly StyledProperty<double> PanelTopProperty = AvaloniaProperty.Register<RevenueChartView, double>(nameof(PanelTop), 14);
    public static readonly StyledProperty<double> DotRadiusProperty = AvaloniaProperty.Register<RevenueChartView, double>(nameof(DotRadius), 4);
    public static readonly StyledProperty<double> CrosshairTopProperty = AvaloniaProperty.Register<RevenueChartView, double>(nameof(CrosshairTop), 8);
    public static readonly StyledProperty<double> LabelLeftInsetProperty = AvaloniaProperty.Register<RevenueChartView, double>(nameof(LabelLeftInset), 14);
    public static readonly StyledProperty<double> LabelRightInsetProperty = AvaloniaProperty.Register<RevenueChartView, double>(nameof(LabelRightInset), 16);
    public static readonly StyledProperty<double> NewAreaOpacityProperty = AvaloniaProperty.Register<RevenueChartView, double>(nameof(NewAreaOpacity), 0.2);
    public static readonly StyledProperty<double> ExpansionAreaOpacityProperty = AvaloniaProperty.Register<RevenueChartView, double>(nameof(ExpansionAreaOpacity), 0.075);
    public static readonly StyledProperty<double> ChurnAreaOpacityProperty = AvaloniaProperty.Register<RevenueChartView, double>(nameof(ChurnAreaOpacity), 0.12);
    public static readonly StyledProperty<double> PrimaryLineWidthProperty = AvaloniaProperty.Register<RevenueChartView, double>(nameof(PrimaryLineWidth), 2);
    public static readonly StyledProperty<double> SecondaryLineWidthProperty = AvaloniaProperty.Register<RevenueChartView, double>(nameof(SecondaryLineWidth), 1.5);

    public double PlotHeight { get => GetValue(PlotHeightProperty); set => SetValue(PlotHeightProperty, value); }
    public double CompactPlotHeight { get => GetValue(CompactPlotHeightProperty); set => SetValue(CompactPlotHeightProperty, value); }
    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }
    public double LeftInset { get => GetValue(LeftInsetProperty); set => SetValue(LeftInsetProperty, value); }
    public double BottomInset { get => GetValue(BottomInsetProperty); set => SetValue(BottomInsetProperty, value); }
    public double FocusRadius { get => GetValue(FocusRadiusProperty); set => SetValue(FocusRadiusProperty, value); }
    public double PanelOffset { get => GetValue(PanelOffsetProperty); set => SetValue(PanelOffsetProperty, value); }
    public double PanelTop { get => GetValue(PanelTopProperty); set => SetValue(PanelTopProperty, value); }
    public double DotRadius { get => GetValue(DotRadiusProperty); set => SetValue(DotRadiusProperty, value); }
    public double CrosshairTop { get => GetValue(CrosshairTopProperty); set => SetValue(CrosshairTopProperty, value); }
    public double LabelLeftInset { get => GetValue(LabelLeftInsetProperty); set => SetValue(LabelLeftInsetProperty, value); }
    public double LabelRightInset { get => GetValue(LabelRightInsetProperty); set => SetValue(LabelRightInsetProperty, value); }
    public double NewAreaOpacity { get => GetValue(NewAreaOpacityProperty); set => SetValue(NewAreaOpacityProperty, value); }
    public double ExpansionAreaOpacity { get => GetValue(ExpansionAreaOpacityProperty); set => SetValue(ExpansionAreaOpacityProperty, value); }
    public double ChurnAreaOpacity { get => GetValue(ChurnAreaOpacityProperty); set => SetValue(ChurnAreaOpacityProperty, value); }
    public double PrimaryLineWidth { get => GetValue(PrimaryLineWidthProperty); set => SetValue(PrimaryLineWidthProperty, value); }
    public double SecondaryLineWidth { get => GetValue(SecondaryLineWidthProperty); set => SetValue(SecondaryLineWidthProperty, value); }

    #endregion

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(512);
    private readonly LiveLoop _loop;
    private readonly AnalyticsValueAxis _axis = new() { EnterDelay = TimeSpan.FromMilliseconds(50) };
    private readonly AnalyticsReadout _readout = new(AnalyticsReadoutStyle.Panel)
    {
        FollowSpring = AnalyticsSprings.ReadoutGlide,
        EnterDuration = TimeSpan.FromMilliseconds(160),
        EnterEasing = AnalyticsEasings.Enter,
        EnterScale = 0.97,
        ExitScale = 0.97
    };
    private readonly AnalyticsSpringArray _prev;

    private RevenueData _data = RevenueData.Empty;
    private RevenueChartOptions _options = new();
    private RevenueViewport? _viewport;
    private int _rs;
    private int _re;
    private bool _showNew = true;
    private bool _showExpansion = true;
    private bool _showChurn = true;
    private bool _compare;
    private AnalyticsSpringChannel _fNew = new(1);
    private AnalyticsSpringChannel _fExp = new(1);
    private AnalyticsSpringChannel _fChurn = new(1);
    private AnalyticsSpringChannel _cmp = new(0);
    private AnalyticsTween _empty = new(0);
    private AnalyticsTween[] _label = [];
    private bool[] _labelOn = [];
    private string[] _labelText = [];
    private int _stride = 1;
    private int? _scrub;
    private bool _scrubByPointer;
    private int _paletteVersion = -1;
    private bool _geometryDirty = true;
    private bool _hasDomain;
    private double[] _prevTargets = [];

    private Point[] _pNew = [];
    private Point[] _pExp = [];
    private Point[] _pZero = [];
    private Point[] _pChurn = [];
    private Point[] _pPrevTop = [];
    private Point[] _pPrevChurn = [];
    private StreamGeometry? _gExpArea;
    private StreamGeometry? _gNewArea;
    private StreamGeometry? _gChurnArea;
    private StreamGeometry? _gNewLine;
    private StreamGeometry? _gExpLine;
    private StreamGeometry? _gChurnLine;
    private StreamGeometry? _gPrevTop;
    private StreamGeometry? _gPrevChurn;
    private int _pointCount;

    static RevenueChartView()
    {
        FocusableProperty.OverrideDefaultValue<RevenueChartView>(true);
        AffectsMeasure<RevenueChartView>(PlotHeightProperty, CompactPlotHeightProperty, CompactWidthProperty);
        AffectsRender<RevenueChartView>(LeftInsetProperty, BottomInsetProperty, FocusRadiusProperty, PanelOffsetProperty, PanelTopProperty,
            DotRadiusProperty, CrosshairTopProperty, LabelLeftInsetProperty, LabelRightInsetProperty, NewAreaOpacityProperty,
            ExpansionAreaOpacityProperty, ChurnAreaOpacityProperty, PrimaryLineWidthProperty, SecondaryLineWidthProperty);
    }

    public RevenueChartView()
    {
        _prev = new AnalyticsSpringArray(this);
        _prev.Changed += MarkDirty;
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        Cursor = new Cursor(StandardCursorType.Cross);
        ActualThemeVariantChanged += (_, _) => InvalidatePalette();
        ResourcesChanged += (_, _) => InvalidatePalette();
    }

    /// <summary>Raised when the person moves the inspected month (pointer, keys): the month index, or null to close; the flag is true for the keyboard.</summary>
    public event Action<int?, bool>? ScrubRequested;

    /// <summary>The window of the chart; set once by the block.</summary>
    public RevenueViewport? Viewport
    {
        get => _viewport;
        set
        {
            if (_viewport is not null)
            {
                _viewport.Changed -= MarkDirty;
            }
            _viewport = value;
            if (value is not null)
            {
                value.Changed += MarkDirty;
            }
            MarkDirty();
        }
    }

    /// <summary>The plot is under <see cref="CompactWidth"/> wide (shorter plot).</summary>
    public bool IsCompact => Bounds.Width > 0 && Bounds.Width < CompactWidth;

    /// <summary>The inspected month, or null.</summary>
    public int? Scrub => _scrub;

    /// <summary>The readout card (tests).</summary>
    internal AnalyticsReadout Readout => _readout;

    /// <summary>The value axis (tests).</summary>
    internal AnalyticsValueAxis Axis => _axis;

    /// <summary>The series factor of <paramref name="series"/> (0 hidden, 1 shown; tests).</summary>
    internal double SeriesFactor(RevenueSeries series) => series switch { RevenueSeries.New => _fNew.Value, RevenueSeries.Expansion => _fExp.Value, _ => _fChurn.Value };

    /// <summary>The compare factor (0 off, 1 on; tests).</summary>
    internal double CompareFactor => _cmp.Value;

    /// <summary>The empty-state opacity (tests).</summary>
    internal double EmptyOpacity => _empty.Value;

    /// <summary>The current opacity of month <paramref name="i"/>'s axis label (tests).</summary>
    internal double LabelOpacity(int i) => i >= 0 && i < _label.Length ? _label[i].Value : 0;

    /// <summary>True while anything moves.</summary>
    internal bool IsMoving => _loop.IsActive;

    /// <summary>The plot area (where the crosshair and the pointer work).</summary>
    public Rect HitRect => new(LeftInset, 0, Math.Max(0, Bounds.Width - LeftInset), Math.Max(0, Bounds.Height - BottomInset));

    private double CurrentHeight => IsCompact ? CompactPlotHeight : PlotHeight;

    private double PlotBottom => CurrentHeight - BottomInset;

    private ArcMotionLevel Level => this.IsAttachedToVisualTree() ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;

    #region State in

    /// <summary>Shows new numbers (jumps: the series, compare and range are re-applied by the block).</summary>
    public void SetData(RevenueData data, int rangeStart, int rangeEnd)
    {
        _data = data;
        _rs = rangeStart;
        _re = rangeEnd;
        var n = data.Count;
        _label = new AnalyticsTween[n];
        _labelOn = new bool[n];
        _labelText = new string[n];
        _pNew = new Point[n + 2];
        _pExp = new Point[n + 2];
        _pZero = new Point[n + 2];
        _pChurn = new Point[n + 2];
        _pPrevTop = new Point[n + 2];
        _pPrevChurn = new Point[n + 2];
        _prevTargets = new double[n * 3];
        if (_scrub is { } s && s >= n)
        {
            _scrub = null;
            _readout.Clear();
        }
        UpdateLabels(animate: false);
        UpdatePrev(animate: false);
        UpdateDomain(animate: false);
        RefreshReadout();
        MarkDirty();
    }

    /// <summary>Sets texts and formatters.</summary>
    public void SetOptions(RevenueChartOptions options)
    {
        _options = options;
        for (var i = 0; i < _labelText.Length; i++)
        {
            _labelText[i] = options.Month(_data.Months[i]);
        }
        UpdateDomain(animate: false);
        RefreshReadout();
        MarkDirty();
    }

    /// <summary>The whole-month range the labels, the y domain and the comparison follow.</summary>
    public void SetRange(int start, int end, bool animate)
    {
        _rs = start;
        _re = end;
        UpdateLabels(animate);
        UpdatePrev(animate);
        UpdateDomain(animate);
        if (_scrub is { } s && (s < start || s > end))
        {
            ScrubRequested?.Invoke(Math.Clamp(s, start, end), false);
        }
        RefreshReadout();
        MarkDirty();
    }

    /// <summary>Shows or hides the series: the layers collapse to zero (or rise) on Smooth springs, the y domain follows.</summary>
    public void SetSeries(bool showNew, bool showExpansion, bool showChurn, bool animate)
    {
        _showNew = showNew;
        _showExpansion = showExpansion;
        _showChurn = showChurn;
        Go(ref _fNew, showNew ? 1 : 0, animate);
        Go(ref _fExp, showExpansion ? 1 : 0, animate);
        Go(ref _fChurn, showChurn ? 1 : 0, animate);
        var none = !showNew && !showExpansion && !showChurn;
        if (animate && Level == ArcMotionLevel.Full)
        {
            _empty.Start(none ? 1 : 0, ArcMotionPresets.Standard, AnalyticsEasings.Enter, TimeSpan.FromMilliseconds(120));
        }
        else if (animate && Level == ArcMotionLevel.Reduced)
        {
            _empty.Start(none ? 1 : 0, ArcMotionPresets.Fast, AnalyticsEasings.Enter);
        }
        else
        {
            _empty.Jump(none ? 1 : 0);
        }
        UpdateDomain(animate);
        RefreshReadout();
        StartLoop();
        MarkDirty();
    }

    /// <summary>Draws (or hides) the previous period.</summary>
    public void SetCompare(bool compare, bool animate)
    {
        _compare = compare;
        Go(ref _cmp, compare ? 1 : 0, animate);
        UpdateDomain(animate);
        RefreshReadout();
        StartLoop();
        MarkDirty();
    }

    /// <summary>Puts the crosshair and the readout on month <paramref name="index"/> (null closes them).</summary>
    public void SetScrub(int? index, bool byPointer)
    {
        if (index is { } i && (i < 0 || i >= _data.Count))
        {
            index = null;
        }
        _scrub = index;
        _scrubByPointer = index is not null && byPointer;
        RefreshReadout();
        StartLoop();
        InvalidateVisual();
    }

    private void Go(ref AnalyticsSpringChannel channel, double target, bool animate)
    {
        if (animate && Level == ArcMotionLevel.Full)
        {
            channel.SpringTo(target, AnalyticsSprings.Smooth);
        }
        else
        {
            channel.Jump(target);
        }
    }

    private void StartLoop()
    {
        if (IsMovingState())
        {
            _loop.Start();
        }
    }

    private bool IsMovingState() =>
        _fNew.IsMoving || _fExp.IsMoving || _fChurn.IsMoving || _cmp.IsMoving || _axis.IsMoving || _readout.IsMoving ||
        _empty.IsRunning || AnyLabelRunning();

    private bool AnyLabelRunning()
    {
        foreach (var t in _label)
        {
            if (t.IsRunning)
            {
                return true;
            }
        }
        return false;
    }

    private void UpdateLabels(bool animate)
    {
        var n = _data.Count;
        if (n == 0)
        {
            return;
        }
        var width = Math.Max(0, Bounds.Width - LeftInset);
        var span = Math.Max(1, _re - _rs);
        _stride = RevenueData.LabelStride(width / span);
        var level = Level;
        for (var i = 0; i < n; i++)
        {
            if (_labelText[i] is null)
            {
                _labelText[i] = _options.Month(_data.Months[i]);
            }
            var on = i >= _rs && i <= _re && i % _stride == 0;
            if (on == _labelOn[i] && _label[i].Target == (on ? 1 : 0))
            {
                continue;
            }
            _labelOn[i] = on;
            if (animate && level != ArcMotionLevel.Off && level != ArcMotionLevel.Reduced)
            {
                _label[i].Start(on ? 1 : 0, TimeSpan.FromMilliseconds(50), AnalyticsEasings.EaseOut);
            }
            else
            {
                _label[i].Jump(on ? 1 : 0);
            }
        }
        StartLoop();
    }

    private void UpdatePrev(bool animate)
    {
        var n = _data.Count;
        if (n == 0)
        {
            return;
        }
        var length = _re - _rs + 1;
        for (var i = 0; i < n; i++)
        {
            var p = i - length;
            _prevTargets[i] = p >= 0 ? _data.New[p] : 0;
            _prevTargets[n + i] = p >= 0 ? _data.Expansion[p] : 0;
            _prevTargets[2 * n + i] = p >= 0 ? _data.Churn[p] : 0;
        }
        if (animate && _prev.Count == _prevTargets.Length)
        {
            _prev.SetTargets(_prevTargets);
        }
        else
        {
            _prev.Jump(_prevTargets);
        }
    }

    private void UpdateDomain(bool animate)
    {
        if (_data.Count == 0)
        {
            return;
        }
        var ticks = _data.Domain(_rs, _re, _showNew, _showExpansion, _showChurn, _compare, PlotBottom);
        _axis.Set(ticks, Level, animate && _hasDomain);
        _hasDomain = true;
        StartLoop();
    }

    #endregion

    #region Frame loop

    private bool Advance(double dt)
    {
        var moving = _fNew.Step(dt);
        moving |= _fExp.Step(dt);
        moving |= _fChurn.Step(dt);
        moving |= _cmp.Step(dt);
        moving |= _axis.Advance(dt);
        moving |= _readout.Advance(dt);
        moving |= _empty.Advance(dt);
        for (var i = 0; i < _label.Length; i++)
        {
            moving |= _label[i].Advance(dt);
        }
        if (Level != ArcMotionLevel.Full)
        {
            // The level dropped while a spring ran: land every spring (the fades above finish on their own).
            _fNew.Jump(_showNew ? 1 : 0);
            _fExp.Jump(_showExpansion ? 1 : 0);
            _fChurn.Jump(_showChurn ? 1 : 0);
            _cmp.Jump(_compare ? 1 : 0);
        }
        _geometryDirty = true;
        InvalidateVisual();
        return moving;
    }

    private void MarkDirty()
    {
        _geometryDirty = true;
        InvalidateVisual();
    }

    private void InvalidatePalette()
    {
        _paletteVersion = -1;
        _text.Clear();
        MarkDirty();
    }

    private void EnsurePalette()
    {
        if (!_palette.IsReady || _paletteVersion < 0)
        {
            _palette.Refresh(this);
            _paletteVersion = _palette.Version;
        }
    }

    #endregion

    #region Readout

    private void RefreshReadout()
    {
        var level = Level;
        if (_scrub is not { } i || _data.Count == 0 || Bounds.Width <= 0 || !_palette.IsReady)
        {
            if (_scrub is null)
            {
                _readout.Hide(level);
            }
            return;
        }
        _readout.SetContent(ContentFor(i), level);
        var size = _readout.Measure(_text, _palette);
        var x = CrossX(i);
        var bounds = new Rect(0, 0, Bounds.Width, CurrentHeight);
        _readout.Show(AnalyticsReadout.Beside(x, PanelTop, size, bounds, PanelOffset), level);
    }

    private AnalyticsReadoutContent ContentFor(int i)
    {
        var o = _options;
        var ex = _palette.Accent;
        var rows = new List<AnalyticsReadoutRow>(5)
        {
            new(o.NewLabel, o.Value(_data.New[i]), _palette.Accent, IsMuted: !_showNew),
            new(o.ExpansionLabel, o.Value(_data.Expansion[i]), AnalyticsColors.Mix(ex, 0.55, _palette.Surface), IsMuted: !_showExpansion),
            new(o.ChurnLabel, o.Value(_data.Churn[i]), _palette.Danger, IsMuted: !_showChurn),
            new(o.NetNewLabel, o.Value(_data.Net[i]), IsEmphasized: true, HasRule: true)
        };
        var length = _re - _rs + 1;
        if (_compare && i - length >= 0)
        {
            var p = i - length;
            rows.Add(new AnalyticsReadoutRow(string.Format(CultureInfo.CurrentCulture, o.PreviousRowFormat, o.MonthYear(_data.Months[p])),
                o.Value(_data.Net[p]), IsMuted: true));
        }
        return new AnalyticsReadoutContent(o.MonthYear(_data.Months[i]), Rows: rows);
    }

    /// <summary>What a screen reader hears for month <paramref name="i"/>.</summary>
    public string SpokenText(int i)
    {
        var o = _options;
        return string.Format(CultureInfo.CurrentCulture, o.ScrubFormat, o.MonthYear(_data.Months[i]), o.Value(_data.New[i]),
            o.Value(_data.Expansion[i]), o.Value(_data.Churn[i]), o.Value(_data.Net[i]));
    }

    #endregion

    #region Layout and input

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
        return new Size(width, width < CompactWidth ? CompactPlotHeight : PlotHeight);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateLabels(animate: false);
        UpdateDomain(animate: false);
        RefreshReadout();
        MarkDirty();
    }

    private double XOf(double month)
    {
        var s = _viewport?.ChartStart ?? _rs;
        var e = _viewport?.ChartEnd ?? _re;
        var width = Bounds.Width - LeftInset;
        return Math.Abs(e - s) < 1e-6 ? LeftInset + width : LeftInset + (month - s) / (e - s) * width;
    }

    private double MonthAt(double x)
    {
        var s = _viewport?.ChartStart ?? _rs;
        var e = _viewport?.ChartEnd ?? _re;
        var width = Bounds.Width - LeftInset;
        return width <= 0 ? s : s + (x - LeftInset) / width * (e - s);
    }

    /// <summary>The crosshair's x for month <paramref name="i"/> (whole pixel centre).</summary>
    public double CrossX(int i) => Math.Round(XOf(i), MidpointRounding.AwayFromZero) + 0.5;

    private int? IndexAt(Point p)
    {
        if (_data.Count == 0 || !HitRect.Contains(p))
        {
            return null;
        }
        var month = (int)Math.Round(MonthAt(p.X), MidpointRounding.AwayFromZero);
        return Math.Clamp(month, _rs, _re);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var index = IndexAt(e.GetPosition(this));
        if (index is null)
        {
            if (_scrub is not null && _scrubByPointer)
            {
                ScrubRequested?.Invoke(null, false);
            }
            return;
        }
        if (index != _scrub)
        {
            ScrubRequested?.Invoke(index, false);
        }
        _scrubByPointer = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_scrub is not null && _scrubByPointer)
        {
            ScrubRequested?.Invoke(null, false);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && IndexAt(e.GetPosition(this)) is { } index)
        {
            Focus(NavigationMethod.Pointer);
            if (index != _scrub)
            {
                ScrubRequested?.Invoke(index, false);
            }
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        // Measured: focusing the plot shows the readout of the last month of the range at once.
        if (_scrub is null && e.NavigationMethod != NavigationMethod.Pointer && _data.Count > 0)
        {
            ScrubRequested?.Invoke(_re, true);
        }
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (_scrub is not null && !_scrubByPointer)
        {
            ScrubRequested?.Invoke(null, true);
        }
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _data.Count == 0)
        {
            return;
        }
        if (e.Key == Key.Escape)
        {
            if (_scrub is not null)
            {
                ScrubRequested?.Invoke(null, true);
                e.Handled = true;
            }
            return;
        }
        // Shift does not enlarge the step here (it does on the overview strip).
        var relative = _scrub is { } s ? s - _rs : -1;
        if (AnalyticsKeyboard.Step(e.Key, relative, _re - _rs + 1) is { } next)
        {
            ScrubRequested?.Invoke(_rs + next, true);
            e.Handled = true;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RevenueChartPeer(this);

    /// <summary>The value text of the inspected month.</summary>
    internal string? ValueText => _scrub is { } i ? SpokenText(i) : null;

    internal int RangeStart => _rs;

    internal int RangeEnd => _re;

    internal void RequestScrub(int month) => ScrubRequested?.Invoke(Math.Clamp(month, _rs, _re), true);

    #endregion
}

/// <summary>The chart's peer: a slider over the months of the range whose value text carries every number of the month.</summary>
internal sealed class RevenueChartPeer(RevenueChartView owner) : ControlAutomationPeer(owner), IRangeValueProvider, IValueProvider
{
    private RevenueChartView Chart => (RevenueChartView)Owner;

    public bool IsReadOnly => false;

    public double Minimum => Chart.RangeStart;

    public double Maximum => Chart.RangeEnd;

    double IRangeValueProvider.Value => Chart.Scrub ?? Chart.RangeEnd;

    string? IValueProvider.Value => Chart.ValueText;

    public double SmallChange => 1;

    public double LargeChange => 1;

    public void SetValue(double value) => Chart.RequestScrub((int)Math.Round(value));

    public void SetValue(string? value)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
}
