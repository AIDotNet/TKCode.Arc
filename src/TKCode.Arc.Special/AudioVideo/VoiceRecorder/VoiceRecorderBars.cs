using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The static waveform of a bubble or of the review, drawn by one control: every bar twice, at 28% of the foreground and
/// then, clipped to <see cref="Progress"/>, at full strength, so the played part fills in from the left without a layout.
/// A bubble's bars are 3px with 2px gaps (they narrow when the bubble is squeezed); the review's
/// <see cref="FlexibleCount"/> bars share the width at 1.5 to 3px each, and when even 1.5px bars do not fit, fewer bars are
/// drawn (Arc clips them instead). A press anywhere on the bars seeks there (no drag). Decorative for assistive tech.
/// </summary>
internal sealed class VoiceRecorderBars : Control
{
    public const double Gap = 2;
    public const double BarWidth = 3;
    public const double MinFlexibleBar = 1.5;
    public const double UnplayedOpacity = 0.28;

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<VoiceRecorderBars, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<VoiceRecorderBars, double>(nameof(Progress));

    private IReadOnlyList<double> _peaks = [];
    private double[] _fitted = [];
    private int _fittedFor = -1;

    static VoiceRecorderBars()
    {
        AffectsRender<VoiceRecorderBars>(ForegroundProperty, ProgressProperty);
        FocusableProperty.OverrideDefaultValue<VoiceRecorderBars>(false);
    }

