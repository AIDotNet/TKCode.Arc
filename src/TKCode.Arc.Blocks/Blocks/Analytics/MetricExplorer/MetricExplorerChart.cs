using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The open chart: five grid rows, y labels that cross-fade in place (staggered 35ms from the bottom), x labels on the ladder
/// that fits (day ladders cross-fade when the range changes), a polyline for this period and a dashed one for the one before
/// it, the last day's dot, and an inspection layer (crosshair, two dots, a date capsule) driven by pointer, touch or keys.
/// </summary>
/// <remarks>
/// Code-driven motion (a ControlTheme cannot express it): the range switch animates one number, the count of visible days,
/// and every frame rebuilds both polylines from the daily series for that window (so the point count changes continuously and
/// the end stays pinned to the right edge); the y domain and the x label positions follow the same spring. A metric switch
/// keeps the x window and moves every point from where it is on screen to its new place: a per-day offset array springs
/// to zero, and a retarget in flight starts from the displayed shape. Both run on <see cref="AnalyticsSprings.Smooth"/>.
/// </remarks>
internal sealed class MetricExplorerChart : Control
{
    private const int Rows = 4;
    private const double StaggerMs = 35;

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(512);
    private readonly LiveLoop _loop;
    private readonly AnalyticsSpringArray _offsets;
    private readonly Dictionary<long, string> _tickText = new();
    private readonly string[] _slotText = new string[Rows + 1];
    private readonly string[] _slotPrevious = new string[Rows + 1];
    private readonly AnalyticsTween[] _slotIn = new AnalyticsTween[Rows + 1];
    private readonly AnalyticsTween[] _slotOut = new AnalyticsTween[Rows + 1];
    private readonly AnalyticsTween[] _levelAlpha = new AnalyticsTween[5];

    private MetricExplorerGeometry _geometry = new();
    private MetricExplorerSeries? _series;
    private int _days = 30;
    private bool _showPrevious = true;
    private AnalyticsSpringChannel _window = new(29, 0.0005, 0.005);
    private AnalyticsSpringChannel _lo = new(0, 1e-9, 1e-9);
    private AnalyticsSpringChannel _hi = new(1, 1e-9, 1e-9);
    private AnalyticsTickSet _ticks;
    private double _unit = 1;
    private int _scrub = -1;
    private int _paletteVersion = -1;
    private bool _hasData;
    private StreamGeometry? _currentLine;
    private StreamGeometry? _previousLine;
    private bool _linesDirty = true;
    private string _scrubFormat = "{0}: {1}. {2}: {3}";
    private string _scrubName = "Scrub";
    private string _summaryFormat = "{0}, last {1} days: {2} to {3}.";

    static MetricExplorerChart()
    {
        FocusableProperty.OverrideDefaultValue<MetricExplorerChart>(true);
    }

    public MetricExplorerChart()
    {
        // Function as well as decoration: the loop also finishes fades under reduced motion.
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        _offsets = new AnalyticsSpringArray(this);
        _offsets.Changed += OnOffsetsChanged;
        Cursor = new Cursor(StandardCursorType.Cross);
        ActualThemeVariantChanged += (_, _) => InvalidatePalette();
        ResourcesChanged += (_, _) => InvalidatePalette();
        for (var i = 0; i <= Rows; i++)
        {
            _slotText[i] = string.Empty;
            _slotPrevious[i] = string.Empty;
            _slotIn[i] = new AnalyticsTween(1);
            _slotOut[i] = new AnalyticsTween(0);
        }
        for (var i = 0; i < _levelAlpha.Length; i++)
        {
            _levelAlpha[i] = new AnalyticsTween(0);
        }
    }

    /// <summary>Raised when the inspected day changes (pointer, keys or code); null when cleared.</summary>
    public event Action<int?>? ScrubChanged;

