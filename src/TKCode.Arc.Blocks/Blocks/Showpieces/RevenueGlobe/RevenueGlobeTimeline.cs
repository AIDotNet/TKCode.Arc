using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The day timeline of a <see cref="RevenueGlobe"/> (template part, a slider over 24 hours UTC): 96 bars of payments per
/// 15 minutes in three layers (a dashed baseline for the future, light bars up to the present, dark bars up to the
/// playhead) in the top 40px, and the playhead: a 2 × 48 line from 4px above the slider to 4px below the bars, under a
/// 10px knob (a 6px Accent dot in a 2px Surface ring) that grows ×1.3 while pressed. A press jumps, a drag follows
/// the pointer, a release replays on with the drag's speed; ← / → move 5 minutes (Shift: an hour), PageUp / PageDown
/// an hour, Home goes to 00:00 and End goes live.
/// </summary>
/// <remarks>
/// Code-driven motion: the knob's press scale is a <see cref="MotionValue{T}"/> tween because the knob is self-drawn
/// (no element a ControlTheme transition could reach).
/// </remarks>
internal sealed class RevenueGlobeTimeline : Control
{
    /// <summary>Height of the slider: 40 for the bars plus 4 under them (where the playhead line ends).</summary>
    public const double SliderHeight = 44;

    /// <summary>Height of the bars' view box (the bars stand on its bottom).</summary>
    public const double BarsHeight = 40;

    private const double ViewBoxWidth = 960;
    private const double BarWidth = 7;
    private const double BarStep = 10;
    private const double BarOffset = 1.5;

    private int[] _bins = new int[RevenueGlobeLedger.BinCount];
    private int _maxBin;
    private double _available;
    private double _played;
    private StreamGeometry? _bars;
    private StreamGeometry? _dashes;
    private double _geometryWidth = -1;
    private bool _colorsValid;
    private IBrush? _dashBrush;
    private IBrush? _baseBrush;
    private IBrush? _playedBrush;
    private IBrush? _accentBrush;
    private IBrush? _surfaceBrush;
    private IPen? _focusPen;
    private MotionValue<double>? _knobScale;
    private double _knob = 1;
    private bool _pressed;
    private readonly (long Ticks, double Time)[] _samples = new (long, double)[6];
    private int _sampleCount;

    static RevenueGlobeTimeline()
    {
        FocusableProperty.OverrideDefaultValue<RevenueGlobeTimeline>(true);
        FocusAdornerProperty.OverrideDefaultValue<RevenueGlobeTimeline>(null);
        CursorProperty.OverrideDefaultValue<RevenueGlobeTimeline>(new Cursor(StandardCursorType.SizeWestEast));
    }

    public RevenueGlobeTimeline()
    {
        ResourcesChanged += (_, _) => InvalidateColors();
        ActualThemeVariantChanged += (_, _) => InvalidateColors();
    }

    /// <summary>The block the timeline drives.</summary>
    public RevenueGlobe? Owner { get; set; }

    /// <summary>Share of the day up to the present (the light bars end there).</summary>
    public double AvailableFraction
    {
        get => _available;
        set
        {
            value = Math.Clamp(value, 0, 1);
            if (value != _available)
            {
                _available = value;
                InvalidateVisual();
            }
        }
    }

    /// <summary>Share of the day up to the playhead (the dark bars end there).</summary>
    public double PlayedFraction
    {
        get => _played;
        set
        {
            value = Math.Clamp(value, 0, 1);
            if (value != _played)
            {
                _played = value;
                InvalidateVisual();
            }
        }
    }

    /// <summary>Knob scale (tests): 1, or 1.3 while pressed.</summary>
    internal double KnobScale => _knob;

    /// <summary>Bar heights in the 40-unit view box (tests).</summary>
    internal double BarHeight(int bin) => RevenueGlobeLedger.BarHeight(_bins[bin], _maxBin);