    public VoiceRecorderBars()
    {
        AutomationProperties.SetAccessibilityView(this, Avalonia.Automation.AccessibilityView.Raw);
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>How much has played (0..1).</summary>
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    /// <summary>The bar heights (0..1 of the control's height).</summary>
    public IReadOnlyList<double> Peaks
    {
        get => _peaks;
        set
        {
            _peaks = value ?? [];
            _fittedFor = -1;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>The review's mode: bars stretch between 1.5 and 3px to fill the width; otherwise bubble bars of 3px.</summary>
    public bool FlexibleCount { get; init; }

    /// <summary>A press asked to seek to this fraction (0..1).</summary>
    public event Action<double>? SeekRequested;

    /// <summary>The bars drawn at <paramref name="width"/>: their count, width and the extent they cover (tests).</summary>
    internal (int Count, double Bar, double Extent) Layout(double width) => Fit(_peaks.Count, width, FlexibleCount);

    /// <summary>The bar layout for <paramref name="count"/> bars in <paramref name="width"/>.</summary>
    internal static (int Count, double Bar, double Extent) Fit(int count, double width, bool flexible)
    {
        if (count <= 0 || width <= 0)
        {
            return (0, 0, 0);
        }
        var bar = (width - Gap * (count - 1)) / count;
        if (flexible)
        {
            if (bar < MinFlexibleBar)
            {
                // Fewer bars rather than bars cut off at the right.
                count = Math.Max(1, (int)Math.Floor((width + Gap) / (MinFlexibleBar + Gap)));
                bar = MinFlexibleBar;
            }
            bar = Math.Min(BarWidth, bar);
        }
        else
        {
            bar = Math.Clamp(bar, 0.5, BarWidth);
        }
        return (count, bar, count * bar + (count - 1) * Gap);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var n = _peaks.Count;
        var natural = n <= 0 ? 0 : n * BarWidth + (n - 1) * Gap;
        if (FlexibleCount)
        {
            return new Size(0, 0);
        }
        return new Size(double.IsInfinity(availableSize.Width) ? natural : Math.Min(natural, availableSize.Width), 0);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var extent = Layout(Bounds.Width).Extent;
        if (extent <= 0)
        {
            return;
        }
        var x = e.GetPosition(this).X;
        SeekRequested?.Invoke(Math.Clamp(x / extent, 0, 1));
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        // The whole box takes presses.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var brush = Foreground;
        var (count, bar, extent) = Layout(width);
        if (brush is null || count <= 0 || height <= 0)
        {
            return;
        }
        var peaks = Bars(count);
        using (context.PushOpacity(UnplayedOpacity))
        {
            Draw(context, brush, peaks, count, bar, height);
        }
        var progress = Math.Clamp(Progress, 0, 1);
        if (progress <= 0)
        {
            return;
        }
        using (context.PushClip(new Rect(0, 0, extent * progress, height)))
        {
            Draw(context, brush, peaks, count, bar, height);
        }
    }

    private IReadOnlyList<double> Bars(int count)
    {
        if (count == _peaks.Count)
        {
            return _peaks;
        }
        if (_fittedFor != count)
        {
            _fitted = VoiceRecorderPeaks.Resample(_peaks, count);
            _fittedFor = count;
        }
        return _fitted;
    }

    private static void Draw(DrawingContext context, IBrush brush, IReadOnlyList<double> peaks, int count, double bar, double height)
    {
        var radius = Math.Min(2, bar / 2);
        for (var i = 0; i < count; i++)
        {
            var h = Math.Clamp(peaks[i], 0, 1) * height;
            var r = Math.Min(radius, h / 2);
            context.DrawRectangle(brush, null, new Rect(i * (bar + Gap), (height - h) / 2, bar, h), r, r);
        }
    }
}

/// <summary>
/// The live waveform of the recording bar. Every 90ms sample of the take becomes a 3px bar that enters at the right edge
/// and travels left at 55.56px/s (5px per sample), growing linearly from 3px to its height over about 110ms; bars fade to
/// 25% across the left quarter. Reduced motion and Off: the bars step left one place per sample and appear at full height.
/// The recorder sets the take's clock (seconds) every frame and owns the samples. Frames allocate nothing.
/// </summary>
internal sealed class VoiceRecorderLiveWave : Control
{
    public const double Pitch = 5;
    public const double Bar = 3;
    public const double MinBarHeight = 3;
    public const double Interval = 0.09;
    public const double GrowDelay = 0.017;
    public const double GrowTime = 0.11;
    public const double FadeShare = 0.25;
    public const double FadeFloor = 0.25;

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<VoiceRecorderLiveWave, IBrush?>(nameof(Foreground));

    private double[] _samples = [];
    private int _count;
    private double _time;

    static VoiceRecorderLiveWave()
    {
        AffectsRender<VoiceRecorderLiveWave>(ForegroundProperty);
    }

    public VoiceRecorderLiveWave()
    {
        IsHitTestVisible = false;
        Focusable = false;
        AutomationProperties.SetAccessibilityView(this, Avalonia.Automation.AccessibilityView.Raw);
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>Whether bars travel smoothly and grow (Full) or step (Reduced, Off).</summary>
    public bool Smooth { get; set; } = true;

    /// <summary>Points at the take's samples (0..1, of which <paramref name="count"/> are recorded) and its clock.</summary>
    public void Update(double[] samples, int count, double time)
    {
        _samples = samples;
        _count = count;
        _time = time;
        InvalidateVisual();
    }

    /// <summary>Clears the bars (a new take starts empty).</summary>
    public void Reset()
    {
        _count = 0;
        _time = 0;
        InvalidateVisual();
    }

    /// <summary>The centre x of sample <paramref name="index"/> at <paramref name="time"/> in a wave <paramref name="width"/> wide.</summary>
    internal static double CentreX(int index, double time, double width, bool smooth, int newest)
    {
        var right = width - Bar / 2;
        return smooth ? right - (time - index * Interval) * (Pitch / Interval) : right - (newest - index) * Pitch;
    }

    /// <summary>The drawn height of a sample born <paramref name="age"/> seconds ago whose full height is <paramref name="target"/>.</summary>
    internal static double GrownHeight(double target, double age, bool smooth)
    {
        target = Math.Max(MinBarHeight, target);
        if (!smooth)
        {
            return target;
        }
        var t = Math.Clamp((age - GrowDelay) / GrowTime, 0, 1);
        return MinBarHeight + (target - MinBarHeight) * t;
    }

    /// <summary>The opacity of a bar whose left edge is at <paramref name="left"/>.</summary>
    internal static double Alpha(double left, double width) =>
        width <= 0 ? 1 : Math.Clamp(left / (FadeShare * width), FadeFloor, 1);

    public override void Render(DrawingContext context)
    {
        var brush = Foreground;
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (brush is null || _count <= 0 || width <= 0 || height <= 0)
        {
            return;
        }
        var newest = _count - 1;
        var scale = height - 4;
        using (context.PushClip(new Rect(Bounds.Size)))
        {
            for (var i = newest; i >= 0; i--)
            {
                var x = CentreX(i, _time, width, Smooth, newest);
                if (x < -Bar)
                {
                    break;
                }
                var level = i < _samples.Length ? _samples[i] : VoiceRecorderPeaks.Silence;
                var h = GrownHeight(level * scale, _time - i * Interval, Smooth);
                var left = x - Bar / 2;
                var alpha = Alpha(left, width);
                var rect = new Rect(left, (height - h) / 2, Bar, h);
                if (alpha >= 0.999)
                {
                    context.DrawRectangle(brush, null, rect, Bar / 2, Bar / 2);
                }
                else
                {
                    using (context.PushOpacity(alpha))
                    {
                        context.DrawRectangle(brush, null, rect, Bar / 2, Bar / 2);
                    }
                }
            }
        }
    }
}