    /// <summary>Geometry shared with the block (plot insets, chart heights).</summary>
    public MetricExplorerGeometry Geometry
    {
        get => _geometry;
        set
        {
            _geometry = value;
            _linesDirty = true;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>The inspected day (0 = first day of the range), or null.</summary>
    public int? ScrubIndex => _scrub >= 0 ? _scrub : null;

    /// <summary>The metric drawn.</summary>
    public MetricExplorerSeries? Series => _series;

    /// <summary>The number of days of the range drawn.</summary>
    public int Days => _days;

    /// <summary>True while anything animates (tests).</summary>
    internal bool IsMoving => _loop.IsActive || _offsets.IsMoving;

    /// <summary>The visible span in day intervals now (tests).</summary>
    internal double WindowIntervals => _window.Value;

    /// <summary>The y domain on screen now (tests).</summary>
    internal (double Lo, double Hi) Domain => (_lo.Value, _hi.Value);

    /// <summary>The y labels' target texts, bottom to top (tests).</summary>
    internal IReadOnlyList<string> AxisTexts => _slotText;

    /// <summary>The y tick set the chart heads to (tests).</summary>
    internal AnalyticsTickSet TargetTicks => _ticks;

    /// <summary>Opacity of an x label ladder now (tests).</summary>
    internal double LevelAlpha(MetricExplorerTickLevel level) => _levelAlpha[(int)level].Value;

    /// <summary>The text of the screen reader name, summary format and scrub format (set by the block).</summary>
    public void SetTexts(string scrubName, string scrubFormat, string summaryFormat)
    {
        _scrubName = scrubName;
        _scrubFormat = scrubFormat;
        _summaryFormat = summaryFormat;
        UpdateAutomation();
    }

    /// <summary>Whether the previous period's dashed line (and its scrub dot) is drawn.</summary>
    public void SetShowPrevious(bool show)
    {
        _showPrevious = show;
        _linesDirty = true;
        InvalidateVisual();
    }

    #region Data

    /// <summary>
    /// Shows a metric over a range. A different series than before morphs the lines point by point (the window stays);
    /// a different range moves the window, the y domain and the label ladders; the first call lands at once.
    /// </summary>
    public void SetData(MetricExplorerSeries series, int days, bool animate)
    {
        days = Math.Max(2, days);
        var level = ArcMotion.GetEffectiveLevel(this);
        var full = level == ArcMotionLevel.Full && animate && _hasData && this.IsAttachedToVisualTree();
        var seriesChanged = !ReferenceEquals(series, _series);
        var rangeChanged = days != _days;
        var old = _series;
        var oldLo = _lo.Value;
        var oldHi = _hi.Value;
        var oldWindow = _window.Value;
        var oldOffsets = _offsets.Count > 0 ? _offsets.Values.ToArray() : [];

        _series = series;
        _days = days;
        if (_scrub >= days)
        {
            _scrub = days - 1;
        }
        var (min, max) = MetricExplorerMath.WindowExtent(series.Values, days, _showPrevious);
        var (ticks, unit) = series.Axis(min, max);
        _ticks = ticks;
        _unit = unit;
        var targetLo = ticks.Ticks[0];
        var targetHi = ticks.Ticks[^1];
        var targetWindow = days - 1;
        // Rest thresholds relative to the axis span (values run from tiny rates to millions).
        var axisSpan = Math.Max(1e-12, targetHi - targetLo);
        _lo.RestDelta = _hi.RestDelta = axisSpan * 1e-5;
        _lo.RestSpeed = _hi.RestSpeed = axisSpan * 1e-4;

        if (!_hasData || !full)
        {
            _window.Jump(targetWindow);
            _lo.Jump(targetLo);
            _hi.Jump(targetHi);
            _offsets.Jump(new double[2 * OffsetCount(series)]);
        }
        else
        {
            if (seriesChanged)
            {
                // Keep every point where it is on screen: offset = (displayed) - (new base position), springing to zero.
                _lo.Jump(targetLo);
                _hi.Jump(targetHi);
                var k = OffsetCount(series);
                var offsets = new double[2 * k];
                if (old is not null)
                {
                    var oldSpan = Math.Max(1e-12, oldHi - oldLo);
                    var newSpan = Math.Max(1e-12, targetHi - targetLo);
                    var oldK = oldOffsets.Length / 2;
                    var shift = oldWindow + 1;
                    for (var i = 0; i < k; i++)
                    {
                        offsets[i] = Difference(old, series, i, 0, shift, targetWindow + 1, oldLo, oldSpan, targetLo, newSpan,
                            i < oldK ? oldOffsets[i] : 0);
                        offsets[k + i] = Difference(old, series, i, 1, shift, targetWindow + 1, oldLo, oldSpan, targetLo, newSpan,
                            i < oldK ? oldOffsets[oldK + i] : 0);
                    }
                }
                _offsets.Jump(offsets);
                _offsets.SetTargets(new double[2 * k]);
            }
            else if (rangeChanged)
            {
                _window.SpringTo(targetWindow, AnalyticsSprings.Smooth);
                _lo.SpringTo(targetLo, AnalyticsSprings.Smooth);
                _hi.SpringTo(targetHi, AnalyticsSprings.Smooth);
            }
            else
            {
                _lo.Jump(targetLo);
                _hi.Jump(targetHi);
                _window.Jump(targetWindow);
            }
        }

        UpdateLevels(rangeChanged || !_hasData || ChooseLevel() != CurrentLevel(), level);
        UpdateSlots(level, animate: _hasData && animate);
        _hasData = true;
        _linesDirty = true;
        UpdateAutomation();
        if (_window.IsMoving || _lo.IsMoving || _hi.IsMoving || AnyTween())
        {
            _loop.Start();
        }
        InvalidateVisual();
    }

    private static int OffsetCount(MetricExplorerSeries series) => Math.Max(1, series.Count);

    // Displayed normalized height of the old series at day k back minus where the new series puts it.
    private double Difference(MetricExplorerSeries old, MetricExplorerSeries series, int k, int line, double oldShift, double newShift,
        double oldLo, double oldSpan, double newLo, double newSpan, double oldOffset)
    {
        var oldValue = old.At(line == 0 ? k : k + oldShift);
        var newValue = series.At(line == 0 ? k : k + newShift);
        if (!double.IsFinite(oldValue) || !double.IsFinite(newValue))
        {
            return 0;
        }
        var displayed = (oldValue - oldLo) / oldSpan + oldOffset;
        var baseline = (newValue - newLo) / newSpan;
        return displayed - baseline;
    }

    private MetricExplorerTickLevel ChooseLevel() => MetricExplorerMath.ChooseLevel(_days, PlotRight() - PlotLeft());

    private MetricExplorerTickLevel CurrentLevel()
    {
        for (var i = _levelAlpha.Length - 1; i >= 0; i--)
        {
            if (_levelAlpha[i].Target > 0.5)
            {
                return (MetricExplorerTickLevel)i;
            }
        }
        return MetricExplorerTickLevel.Weekly;
    }

    private bool AnyTween()
    {
        for (var i = 0; i < _levelAlpha.Length; i++)
        {
            if (_levelAlpha[i].IsRunning)
            {
                return true;
            }
        }
        for (var i = 0; i <= Rows; i++)
        {
            if (_slotIn[i].IsRunning || _slotOut[i].IsRunning)
            {
                return true;
            }
        }
        return false;
    }

    // The x label ladders cross-fade: the old one leaves in 160ms (ease-out), the new one arrives in 240ms after 60ms.
    private void UpdateLevels(bool changed, ArcMotionLevel level)
    {
        var target = (int)ChooseLevel();
        for (var i = 0; i < _levelAlpha.Length; i++)
        {
            var want = i == target ? 1.0 : 0.0;
            if (level == ArcMotionLevel.Off || !_hasData || !this.IsAttachedToVisualTree())
            {
                _levelAlpha[i].Jump(want);
            }
            else if (Math.Abs(_levelAlpha[i].Target - want) > 1e-9)
            {
                if (level == ArcMotionLevel.Reduced)
                {
                    _levelAlpha[i].Start(want, ArcMotionPresets.Instant, AnalyticsEasings.EaseOut);
                }
                else if (want > 0)
                {
                    _levelAlpha[i].Start(1, ArcMotionPresets.Standard, AnalyticsEasings.EaseOut, TimeSpan.FromMilliseconds(60));
                }
                else
                {
                    _levelAlpha[i].Start(0, ArcMotionPresets.Fast, AnalyticsEasings.EaseOut);
                }
            }
        }
    }

    // Each y label slot keeps its place; only a changed text animates (old: fade out, new: fade in rising 3.6px), the
    // slots one after another from the bottom.
    private void UpdateSlots(ArcMotionLevel level, bool animate)
    {
        if (_series is null)
        {
            return;
        }
        for (var i = 0; i <= Rows; i++)
        {
            var text = _series.AxisText(_ticks.Ticks[i], _ticks.Step, _unit);
            if (text == _slotText[i])
            {
                continue;
            }
            if (!animate || level == ArcMotionLevel.Off || !this.IsAttachedToVisualTree())
            {
                _slotText[i] = text;
                _slotPrevious[i] = string.Empty;
                _slotIn[i].Jump(1);
                _slotOut[i].Jump(0);
                continue;
            }
            // A change in flight leaves the text that is on screen as the outgoing one.
            _slotPrevious[i] = _slotText[i].Length > 0 ? _slotText[i] : _slotPrevious[i];
            _slotText[i] = text;
            var delay = TimeSpan.FromMilliseconds(i * StaggerMs);
            _slotOut[i].Jump(Math.Clamp(_slotIn[i].Value, 0, 1));
            _slotIn[i].Jump(0);
            if (level == ArcMotionLevel.Reduced)
            {
                _slotOut[i].Start(0, ArcMotionPresets.Instant, AnalyticsEasings.EaseOut);
                _slotIn[i].Start(1, ArcMotionPresets.Instant, AnalyticsEasings.EaseOut);
            }
            else
            {
                _slotOut[i].Start(0, ArcMotionPresets.Fast, AnalyticsEasings.Standard, delay);
                _slotIn[i].Start(1, ArcMotionPresets.Standard, AnalyticsEasings.Enter, delay);
            }
        }
    }

    #endregion

    #region Frame loop

    private bool Advance(double dt)
    {
        var full = ArcMotion.IsFull(this);
        var moving = false;
        moving |= Step(ref _window, dt, full);
        moving |= Step(ref _lo, dt, full);
        moving |= Step(ref _hi, dt, full);
        for (var i = 0; i < _levelAlpha.Length; i++)
        {
            moving |= _levelAlpha[i].Advance(dt);
        }
        for (var i = 0; i <= Rows; i++)
        {
            moving |= _slotIn[i].Advance(dt);
            moving |= _slotOut[i].Advance(dt);
        }
        _linesDirty = true;
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

    private void OnOffsetsChanged()
    {
        _linesDirty = true;
        InvalidateVisual();
    }

    #endregion

    #region Scrub

    /// <summary>Inspects day <paramref name="index"/> of the range (null clears).</summary>
    public void SetScrub(int? index, bool notify = true)
    {
        var next = index is { } i ? Math.Clamp(i, 0, _days - 1) : -1;
        if (next == _scrub)
        {
            return;
        }
        _scrub = next;
        if (next < 0)
        {
        }
        UpdateAutomation();
        InvalidateVisual();
        if (notify)
        {
            ScrubChanged?.Invoke(ScrubIndex);
        }
    }

    private int IndexAt(double x)
    {
        var step = Step();
        var k = (int)Math.Round((PlotRight() - x) / step, MidpointRounding.AwayFromZero);
        return Math.Clamp(_days - 1 - k, 0, _days - 1);
    }

    private double Step() => (PlotRight() - PlotLeft()) / Math.Max(1e-6, _window.Value);

    private double PlotLeft() => _geometry.PlotInset.Left;

    private double PlotRight() => Math.Max(PlotLeft() + 1, Bounds.Width - _geometry.PlotInset.Right);

    private double PlotTop() => _geometry.PlotInset.Top;

    private double PlotBottom() => Math.Max(PlotTop() + 1, Bounds.Height - _geometry.PlotInset.Bottom);

    private bool InPlot(Point p) => p.X >= PlotLeft() - 1 && p.X <= PlotRight() + 1 && p.Y >= 0 && p.Y <= PlotBottom();

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_series is null)
        {
            return;
        }
        var p = e.GetPosition(this);
        if (InPlot(p))
        {
            SetScrub(IndexAt(p.X));
        }
        else if (_scrub >= 0)
        {
            SetScrub(null);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_scrub >= 0)
        {
            SetScrub(null);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_series is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        // A click focuses the slider (as the reference does) without the ring: the focus method is pointer.
        Focus(NavigationMethod.Pointer);
        var p = e.GetPosition(this);
        if (InPlot(p))
        {
            SetScrub(IndexAt(p.X));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        // Touch inspects while the finger is down and clears when it lifts.
        if (e.Pointer.Type == PointerType.Touch && _scrub >= 0)
        {
            SetScrub(null);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        // Tabbing in shows the last day at once.
        if (e.NavigationMethod == NavigationMethod.Tab && _scrub < 0 && _series is not null)
        {
            SetScrub(_days - 1);
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
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || _series is null)
        {
            return;
        }
        if (e.Key == Key.Escape)
        {
            if (_scrub >= 0)
            {
                // The first Escape only clears the inspection; the next one closes the chart.
                SetScrub(null);
                e.Handled = true;
            }
            return;
        }
        // The slider's keys: ← and ↓ go back a day, → and ↑ forward (the helper's ↑ goes back, so swap the vertical pair).
        var key = e.Key switch { Key.Up => Key.Right, Key.Down => Key.Left, _ => e.Key };
        if (AnalyticsKeyboard.Step(key, _scrub, _days) is { } next && e.Key is not (Key.PageUp or Key.PageDown))
        {
            SetScrub(next);
            e.Handled = true;
        }
    }

    #endregion

    #region Layout

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 640 : availableSize.Width;
        return new Size(width, MetricExplorerMath.ChartHeight(width, _geometry.ChartMinHeight, _geometry.ChartMaxHeight));
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _linesDirty = true;
        if (_hasData && this.IsAttachedToVisualTree() && MetricExplorerMath.ChooseLevel(_days, PlotRight() - PlotLeft()) != CurrentLevel())
        {
            // A resize changes the ladder at once (only a range change cross-fades it).
            UpdateLevels(true, ArcMotionLevel.Off);
            InvalidateVisual();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _offsets.Stop();
    }

    #endregion

    #region Automation

    private string ScrubValueText()
    {
        if (_series is null)
        {
            return string.Empty;
        }
        var i = _scrub >= 0 ? _scrub : _days - 1;
        var k = _days - 1 - i;
        var date = _series.DateAt(k).ToString("MMM d, yyyy", AnalyticsFormat.DefaultCulture);
        var previousDate = _series.DateAt(k + _days).ToString("MMM d, yyyy", AnalyticsFormat.DefaultCulture);
        return string.Format(AnalyticsFormat.DefaultCulture, _scrubFormat, date, _series.ValueText(_series.At(k)), previousDate,
            _series.ValueText(_series.At(k + _days)));
    }

    private void UpdateAutomation()
    {
        if (_series is null)
        {
            return;
        }
        AutomationProperties.SetName(this, $"{_scrubName} {_series.Title}");
        AutomationProperties.SetHelpText(this, ScrubValueText());
    }

    /// <summary>The one-sentence summary of the chart for screen readers.</summary>
    public string Summary()
    {
        if (_series is null)
        {
            return string.Empty;
        }
        return string.Format(AnalyticsFormat.DefaultCulture, _summaryFormat, _series.Title, _days, _series.ValueText(_series.At(_days - 1)),
            _series.ValueText(_series.At(0)));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ChartPeer(this);

    private sealed class ChartPeer(MetricExplorerChart owner) : ControlAutomationPeer(owner), IRangeValueProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override string? GetHelpTextCore() => owner.ScrubValueText();

        public bool IsReadOnly => false;

        public double Maximum => Math.Max(0, owner._days - 1);

        public double Minimum => 0;

        public double Value => owner._scrub >= 0 ? owner._scrub : owner._days - 1;

        public double SmallChange => 1;

        public double LargeChange => 1;

        public void SetValue(double value) => owner.SetScrub((int)Math.Round(value));
    }

    #endregion

    #region Render

    private void InvalidatePalette()
    {
        _paletteVersion = -1;
        _text.Clear();
        _tickText.Clear();
        _linesDirty = true;
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

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        EnsurePalette();
        if (!_palette.IsReady || _series is null || !_hasData)
        {
            return;
        }
        var body = new Typeface(_palette.BodyFont);
        var l = PlotLeft();
        var r = PlotRight();
        var t = PlotTop();
        var b = PlotBottom();
        var plotH = b - t;

        DrawGrid(context, l, r, t, plotH);
        DrawYLabels(context, body, t, plotH);
        DrawXLabels(context, body, l, r, size.Height);
        DrawLines(context, l, r, b, plotH);
        DrawScrub(context, body, l, r, t, b, plotH, size.Height);
        if (AnalyticsFocus.IsVisible(this))
        {
            AnalyticsFocus.Draw(context, _palette, new Rect(l, 0, r - l, b));
        }
    }

    private void DrawGrid(DrawingContext context, double l, double r, double t, double plotH)
    {
        for (var i = 0; i <= Rows; i++)
        {
            var y = AnalyticsPixels.Crisp(t + plotH * i / Rows);
            var pen = _palette.Pen(i == Rows ? _palette.BorderStrong : _palette.Border, 1, cap: PenLineCap.Flat);
            context.DrawLine(pen, new Point(l, y), new Point(r, y));
        }
    }

    private void DrawYLabels(DrawingContext context, Typeface body, double t, double plotH)
    {
        var right = _geometry.AxisLabelWidth;
        for (var i = 0; i <= Rows; i++)
        {
            // Slot i counts from the bottom; its label sits on grid row Rows - i.
            var y = t + plotH * (Rows - i) / Rows;
            DrawSlotText(context, body, _slotPrevious[i], right, y, _slotOut[i].Value, 0);
            DrawSlotText(context, body, _slotText[i], right, y, _slotIn[i].Value, 3.6 * (1 - _slotIn[i].Value));
        }
    }

    private void DrawSlotText(DrawingContext context, Typeface body, string text, double right, double y, double alpha, double rise)
    {
        if (text.Length == 0 || alpha < 0.004)
        {
            return;
        }
        var layout = _text.Get(text, body, 12, true);
        layout.Draw(context, _text.Brush(_palette.TextMuted, alpha), new Point(right - layout.Width, y - layout.Height / 2 + rise));
    }

    private string TickText(MetricExplorerTickLevel level, DateTime date)
    {
        var key = (long)(int)level * 4_000_000 + DateOnly.FromDateTime(date).DayNumber;
        if (!_tickText.TryGetValue(key, out var text))
        {
            text = MetricExplorerMath.TickLabel(level, date);
            _tickText[key] = text;
        }
        return text;
    }

    private void DrawXLabels(DrawingContext context, Typeface body, double l, double r, double height)
    {
        var step = Step();
        var reach = (int)Math.Ceiling(_window.Value) + 1;
        var end = _series!.End;
        for (var li = 0; li < _levelAlpha.Length; li++)
        {
            var groupAlpha = _levelAlpha[li].Value;
            if (groupAlpha < 0.004)
            {
                continue;
            }
            var level = (MetricExplorerTickLevel)li;
            for (var k = 0; k <= reach; k++)
            {
                var date = end.AddDays(-k);
                if (!MetricExplorerMath.IsTick(level, date))
                {
                    continue;
                }
                var x = r - k * step;
                var alpha = groupAlpha * MetricExplorerMath.EdgeFade(x, l, r);
                if (alpha < 0.004)
                {
                    continue;
                }
                var layout = _text.Get(TickText(level, date), body, 12, true);
                layout.Draw(context, _text.Brush(_palette.TextMuted, alpha), new Point(x - layout.Width / 2, height - 8 - layout.Height / 2 - 1));
            }
        }
    }

    // Normalized height (0 bottom, 1 top) of day k back on the line, with the morph offset.
    private double Norm(double value, int k, bool previous)
    {
        var span = _hi.Value - _lo.Value;
        if (!(span > 0))
        {
            span = 1;
        }
        var n = (value - _lo.Value) / span;
        var count = _offsets.Count / 2;
        if (count > 0 && k < count)
        {
            n += _offsets[previous ? count + k : k];
        }
        return n;
    }

    private void BuildLines(double l, double r, double b, double plotH)
    {
        var step = (r - l) / Math.Max(1e-6, _window.Value);
        var shift = _window.Value + 1;
        _currentLine = BuildLine(false, l, r, b, plotH, step, shift);
        _previousLine = _showPrevious ? BuildLine(true, l, r, b, plotH, step, shift) : null;
        _linesDirty = false;
    }

    private StreamGeometry? BuildLine(bool previous, double l, double r, double b, double plotH, double step, double shift)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        var began = false;
        var limit = l - 3;
        for (var k = 0; ; k++)
        {
            var value = previous ? _series!.At(k + shift) : _series!.At(k);
            if (!double.IsFinite(value))
            {
                break;
            }
            var x = r - k * step;
            var y = b - Norm(value, k, previous) * plotH;
            var point = new Point(x, y);
            if (!began)
            {
                ctx.BeginFigure(point, false);
                began = true;
            }
            else
            {
                ctx.LineTo(point);
            }
            if (x < limit)
            {
                break;
            }
        }
        if (!began)
        {
            return null;
        }
        ctx.EndFigure(false);
        return geometry;
    }

    private void DrawLines(DrawingContext context, double l, double r, double b, double plotH)
    {
        if (_linesDirty || _currentLine is null && _previousLine is null)
        {
            BuildLines(l, r, b, plotH);
        }
        using (context.PushClip(new Rect(l - 3, 0, r - l + 6, b + 6)))
        {
            if (_previousLine is not null)
            {
                context.DrawGeometry(null, _palette.Pen(_palette.TextMuted, 1.25, dash: 2, gap: 4), _previousLine);
            }
            if (_currentLine is not null)
            {
                context.DrawGeometry(null, _palette.Pen(_palette.Accent, 2), _currentLine);
            }
        }
        if (_scrub < 0)
        {
            var value = _series!.At(0);
            if (double.IsFinite(value))
            {
                var center = new Point(r, b - Norm(value, 0, false) * plotH);
                context.DrawEllipse(_palette.Brush(_palette.Accent), _palette.Pen(_palette.Surface, 2), center, 3.5, 3.5);
            }
        }
    }

    private void DrawScrub(DrawingContext context, Typeface body, double l, double r, double t, double b, double plotH, double height)
    {
        if (_scrub < 0)
        {
            return;
        }
        var k = _days - 1 - _scrub;
        var x = r - k * Step();
        var crisp = Math.Round(x, MidpointRounding.AwayFromZero) + 0.5;
        context.DrawLine(_palette.Pen(_palette.BorderStrong, 1, cap: PenLineCap.Flat), new Point(crisp, t), new Point(crisp, b));
        var current = _series!.At(k);
        var shift = _window.Value + 1;
        var previous = _showPrevious ? _series.At(k + shift) : double.NaN;
        if (double.IsFinite(previous))
        {
            context.DrawEllipse(_palette.Brush(_palette.Surface), _palette.Pen(_palette.TextMuted, 1.5),
                new Point(x, b - Norm(previous, k, true) * plotH), 3, 3);
        }
        if (double.IsFinite(current))
        {
            context.DrawEllipse(_palette.Brush(_palette.Accent), _palette.Pen(_palette.Surface, 2),
                new Point(x, b - Norm(current, k, false) * plotH), 4, 4);
        }
        // The date capsule sits on the x label row, centred on the day and kept inside the plot.
        var date = _series.DateAt(k).ToString("MMM d, yyyy", AnalyticsFormat.DefaultCulture);
        var label = _text.Get(date, body, 12, true);
        var width = label.Width + 16;
        var capsuleHeight = label.Height + 3;
        var left = Math.Clamp(x - width / 2, l, Math.Max(l, r - width));
        var top = height - 23;
        var rect = new Rect(left, top, width, capsuleHeight);
        context.DrawRectangle(_palette.Brush(_palette.SurfaceRaised), _palette.Pen(_palette.Border, 1), new RoundedRect(rect, capsuleHeight / 2));
        label.Draw(context, _palette.Brush(_palette.Foreground), new Point(left + 8, top + 1));
    }

    #endregion
}