    /// <summary>New payment counts per 15 minutes.</summary>
    public void SetBins(ReadOnlySpan<int> bins, int maxBin)
    {
        if (bins.Length != _bins.Length)
        {
            _bins = new int[bins.Length];
        }
        bins.CopyTo(_bins);
        _maxBin = maxBin;
        _bars = null;
        InvalidateVisual();
    }

    private void InvalidateColors()
    {
        _colorsValid = false;
        InvalidateVisual();
    }

    private void EnsureColors()
    {
        if (_colorsValid)
        {
            return;
        }
        _colorsValid = true;
        var foreground = ArcTokens.GetColor(this, ArcDesignTokenKind.Foreground);
        var surface = ArcTokens.GetColor(this, ArcDesignTokenKind.Surface);
        _dashBrush = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.Border));
        _baseBrush = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.BorderStrong));
        // Played bars: Foreground mixed 56.6% into Surface (the same mix in both themes).
        _playedBrush = new ImmutableSolidColorBrush(Oklch.MixOklab(surface, foreground, 0.566));
        _accentBrush = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.Accent));
        _surfaceBrush = new ImmutableSolidColorBrush(surface);
        _focusPen = new ImmutablePen(new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.FocusRing)), 2);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsFinite(availableSize.Width) ? availableSize.Width : 0, SliderHeight);

    private void EnsureGeometry(double width)
    {
        if (_bars is not null && _geometryWidth == width)
        {
            return;
        }
        _geometryWidth = width;
        var sx = width / ViewBoxWidth;
        var top = BarsHeight;
        _bars = new StreamGeometry();
        using (var g = _bars.Open())
        {
            for (var i = 0; i < _bins.Length; i++)
            {
                var h = RevenueGlobeLedger.BarHeight(_bins[i], _maxBin);
                if (h <= 0)
                {
                    continue;
                }
                var x = (BarOffset + BarStep * i) * sx;
                Rectangle(g, x, top - h, BarWidth * sx, h);
            }
        }
        _dashes = new StreamGeometry();
        using (var g = _dashes.Open())
        {
            for (var i = 0; i < _bins.Length; i++)
            {
                Rectangle(g, (BarOffset + BarStep * i) * sx, top - 1.5, BarWidth * sx, 1.5);
            }
        }
    }

    private static void Rectangle(StreamGeometryContext g, double x, double y, double w, double h)
    {
        g.BeginFigure(new Point(x, y), true);
        g.LineTo(new Point(x + w, y));
        g.LineTo(new Point(x + w, y + h));
        g.LineTo(new Point(x, y + h));
        g.EndFigure(true);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        if (width <= 0)
        {
            return;
        }
        EnsureColors();
        EnsureGeometry(width);
        // The whole slider takes pointer input.
        context.FillRectangle(Brushes.Transparent, new Rect(0, 0, width, SliderHeight));
        context.DrawGeometry(_dashBrush, null, _dashes!);
        var available = _available * width;
        if (available > 0)
        {
            using (context.PushClip(new Rect(0, 0, available, SliderHeight)))
            {
                context.DrawGeometry(_baseBrush, null, _bars!);
            }
        }
        var played = Math.Min(_played, _available) * width;
        if (played > 0)
        {
            using (context.PushClip(new Rect(0, 0, played, SliderHeight)))
            {
                context.DrawGeometry(_playedBrush, null, _bars!);
            }
        }
        // Playhead: a 2px line from 4 above the slider to 4 below the bars, and the knob on top: a 10px Surface disc
        // around a 6px Accent dot, so a thin gap separates the dot from the line.
        var x = _played * width;
        context.FillRectangle(_accentBrush!, new Rect(x - 1, -4, 2, SliderHeight + 4), 1);
        var center = new Point(x, -2);
        context.DrawEllipse(_surfaceBrush, null, center, 5 * _knob, 5 * _knob);
        context.DrawEllipse(_accentBrush, null, center, 3 * _knob, 3 * _knob);
        if (IsFocused && Classes.Contains(":focus-visible"))
        {
            // 2px ring, 2px outside the slider (square corners).
            context.DrawRectangle(null, _focusPen, new Rect(-3, -3, width + 6, SliderHeight + 6));
        }
    }

    // Input -----------------------------------------------------------------------------------------------------------------

    private double TimeAt(double x) => Math.Clamp(x / Math.Max(1, Bounds.Width), 0, 1) * RevenueGlobeLedger.DaySeconds;

    private void SetPressed(bool pressed)
    {
        if (_pressed == pressed)
        {
            return;
        }
        _pressed = pressed;
        _knobScale ??= CreateKnobScale();
        _ = _knobScale.AnimateTo(pressed ? 1.3 : 1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
    }

    private MotionValue<double> CreateKnobScale()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += v =>
        {
            _knob = v;
            InvalidateVisual();
        };
        return value;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Owner is null)
        {
            return;
        }
        e.Pointer.Capture(this);
        Focus(NavigationMethod.Pointer);
        SetPressed(true);
        var time = TimeAt(e.GetPosition(this).X);
        _sampleCount = 0;
        AddSample(time);
        Owner.BeginScrub(time);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pressed || Owner is null)
        {
            return;
        }
        var time = TimeAt(e.GetPosition(this).X);
        AddSample(time);
        Owner.ScrubTo(time);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        e.Pointer.Capture(null);
        EndPress(ReleaseSpeed());
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_pressed)
        {
            EndPress(0);
        }
    }

    private void EndPress(double speed)
    {
        SetPressed(false);
        Owner?.EndScrub(speed);
    }

    private void AddSample(double time)
    {
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_sampleCount == _samples.Length)
        {
            Array.Copy(_samples, 1, _samples, 0, _samples.Length - 1);
            _sampleCount--;
        }
        _samples[_sampleCount++] = (now, time);
    }

    /// <summary>Simulated seconds per real second over the last 100 ms of the drag.</summary>
    private double ReleaseSpeed()
    {
        if (_sampleCount < 2)
        {
            return 0;
        }
        var last = _samples[_sampleCount - 1];
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        var frequency = (double)System.Diagnostics.Stopwatch.Frequency;
        if ((now - last.Ticks) / frequency > 0.1)
        {
            return 0;
        }
        var first = last;
        for (var i = _sampleCount - 2; i >= 0; i--)
        {
            first = _samples[i];
            if ((last.Ticks - first.Ticks) / frequency >= 0.1)
            {
                break;
            }
        }
        var seconds = (last.Ticks - first.Ticks) / frequency;
        return seconds > 1e-3 ? (last.Time - first.Time) / seconds : 0;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || Owner is null)
        {
            return;
        }
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        switch (e.Key)
        {
            case Key.Left:
                Owner.StepTime(shift ? -3600 : -300);
                break;
            case Key.Right:
                Owner.StepTime(shift ? 3600 : 300);
                break;
            case Key.PageUp:
                Owner.StepTime(3600);
                break;
            case Key.PageDown:
                Owner.StepTime(-3600);
                break;
            case Key.Home:
                Owner.SeekToDayTime(0, fromKeyboard: true);
                break;
            case Key.End:
                Owner.GoLive();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RevenueGlobeTimelineAutomationPeer(this);
}

/// <summary>Screen-reader view of the timeline: a slider over the day so far, in seconds since 00:00 UTC.</summary>
internal sealed class RevenueGlobeTimelineAutomationPeer : ControlAutomationPeer, IRangeValueProvider
{
    public RevenueGlobeTimelineAutomationPeer(RevenueGlobeTimeline owner)
        : base(owner)
    {
    }

    private RevenueGlobeTimeline Timeline => (RevenueGlobeTimeline)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

    protected override string? GetNameCore() => AutomationProperties.GetName(Owner);

    public bool IsReadOnly => false;

    public double Minimum => 0;

    public double Maximum => Timeline.Owner?.DaySecondsNow ?? 0;

    public double Value => Timeline.Owner?.DaySecondsPlayhead ?? 0;

    public double LargeChange => 3600;

    public double SmallChange => 300;

    public void SetValue(double value) => Timeline.Owner?.SeekToDayTime(value, fromKeyboard: true);
}
