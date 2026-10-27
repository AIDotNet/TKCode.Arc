using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The detail view's trend chart (notes 3.5 and 4): 220 high, the plot is the width less a 56px label column. The current
/// period is a 72-point straight polyline (1.75 Accent) over an Accent 10% area down to 196; the prior period a dashed
/// (3 4) TextMuted line that fades in and out with Compare (240ms). Three gridlines at 28 / 103 / 178 carry the domain's
/// max, mean and min (no nice values); the domain springs (Smooth) when the range or Compare changes while the labels stay
/// put and show the interpolated numbers. Three dates sit under the plot (first, middle, last) and cross-fade (160ms) on a
/// range change. Pointing shows a cursor snapped to the nearest of the 72 points, a dot per line and a readout pinned to
/// the top whose centre is clamped 70 from the ends; the focused chart moves the cursor by day with ← / →, Home and End.
/// </summary>
/// <remarks>
/// Why code-driven: the 72 + 72 rows and the domain are coordinates inside one geometry that must move in lockstep and
/// keep their velocity when a range, Compare or width change interrupts them; the readout follows a cursor per frame.
/// </remarks>
internal sealed class KpiTrendChart : Control
{
    private static readonly AnalyticsReadoutStyle s_readoutStyle = AnalyticsReadoutStyle.Compact with
    {
        Padding = new Thickness(11, 8),
        TitleGap = 1,
        ValueLineHeight = 19.6,
        RowHeight = 17.8
    };

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(256);
    private readonly AnalyticsSpringArray _current;
    private readonly AnalyticsSpringArray _previous;
    private readonly AnalyticsReadout _readout = new(s_readoutStyle) { ExitDuration = ArcMotionPresets.Instant };
    private readonly LiveLoop _loop;
    private readonly Point[] _currentPoints = new Point[KpiDrilldownMath.DetailPoints];
    private readonly Point[] _previousPoints = new Point[KpiDrilldownMath.DetailPoints];
    private readonly DateSlot[] _dates = [new(), new(), new()];
    private AnalyticsSpringChannel _min = new(0, 1e-9, 1e-6);
    private AnalyticsSpringChannel _max = new(1, 1e-9, 1e-6);
    private AnalyticsTween _priorOpacity = new(1);
    private StreamGeometry? _currentLine;
    private StreamGeometry? _currentArea;
    private StreamGeometry? _previousLine;
    private Size _geometrySize;
    private bool _dirty = true;
    private bool _hasData;
    private double[] _daily = [];
    private double[] _dailyPrevious = [];
    private bool _hasPrevious;
    private bool _compare = true;
    private DateOnly _firstDate;
    private Func<double, string> _format = v => v.ToString("0.#", AnalyticsFormat.DefaultCulture);
    private string _dateFormat = "MMM d";
    private string _earlierFormat = "{0} one period back";
    private int _cursor = -1;
    private bool _pointerCursor;

