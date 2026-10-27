using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The rows of a <see cref="HorizonChart"/> drawn by one control: name, folded band strip and value for every row, the
/// synchronized crosshair and the day axis. It owns the fold (one continuous band count, so the alpha and the four layer
/// transforms follow it), the reorder of the rows, the crosshair's follow spring, hit testing and the keyboard; the block
/// that hosts it owns the data, the state and the announcements.
/// </summary>
/// <remarks>
/// Code-driven motion: the fold is a single spring value that every row's four layers read each frame (a ControlTheme
/// transition animates properties of elements, not a number shared by 112 drawn layers), the rows' FLIP offsets are one
/// spring each, and the crosshair chases a pointer-driven target that keeps moving, keeping its velocity. One
/// <see cref="LiveLoop"/> advances them all and stops when everything rests.
/// </remarks>
internal sealed partial class HorizonPlot : Control
{
    private const double CrossFadeSeconds = 0.16;

    #region Geometry properties (from the token)

    // Only called from the static field initializers below.
#pragma warning disable AVP1001
    private static StyledProperty<double> Metric(string name, double fallback)
        => AvaloniaProperty.Register<HorizonPlot, double>(name, fallback);
#pragma warning restore AVP1001

    public static readonly StyledProperty<double> MediumWidthProperty = Metric(nameof(MediumWidth), HorizonEngine.MediumWidth);
    public static readonly StyledProperty<double> CompactWidthProperty = Metric(nameof(CompactWidth), HorizonEngine.CompactWidth);
    public static readonly StyledProperty<double> RowHeightProperty = Metric(nameof(RowHeight), HorizonEngine.Wide.RowHeight);
    public static readonly StyledProperty<double> CompactRowHeightProperty = Metric(nameof(CompactRowHeight), HorizonEngine.Compact.RowHeight);
    public static readonly StyledProperty<double> BandInsetProperty = Metric(nameof(BandInset), HorizonEngine.Wide.BandInset);
    public static readonly StyledProperty<double> BandRadiusProperty = Metric(nameof(BandRadius), 4);
    public static readonly StyledProperty<double> SidePaddingProperty = Metric(nameof(SidePadding), HorizonEngine.Wide.SidePadding);
    public static readonly StyledProperty<double> CompactSidePaddingProperty = Metric(nameof(CompactSidePadding), HorizonEngine.Compact.SidePadding);
    public static readonly StyledProperty<double> NameWidthProperty = Metric(nameof(NameWidth), HorizonEngine.Wide.NameWidth);
    public static readonly StyledProperty<double> MediumNameWidthProperty = Metric(nameof(MediumNameWidth), HorizonEngine.Medium.NameWidth);
    public static readonly StyledProperty<double> CompactNameWidthProperty = Metric(nameof(CompactNameWidth), HorizonEngine.Compact.NameWidth);
    public static readonly StyledProperty<double> ValueWidthProperty = Metric(nameof(ValueWidth), HorizonEngine.Wide.ValueWidth);
    public static readonly StyledProperty<double> MediumValueWidthProperty = Metric(nameof(MediumValueWidth), HorizonEngine.Medium.ValueWidth);
    public static readonly StyledProperty<double> CompactValueWidthProperty = Metric(nameof(CompactValueWidth), HorizonEngine.Compact.ValueWidth);
    public static readonly StyledProperty<double> ColumnGapProperty = Metric(nameof(ColumnGap), HorizonEngine.Wide.ColumnGap);
    public static readonly StyledProperty<double> MediumColumnGapProperty = Metric(nameof(MediumColumnGap), HorizonEngine.Medium.ColumnGap);
    public static readonly StyledProperty<double> CompactColumnGapProperty = Metric(nameof(CompactColumnGap), HorizonEngine.Compact.ColumnGap);
    public static readonly StyledProperty<double> CrosshairOverhangProperty = Metric(nameof(CrosshairOverhang), 6);