    public KpiTrendChart()
    {
        Focusable = true;
        IsTabStop = true;
        ClipToBounds = false;
        Cursor = new Cursor(StandardCursorType.Cross);
        FocusAdorner = null;
        _current = new AnalyticsSpringArray(this, AnalyticsSprings.Smooth);
        _previous = new AnalyticsSpringArray(this, AnalyticsSprings.Smooth);
        _current.Changed += MarkDirty;
        _previous.Changed += MarkDirty;
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Reduced };
        ActualThemeVariantChanged += (_, _) => RefreshPalette();
        ResourcesChanged += (_, _) => RefreshPalette();
    }

    /// <summary>Raised when the cursor moves (index of the 72 points, −1 none; true when by keyboard).</summary>
    public event Action<int, bool>? CursorMoved;

    /// <summary>The plot's width (the control less the label column).</summary>
    public double PlotWidth => Math.Max(0, Bounds.Width - KpiDrilldownMath.AxisWidth);

    /// <summary>The cursor's point index (−1 none).</summary>
    public int CursorIndex => _cursor;

    /// <summary>The day under the cursor (−1 none).</summary>
    public int CursorDay => _cursor < 0 ? -1 : KpiDrilldownMath.DayOfIndex(_cursor, KpiDrilldownMath.DetailPoints, _daily.Length);

    /// <summary>The domain drawn now and its target (tests).</summary>
    internal (double Min, double Max) Domain => (_min.Value, _max.Value);

    internal (double Min, double Max) DomainTarget => (_min.Target, _max.Target);

    internal AnalyticsSpringArray CurrentRows => _current;

    internal AnalyticsSpringArray PreviousRows => _previous;

    internal double PriorOpacity => _priorOpacity.Value;

    internal AnalyticsReadout Readout => _readout;

    /// <summary>The y labels as drawn now (max, mean, min).</summary>
    internal string[] AxisLabels => [_format(_max.Value), _format((_min.Value + _max.Value) / 2), _format(_min.Value)];

    /// <summary>The date ticks as drawn now (first, middle, last).</summary>
    internal string[] DateLabels => [_dates[0].Text, _dates[1].Text, _dates[2].Text];

    /// <summary>The text the readout shows for the cursor (date, value, optional prior line).</summary>
    internal AnalyticsReadoutContent? ReadoutContent => _readout.Content;

    /// <summary>Shows a period. The first call (and <paramref name="animate"/> false) places everything at once.</summary>
    public void SetData(KpiPeriod period, Func<double, string> format, string dateFormat, string earlierFormat, bool compare, bool animate)
    {
        animate &= _hasData;
        _daily = period.Current;
        _dailyPrevious = period.Previous;
        _hasPrevious = period.HasPrevious;
        _firstDate = period.FirstDate;
        _format = format;
        _dateFormat = dateFormat;
        _earlierFormat = earlierFormat;
        var level = ArcMotion.GetEffectiveLevel(this);

        Span<double> current = stackalloc double[KpiDrilldownMath.DetailPoints];
        Span<double> previous = stackalloc double[KpiDrilldownMath.DetailPoints];
        KpiDrilldownMath.Resample(period.Current, current);
        KpiDrilldownMath.Resample(period.Previous, previous);
        if (!_hasPrevious)
        {
            current.CopyTo(previous);
        }
        if (animate)
        {
            _current.SetTargets(current);
            _previous.SetTargets(previous);
        }
        else
        {
            _current.Jump(current);
            _previous.Jump(previous);
        }

        var days = period.Days;
        SetDate(0, period.DateOf(0), animate, level);
        SetDate(1, period.DateOf(KpiDrilldownMath.MiddleTickDay(days)), animate, level);
        SetDate(2, period.DateOf(days - 1), animate, level);
        _dates[1].Fraction = days < 2 ? 0.5 : (double)KpiDrilldownMath.MiddleTickDay(days) / (days - 1);

        _compare = compare;
        _priorOpacity.Jump(compare && _hasPrevious ? 1 : 0);
        ApplyDomain(animate);
        _hasData = true;
        RefreshReadout(false);
        MarkDirty();
        StartLoop();
    }

    /// <summary>Shows or hides the prior period (the line fades 240ms; the domain springs).</summary>
    public void SetCompare(bool compare, bool animate)
    {
        if (_compare == compare)
        {
            return;
        }
        _compare = compare;
        var target = compare && _hasPrevious ? 1 : 0;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (animate && _hasData && level != ArcMotionLevel.Off && this.IsAttachedToVisualTree())
        {
            _priorOpacity.Start(target, ArcMotionPresets.Standard, AnalyticsEasings.Standard);
        }
        else
        {
            _priorOpacity.Jump(target);
        }
        ApplyDomain(animate);
        RefreshReadout(false);
        MarkDirty();
        StartLoop();
    }

    /// <summary>Hides the cursor and readout at once (the panel closes).</summary>
    public void ClearCursor()
    {
        _cursor = -1;
        _pointerCursor = false;
        _readout.Clear();
        InvalidateVisual();
    }

    private void ApplyDomain(bool animate)
    {
        var (min, max) = KpiDrilldownMath.Domain(_daily, _dailyPrevious, _compare && _hasPrevious);
        if (animate && _hasData && ArcMotion.IsFull(this) && this.IsAttachedToVisualTree())
        {
            _min.SpringTo(min, AnalyticsSprings.Smooth);
            _max.SpringTo(max, AnalyticsSprings.Smooth);
        }
        else
        {
            _min.Jump(min);
            _max.Jump(max);
        }
    }

    private void SetDate(int slot, DateOnly date, bool animate, ArcMotionLevel level)
    {
        var text = AnalyticsFormat.Date(date, _dateFormat);
        var d = _dates[slot];
        if (d.Text == text)
        {
            return;
        }
        if (animate && level != ArcMotionLevel.Off && d.Text.Length > 0)
        {
            d.Old = d.Text;
            d.OldFraction = d.Fraction;
            d.Fade.Jump(0);
            d.Fade.Start(1, ArcMotionPresets.Fast, AnalyticsEasings.EaseOut);
        }
        else
        {
            d.Old = null;
            d.Fade.Jump(1);
        }
        d.Text = text;
    }

    private void StartLoop()
    {
        if (_min.IsMoving || _max.IsMoving || _priorOpacity.IsRunning || _readout.IsMoving || _dates.Any(d => d.Fade.IsRunning))
        {
            _loop.Start();
        }
    }

    private bool Advance(double dt)
    {
        var moving = _min.Step(dt) | _max.Step(dt);
        moving |= _priorOpacity.Advance(dt);
        moving |= _readout.Advance(dt);
        foreach (var d in _dates)
        {
            moving |= d.Fade.Advance(dt);
            if (!d.Fade.IsRunning)
            {
                d.Old = null;
            }
        }
        MarkDirty();
        return moving;
    }

    private void MarkDirty()
    {
        _dirty = true;
        InvalidateVisual();
    }

    private void RefreshPalette()
    {
        _text.Clear();
        if (_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        InvalidateVisual();
    }

    // Input --------------------------------------------------------------------------------------------------------------

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Touch && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var p = e.GetPosition(this);
        if (p.Y < 0 || p.Y > Bounds.Height)
        {
            return;
        }
        _pointerCursor = true;
        MoveCursor(KpiDrilldownMath.NearestIndex(p.X, PlotWidth, KpiDrilldownMath.DetailPoints), false);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_pointerCursor)
        {
            _pointerCursor = false;
            HideCursor();
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (!_pointerCursor)
        {
            HideCursor();
        }
        InvalidateVisual();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || _daily.Length == 0)
        {
            return;
        }
        var days = _daily.Length;
        var current = _cursor < 0 ? -1 : CursorDay;
        if (AnalyticsKeyboard.Step(e.Key, current, days, vertical: false) is not { } day)
        {
            return;
        }
        e.Handled = true;
        _pointerCursor = false;
        MoveCursor(KpiDrilldownMath.IndexOfDay(day, KpiDrilldownMath.DetailPoints, days), true);
    }

    private void MoveCursor(int index, bool byKeyboard)
    {
        if (index == _cursor && _readout.IsShown)
        {
            return;
        }
        _cursor = index;
        RefreshReadout(true);
        InvalidateVisual();
        StartLoop();
        CursorMoved?.Invoke(index, byKeyboard);
    }

    private void HideCursor()
    {
        if (_cursor < 0)
        {
            return;
        }
        _cursor = -1;
        _readout.Hide(ArcMotion.GetEffectiveLevel(this));
        InvalidateVisual();
        StartLoop();
        CursorMoved?.Invoke(-1, false);
    }

    /// <summary>The readout text for a day: date, value, and the prior value with Compare on.</summary>
    internal AnalyticsReadoutContent ContentFor(int day)
    {
        day = Math.Clamp(day, 0, Math.Max(0, _daily.Length - 1));
        var title = AnalyticsFormat.Date(_firstDate.AddDays(day), _dateFormat);
        var value = _daily.Length > 0 ? _format(_daily[day]) : AnalyticsFormat.Missing;
        if (_compare && _hasPrevious && day < _dailyPrevious.Length && double.IsFinite(_dailyPrevious[day]))
        {
            var earlier = string.Format(AnalyticsFormat.DefaultCulture, _earlierFormat, _format(_dailyPrevious[day]));
            return new AnalyticsReadoutContent(title, value, [new AnalyticsReadoutRow(earlier, IsMuted: true)]);
        }
        return new AnalyticsReadoutContent(title, value);
    }

    private void RefreshReadout(bool show)
    {
        if (_cursor < 0 || !_hasData)
        {
            return;
        }
        if (!show && !_readout.IsShown)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        _readout.SetContent(ContentFor(CursorDay), level);
        if (!_palette.IsReady && this.IsAttachedToVisualTree())
        {
            _palette.Refresh(this);
        }
        var size = _palette.IsReady ? _readout.Measure(_text, _palette) : new Size(140, 70);
        var x = _cursor * PlotWidth / (KpiDrilldownMath.DetailPoints - 1);
        var center = KpiDrilldownMath.ReadoutCenter(x, PlotWidth);
        _readout.Show(new Point(center - size.Width / 2, 0), level);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            MarkDirty();
            RefreshReadout(false);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ChartPeer(this);

    // Render -------------------------------------------------------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var size = Bounds.Size;
        // The whole box takes the pointer (a self-drawn control is only hit where it paints).
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        if (AnalyticsFocus.IsVisible(this))
        {
            AnalyticsFocus.Draw(context, _palette, new Rect(size));
        }
        var w = PlotWidth;
        if (!_hasData || w <= 0)
        {
            return;
        }
        if (_dirty || _geometrySize != size || _currentLine is null)
        {
            Build(w);
            _geometrySize = size;
            _dirty = false;
        }

        var p = _palette;
        var grid = p.Pen(p.BorderSubtle, 1, cap: PenLineCap.Flat);
        var body = new Typeface(p.BodyFont);
        var muted = p.Brush(p.TextMuted);
        for (var i = 0; i < 3; i++)
        {
            var row = i == 0 ? KpiDrilldownMath.GridTop : i == 1 ? KpiDrilldownMath.GridMiddle : KpiDrilldownMath.GridBottom;
            var value = i == 0 ? _max.Value : i == 1 ? (_min.Value + _max.Value) / 2 : _min.Value;
            context.DrawLine(grid, new Point(0, row + 0.5), new Point(w, row + 0.5));
            var label = _text.Get(_format(value), body, 12, true);
            label.DrawAtBaseline(context, muted, w + KpiDrilldownMath.AxisLabelGap, row + 4);
        }

        using (context.PushClip(new Rect(-2, 0, w + 4, KpiDrilldownMath.AreaFloor)))
        {
            context.DrawGeometry(p.Brush(p.Accent, 0.10), null, _currentArea!);
            var prior = _priorOpacity.Value;
            if (prior > 0.001 && _hasPrevious && _previousLine is not null)
            {
                context.DrawGeometry(null, p.Pen(p.TextMuted, 1.5, prior, 3, 4, PenLineCap.Round), _previousLine);
            }
            context.DrawGeometry(null, p.Pen(p.Accent, 1.75), _currentLine!);
        }

        DrawDates(context, w, body);

        if (_cursor >= 0)
        {
            var x = Math.Round(_cursor * w / (KpiDrilldownMath.DetailPoints - 1)) + 0.5;
            context.DrawLine(p.Pen(p.BorderStrong, 1, cap: PenLineCap.Flat), new Point(x, KpiDrilldownMath.CursorTop), new Point(x, KpiDrilldownMath.AreaFloor));
            var cx = _cursor * w / (KpiDrilldownMath.DetailPoints - 1);
            var fill = p.Brush(p.SurfaceRaised);
            if (_compare && _hasPrevious && _priorOpacity.Value > 0.5)
            {
                context.DrawEllipse(fill, p.Pen(p.TextMuted, 1.75), new Point(cx, _previousPoints[_cursor].Y), 3, 3);
            }
            context.DrawEllipse(fill, p.Pen(p.Accent, 1.75), new Point(cx, _currentPoints[_cursor].Y), 3.5, 3.5);
        }
        _readout.Draw(context, _text, p);
    }

    private void DrawDates(DrawingContext context, double w, Typeface body)
    {
        var p = _palette;
        for (var i = 0; i < 3; i++)
        {
            var d = _dates[i];
            var fade = Math.Clamp(d.Fade.Value, 0, 1);
            if (d.Old is { } old && fade < 1)
            {
                DrawDate(context, i, old, i == 1 ? d.OldFraction : 0, w, body, p.Brush(p.TextMuted, 1 - fade));
            }
            if (d.Text.Length > 0)
            {
                DrawDate(context, i, d.Text, d.Fraction, w, body, p.Brush(p.TextMuted, fade));
            }
        }
    }

    private void DrawDate(DrawingContext context, int slot, string text, double fraction, double w, Typeface body, IBrush brush)
    {
        var t = _text.Get(text, body, 12, true);
        var x = slot switch
        {
            0 => 0,
            2 => w - t.Width,
            _ => fraction * w - t.Width / 2
        };
        t.DrawAtBaseline(context, brush, x, KpiDrilldownMath.DateBaseline);
    }

    private void Build(double w)
    {
        var n = Math.Min(_current.Count, _currentPoints.Length);
        if (n < 2)
        {
            return;
        }
        var min = _min.Value;
        var max = _max.Value;
        var cur = _current.Values;
        var prev = _previous.Values;
        for (var i = 0; i < n; i++)
        {
            var x = i * w / (n - 1);
            _currentPoints[i] = new Point(x, KpiDrilldownMath.PlotY(cur[i], min, max));
            _previousPoints[i] = new Point(x, KpiDrilldownMath.PlotY(i < prev.Length ? prev[i] : cur[i], min, max));
        }
        _currentLine = AnalyticsGeometry.Line(_currentPoints.AsSpan(0, n), AnalyticsCurve.Linear);
        _currentArea = AnalyticsGeometry.Area(_currentPoints.AsSpan(0, n), KpiDrilldownMath.AreaFloor, AnalyticsCurve.Linear);
        _previousLine = AnalyticsGeometry.Line(_previousPoints.AsSpan(0, n), AnalyticsCurve.Linear);
    }

    private sealed class DateSlot
    {
        public string Text = string.Empty;
        public string? Old;
        public double Fraction = 0.5;
        public double OldFraction = 0.5;
        public AnalyticsTween Fade = new(1);
    }

    private sealed class ChartPeer(KpiTrendChart owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