    public double MediumWidth { get => GetValue(MediumWidthProperty); set => SetValue(MediumWidthProperty, value); }
    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }
    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }
    public double CompactRowHeight { get => GetValue(CompactRowHeightProperty); set => SetValue(CompactRowHeightProperty, value); }
    public double BandInset { get => GetValue(BandInsetProperty); set => SetValue(BandInsetProperty, value); }
    public double BandRadius { get => GetValue(BandRadiusProperty); set => SetValue(BandRadiusProperty, value); }
    public double SidePadding { get => GetValue(SidePaddingProperty); set => SetValue(SidePaddingProperty, value); }
    public double CompactSidePadding { get => GetValue(CompactSidePaddingProperty); set => SetValue(CompactSidePaddingProperty, value); }
    public double NameWidth { get => GetValue(NameWidthProperty); set => SetValue(NameWidthProperty, value); }
    public double MediumNameWidth { get => GetValue(MediumNameWidthProperty); set => SetValue(MediumNameWidthProperty, value); }
    public double CompactNameWidth { get => GetValue(CompactNameWidthProperty); set => SetValue(CompactNameWidthProperty, value); }
    public double ValueWidth { get => GetValue(ValueWidthProperty); set => SetValue(ValueWidthProperty, value); }
    public double MediumValueWidth { get => GetValue(MediumValueWidthProperty); set => SetValue(MediumValueWidthProperty, value); }
    public double CompactValueWidth { get => GetValue(CompactValueWidthProperty); set => SetValue(CompactValueWidthProperty, value); }
    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }
    public double MediumColumnGap { get => GetValue(MediumColumnGapProperty); set => SetValue(MediumColumnGapProperty, value); }
    public double CompactColumnGap { get => GetValue(CompactColumnGapProperty); set => SetValue(CompactColumnGapProperty, value); }
    public double CrosshairOverhang { get => GetValue(CrosshairOverhangProperty); set => SetValue(CrosshairOverhangProperty, value); }

    #endregion

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(512);
    private readonly LiveLoop _loop;

    // Rows (data order).
    private string[] _names = [];
    private double[][] _values = [];
    private StreamGeometry?[] _upper = [];
    private StreamGeometry?[] _lower = [];
    private bool[] _hasUpper = [];
    private bool[] _hasLower = [];
    private AnalyticsSpringChannel[] _offset = [];
    private string[] _valueTexts = [];
    private int[] _rowAt = [];
    private int[] _slotOf = [];
    private int _days;
    private double _extent = 36;
    private IReadOnlyList<HorizonTick> _ticks = [];
    private HorizonSort _sort = HorizonSort.Peak;
    private Func<double, string> _format = DefaultFormat;
    private string _emptyText = "No rows to show.";
    private Color? _aboveColor;
    private Color? _belowColor;

    // Fold and crosshair.
    private AnalyticsSpringChannel _fold = new(3, 0.0005, 0.005);
    private AnalyticsSpringChannel _scrub = new(0, 0.0005, 0.005);
    private AnalyticsTween _crossOpacity = new(0);
    private int _day = -1;
    private bool _pointerDriven;
    private bool _touchCapture;
    private Point _touchStart;
    private int _paletteVersion = -1;
    private string? _tooltipFor;

    static HorizonPlot()
    {
        FocusableProperty.OverrideDefaultValue<HorizonPlot>(true);
        AffectsMeasure<HorizonPlot>(MediumWidthProperty, CompactWidthProperty, RowHeightProperty, CompactRowHeightProperty,
            BandInsetProperty, SidePaddingProperty, CompactSidePaddingProperty);
        AffectsRender<HorizonPlot>(BandRadiusProperty, NameWidthProperty, MediumNameWidthProperty, CompactNameWidthProperty,
            ValueWidthProperty, MediumValueWidthProperty, CompactValueWidthProperty, ColumnGapProperty, MediumColumnGapProperty,
            CompactColumnGapProperty, CrosshairOverhangProperty);
    }

    public HorizonPlot()
    {
        // Function as well as decoration: the loop also finishes motions after the level drops (they then snap).
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        ActualThemeVariantChanged += (_, _) => InvalidatePalette();
        ResourcesChanged += (_, _) => InvalidatePalette();
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    /// <summary>Raised when the person moves the crosshair (pointer, touch or keys): the day (null clears) and whether a key did it.</summary>
    public event Action<int?, bool>? DayRequested;

    /// <summary>The crosshair's day, or −1 when it is hidden.</summary>
    public int Day => _day;

    /// <summary>The band count the rows are folded to right now (continuous while the fold runs).</summary>
    public double FoldedBands => _fold.Value;

    /// <summary>The band count the fold is heading for.</summary>
    public double TargetBands => _fold.Target;

    /// <summary>The rows, top to bottom, as indexes into the data (what is drawn at rest).</summary>
    public IReadOnlyList<int> RowOrder => _rowAt;

    /// <summary>The crosshair's drawn position as a day index (continuous while it follows).</summary>
    public double CrosshairPosition => _scrub.Value;

    /// <summary>The crosshair's opacity right now (0 hidden, 1 shown).</summary>
    public double CrosshairOpacity => _crossOpacity.Value;

    /// <summary>The remaining reorder offset of data row <paramref name="row"/> in pixels (0 at rest).</summary>
    public double RowOffset(int row) => row >= 0 && row < _offset.Length ? _offset[row].Value : 0;

    /// <summary>The text of the value column of data row <paramref name="row"/>.</summary>
    public string ValueText(int row) => row >= 0 && row < _valueTexts.Length ? _valueTexts[row] : string.Empty;

    /// <summary>True while any spring or fade runs.</summary>
    public bool IsMoving => _fold.IsMoving || _scrub.IsMoving || _crossOpacity.IsRunning || _offset.Any(o => o.IsMoving);

    /// <summary>The metrics of the current width.</summary>
    public HorizonLayout CurrentLayout => HorizonEngine.Resolve(Bounds.Width > 0 ? Bounds.Width : 600, Metrics(HorizonTier.Wide),
        Metrics(HorizonTier.Medium), Metrics(HorizonTier.Compact), MediumWidth, CompactWidth);

    private HorizonMetrics Metrics(HorizonTier tier) => tier switch
    {
        HorizonTier.Compact => new HorizonMetrics(CompactRowHeight, BandInset, CompactSidePadding, CompactNameWidth, CompactValueWidth,
            CompactColumnGap, 12),
        HorizonTier.Medium => new HorizonMetrics(RowHeight, BandInset, SidePadding, MediumNameWidth, MediumValueWidth, MediumColumnGap, 14),
        _ => new HorizonMetrics(RowHeight, BandInset, SidePadding, NameWidth, ValueWidth, ColumnGap, 14)
    };

    private static string DefaultFormat(double v) => AnalyticsFormat.Percent(v, 0, signed: true);

    #region Data

    /// <summary>Replaces the rows. Matching keys are not tracked: new data jumps to its order.</summary>
    public void SetRows(IReadOnlyList<string> names, IReadOnlyList<double[]> values, int days, double extent, IReadOnlyList<HorizonTick> ticks,
        HorizonSort sort)
    {
        _names = names.ToArray();
        _values = values.ToArray();
        _days = days;
        _extent = extent;
        _ticks = ticks;
        _sort = sort;
        var n = _names.Length;
        _upper = new StreamGeometry?[n];
        _lower = new StreamGeometry?[n];
        _hasUpper = new bool[n];
        _hasLower = new bool[n];
        _offset = new AnalyticsSpringChannel[n];
        for (var i = 0; i < n; i++)
        {
            _offset[i] = new AnalyticsSpringChannel(0, 0.1, 0.5);
            Sides(i);
        }
        _valueTexts = new string[n];
        _rowAt = HorizonEngine.Order(_names, _values, sort);
        _slotOf = new int[n];
        for (var slot = 0; slot < n; slot++)
        {
            _slotOf[_rowAt[slot]] = slot;
        }
        if (_day >= days)
        {
            _day = days - 1;
        }
        RefreshTexts();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Changes only the extent (geometry is rebuilt).</summary>
    public void SetExtent(double extent)
    {
        _extent = extent;
        Array.Clear(_upper);
        Array.Clear(_lower);
        for (var i = 0; i < _names.Length; i++)
        {
            Sides(i);
        }
        InvalidateVisual();
    }

    private void Sides(int row)
    {
        var hasUpper = false;
        var hasLower = false;
        foreach (var v in _values[row])
        {
            hasUpper |= !double.IsNaN(v) && v > 0 && _extent > 0;
            hasLower |= !double.IsNaN(v) && v < 0 && _extent > 0;
        }
        _hasUpper[row] = hasUpper;
        _hasLower[row] = hasLower;
    }

    /// <summary>The colours of the sides above and below the baseline (null: Series1 and Series3).</summary>
    public void SetColors(Color? above, Color? below)
    {
        _aboveColor = above;
        _belowColor = below;
        InvalidateVisual();
    }

    /// <summary>How row values read (the value column).</summary>
    public void SetFormatter(Func<double, string>? format)
    {
        _format = format ?? DefaultFormat;
        RefreshTexts();
        InvalidateVisual();
    }

    /// <summary>The text of an empty chart.</summary>
    public void SetEmptyText(string text)
    {
        _emptyText = text;
        InvalidateVisual();
    }

    /// <summary>The month ticks (positions are day indexes).</summary>
    public void SetTicks(IReadOnlyList<HorizonTick> ticks)
    {
        _ticks = ticks;
        InvalidateVisual();
    }

    private void RefreshTexts()
    {
        var day = _day >= 0 ? _day : _days - 1;
        for (var i = 0; i < _names.Length; i++)
        {
            var v = day >= 0 && day < _values[i].Length ? _values[i][day] : double.NaN;
            _valueTexts[i] = double.IsNaN(v) ? AnalyticsFormat.Missing : _format(v);
        }
    }

    #endregion

    #region State set by the block

    /// <summary>
    /// Changes the order. The rows start where they were drawn and spring to their new slots, all at once (FLIP); the
    /// current offsets are kept, so a re-sort in flight continues from where each row is.
    /// </summary>
    public void SetSort(HorizonSort sort, bool animate)
    {
        _sort = sort;
        if (_names.Length == 0)
        {
            return;
        }
        var layout = CurrentLayout;
        var oldY = new double[_names.Length];
        for (var row = 0; row < oldY.Length; row++)
        {
            oldY[row] = layout.RowTop(_slotOf[row]) + _offset[row].Value;
        }
        _rowAt = HorizonEngine.Order(_names, _values, sort);
        for (var slot = 0; slot < _rowAt.Length; slot++)
        {
            _slotOf[_rowAt[slot]] = slot;
        }
        var full = animate && ArcMotion.IsFull(this) && this.IsAttachedToVisualTree();
        for (var row = 0; row < oldY.Length; row++)
        {
            var delta = oldY[row] - layout.RowTop(_slotOf[row]);
            if (full && Math.Abs(delta) > 0.01)
            {
                _offset[row].Value = delta;
                _offset[row].SpringTo(0, AnalyticsSprings.Reorder);
            }
            else
            {
                _offset[row].Jump(0);
            }
        }
        StartLoop();
        InvalidateVisual();
    }

    /// <summary>Folds the rows into <paramref name="bands"/> bands: the spring runs from the current (possibly in-flight) value.</summary>
    public void SetBands(int bands, bool animate)
    {
        if (animate && ArcMotion.IsFull(this) && this.IsAttachedToVisualTree())
        {
            _fold.SpringTo(bands, AnalyticsSprings.Fold);
            StartLoop();
        }
        else
        {
            _fold.Jump(bands);
        }
        InvalidateVisual();
    }

    /// <summary>
    /// Shows the crosshair at <paramref name="day"/> (null hides it). A hidden crosshair appears in place; a shown one
    /// follows on the Scrub spring. Values and the readout change at once.
    /// </summary>
    public void SetDay(int? day, bool animate = true)
    {
        var next = day is { } d && _days > 0 ? Math.Clamp(d, 0, _days - 1) : -1;
        var wasVisible = _crossOpacity.Target > 0.5;
        var full = animate && ArcMotion.IsFull(this) && this.IsAttachedToVisualTree();
        if (next >= 0)
        {
            if (!wasVisible)
            {
                _scrub.Jump(next);
            }
            else if (full)
            {
                _scrub.SpringTo(next, AnalyticsSprings.Scrub);
            }
            else
            {
                _scrub.Jump(next);
            }
            if (!wasVisible)
            {
                if (full)
                {
                    _crossOpacity.Start(1, TimeSpan.FromSeconds(CrossFadeSeconds), AnalyticsEasings.Standard);
                }
                else
                {
                    _crossOpacity.Jump(1);
                }
            }
        }
        else if (wasVisible)
        {
            // The line fades out where it is; its position is not reset.
            if (full)
            {
                _crossOpacity.Start(0, TimeSpan.FromSeconds(CrossFadeSeconds), AnalyticsEasings.Standard);
            }
            else
            {
                _crossOpacity.Jump(0);
            }
            _pointerDriven = false;
        }
        var changed = next != _day;
        _day = next;
        if (changed)
        {
            RefreshTexts();
        }
        StartLoop();
        InvalidateVisual();
    }

    private void StartLoop()
    {
        if (IsMoving)
        {
            _loop.Start();
        }
    }

    #endregion

    #region Frame loop

    private bool Advance(double dt)
    {
        var full = ArcMotion.IsFull(this);
        var moving = false;
        if (_fold.IsMoving)
        {
            if (full)
            {
                moving |= _fold.Step(dt);
            }
            else
            {
                _fold.Jump(_fold.Target);
            }
        }
        if (_scrub.IsMoving)
        {
            if (full)
            {
                moving |= _scrub.Step(dt);
            }
            else
            {
                _scrub.Jump(_scrub.Target);
            }
        }
        if (_crossOpacity.IsRunning)
        {
            if (full)
            {
                moving |= _crossOpacity.Advance(dt);
            }
            else
            {
                _crossOpacity.Jump(_crossOpacity.Target);
            }
        }
        for (var i = 0; i < _offset.Length; i++)
        {
            if (!_offset[i].IsMoving)
            {
                continue;
            }
            if (full)
            {
                moving |= _offset[i].Step(dt);
            }
            else
            {
                _offset[i].Jump(0);
            }
        }
        InvalidateVisual();
        return moving;
    }

    #endregion

    #region Layout

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 600;
        var layout = HorizonEngine.Resolve(width, Metrics(HorizonTier.Wide), Metrics(HorizonTier.Medium), Metrics(HorizonTier.Compact),
            MediumWidth, CompactWidth);
        var rows = Math.Max(_names.Length, _names.Length == 0 ? 3 : 0);
        return new Size(width, layout.TotalHeight(rows));
    }

    #endregion

    #region Input

    private bool InColumns(Point p, HorizonLayout layout)
        => p.X >= layout.PlotLeft && p.X <= layout.PlotRight && p.Y >= 0 && p.Y <= layout.AxisTop(_names.Length) + layout.AxisHeight;

    private int DayFor(Point p, HorizonLayout layout) => HorizonEngine.DayAt(p.X, layout.PlotLeft, layout.PlotWidth, _days);

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_names.Length == 0 || _days == 0)
        {
            return;
        }
        var layout = CurrentLayout;
        var point = e.GetPosition(this);
        if (e.Pointer.Type == PointerType.Touch)
        {
            OnTouchMoved(e, point, layout);
            return;
        }
        UpdateNameTip(point, layout);
        if (InColumns(point, layout))
        {
            var day = DayFor(point, layout);
            _pointerDriven = true;
            if (day != _day)
            {
                DayRequested?.Invoke(day, false);
            }
            else if (_crossOpacity.Target < 0.5)
            {
                DayRequested?.Invoke(day, false);
            }
        }
        else if (_pointerDriven && _day >= 0)
        {
            DayRequested?.Invoke(null, false);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type != PointerType.Touch && _pointerDriven && _day >= 0)
        {
            DayRequested?.Invoke(null, false);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_names.Length == 0 || _days == 0 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var layout = CurrentLayout;
        var point = e.GetPosition(this);
        if (e.Pointer.Type == PointerType.Touch)
        {
            // A tap sets the crosshair and it stays after lifting; a tap beside the columns clears it. Capturing waits for a
            // horizontal drag, so a vertical pan is left to the scroll viewer (the measured touch-action: pan-y).
            _touchStart = point;
            _touchCapture = false;
            if (InColumns(point, layout))
            {
                _pointerDriven = false;
                DayRequested?.Invoke(DayFor(point, layout), false);
            }
            else if (_day >= 0)
            {
                DayRequested?.Invoke(null, false);
            }
        }
        else if (InColumns(point, layout))
        {
            _pointerDriven = true;
            DayRequested?.Invoke(DayFor(point, layout), false);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_touchCapture)
        {
            _touchCapture = false;
            e.Pointer.Capture(null);
        }
    }

    private void OnTouchMoved(PointerEventArgs e, Point point, HorizonLayout layout)
    {
        if (!_touchCapture)
        {
            var dx = Math.Abs(point.X - _touchStart.X);
            var dy = Math.Abs(point.Y - _touchStart.Y);
            if (dx > 6 && dx > dy)
            {
                _touchCapture = true;
                e.Pointer.Capture(this);
            }
            else
            {
                return;
            }
        }
        _pointerDriven = false;
        var day = DayFor(point, layout);
        if (day != _day)
        {
            DayRequested?.Invoke(day, false);
        }
    }

    private void UpdateNameTip(Point point, HorizonLayout layout)
    {
        string? tip = null;
        if (point.X >= layout.Metrics.SidePadding && point.X <= layout.Metrics.SidePadding + layout.Metrics.NameWidth)
        {
            var slot = (int)Math.Floor((point.Y - layout.TopPadding) / layout.Metrics.RowHeight);
            if (slot >= 0 && slot < _rowAt.Length)
            {
                var name = _names[_rowAt[slot]];
                if (_palette.IsReady)
                {
                    var layoutText = _text.Get(name, new Typeface(_palette.BodyFont), layout.Metrics.FontSize);
                    tip = layoutText.Width > layout.Metrics.NameWidth ? name : null;
                }
            }
        }
        if (tip != _tooltipFor)
        {
            _tooltipFor = tip;
            ToolTip.SetTip(this, tip);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        // Measured: keyboard focus puts the crosshair on the latest day at once; focus from a click or code does not.
        if (e.NavigationMethod == NavigationMethod.Tab && _day < 0 && _days > 0)
        {
            _pointerDriven = false;
            DayRequested?.Invoke(_days - 1, true);
        }
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _days == 0)
        {
            return;
        }
        if (e.Key == Key.Escape)
        {
            if (_day >= 0)
            {
                DayRequested?.Invoke(null, true);
                e.Handled = true;
            }
            return;
        }
        if ((e.KeyModifiers & ~KeyModifiers.Shift) != KeyModifiers.None)
        {
            return;
        }
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        if (HorizonEngine.KeyDay(e.Key, shift, _day, _days) is { } next)
        {
            _pointerDriven = false;
            if (next != _day || _crossOpacity.Target < 0.5)
            {
                DayRequested?.Invoke(next, true);
            }
            e.Handled = true;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HorizonPlotPeer(this);

    #endregion

    #region Colours

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
}

/// <summary>The plot's peer: a focusable group named by the block's summary of the chart.</summary>
internal sealed class HorizonPlotPeer(HorizonPlot owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetLocalizedControlTypeCore() => "chart";

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
