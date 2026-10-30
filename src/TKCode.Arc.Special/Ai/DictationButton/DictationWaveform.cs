using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The pill's live waveform, drawn by one control. It fits as many rounded bars as its width holds with an even gap,
/// centred so no bar is ever clipped. A level sample enters on the right every 60ms (a fixed ring buffer) and each bar eases
/// toward its sample every frame, so heights glide without steps. When <see cref="IsLive"/> turns off, the bars settle in
/// place to a row of dots and the frame loop stops. Reduced motion: the bars stop travelling and pulse in place with the
/// voice. Motion off: a still row of dots. Frames allocate nothing. Decorative: hidden from assistive tech.
/// </summary>
internal sealed class DictationWaveform : Control
{
    /// <summary>The ring holds more samples than any bar count the control draws.</summary>
    internal const int Capacity = 192;

    /// <summary>A sample enters every 60ms.</summary>
    internal const double SampleInterval = 0.06;

    /// <summary>The time constant of the bars' easing (seconds).</summary>
    private const double EaseTime = 0.07;

    /// <summary>The bars' color.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<DictationWaveform, IBrush?>(nameof(Foreground));

    /// <summary>A bar's width.</summary>
    public static readonly StyledProperty<double> BarWidthProperty =
        AvaloniaProperty.Register<DictationWaveform, double>(nameof(BarWidth), 3);

    /// <summary>The gap between bars.</summary>
    public static readonly StyledProperty<double> BarGapProperty =
        AvaloniaProperty.Register<DictationWaveform, double>(nameof(BarGap), 2);

    /// <summary>The smallest bar: a dot.</summary>
    public static readonly StyledProperty<double> BarMinHeightProperty =
        AvaloniaProperty.Register<DictationWaveform, double>(nameof(BarMinHeight), 3);

    /// <summary>Whether audio flows: bars follow the level, otherwise they settle to dots.</summary>
    public static readonly StyledProperty<bool> IsLiveProperty =
        AvaloniaProperty.Register<DictationWaveform, bool>(nameof(IsLive));

    private readonly double[] _samples = new double[Capacity];
    private readonly double[] _shown = new double[Capacity];
    private readonly LiveLoop _loop;
    private int _head;
    private double _clock;
    private double _smoothed;

    static DictationWaveform()
    {
        AffectsRender<DictationWaveform>(ForegroundProperty, BarWidthProperty, BarGapProperty, BarMinHeightProperty);
        FocusableProperty.OverrideDefaultValue<DictationWaveform>(false);
    }

    public DictationWaveform()
    {
        IsHitTestVisible = false;
        Avalonia.Automation.AutomationProperties.SetAccessibilityView(this, Avalonia.Automation.AccessibilityView.Raw);
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        _loop.RunningChanged += _ => InvalidateVisual();
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public double BarWidth { get => GetValue(BarWidthProperty); set => SetValue(BarWidthProperty, value); }

    public double BarGap { get => GetValue(BarGapProperty); set => SetValue(BarGapProperty, value); }

    public double BarMinHeight { get => GetValue(BarMinHeightProperty); set => SetValue(BarMinHeightProperty, value); }

    public bool IsLive { get => GetValue(IsLiveProperty); set => SetValue(IsLiveProperty, value); }

    /// <summary>Where the level comes from (read once per frame, never stored as a list).</summary>
    internal Func<double>? LevelSource { get; set; }

    /// <summary>How many bars the current width holds.</summary>
    internal int BarCount => Count(Bounds.Width);

    /// <summary>The frame loop runs (tests).</summary>
    internal bool IsRunning => _loop.IsRunning;

    /// <summary>The height fraction (0 to 1) bar <paramref name="fromRight"/> shows, 0 being the newest at the right (tests).</summary>
    internal double Shown(int fromRight) => _shown[fromRight];

    /// <summary>True when every bar rests as a dot.</summary>
    internal bool IsSettled
    {
        get
        {
            for (var i = 0; i < Capacity; i++)
            {
                if (_shown[i] > 0.002)
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>Advances the bars by <paramref name="dt"/> seconds (the frame loop calls it; tests may too).</summary>
    internal bool Step(double dt)
    {
        var live = IsLive;
        var level = live ? Shape(LevelSource?.Invoke() ?? 0) : 0;
        var travels = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        var count = Math.Min(Capacity, BarCount);
        if (live && travels)
        {
            _clock += dt;
            while (_clock >= SampleInterval)
            {
                _clock -= SampleInterval;
                _smoothed += (level - _smoothed) * 0.65;
                _head = (_head + 1) % Capacity;
                _samples[_head] = _smoothed;
            }
        }
        else
        {
            _smoothed += (level - _smoothed) * Math.Min(1, dt / 0.05);
        }
        var k = 1 - Math.Exp(-dt / EaseTime);
        var settled = true;
        for (var r = 0; r < Capacity; r++)
        {
            double target;
            if (!live || r >= count)
            {
                target = 0;
            }
            else if (travels)
            {
                target = _samples[(_head - r + Capacity) % Capacity];
            }
            else
            {
                target = _smoothed * Profile(r, count);
            }
            _shown[r] += (target - _shown[r]) * k;
            if (Math.Abs(_shown[r] - target) > 0.002 || _shown[r] > 0.002)
            {
                settled = false;
            }
        }
        InvalidateVisual();
        if (!live && settled)
        {
            Array.Clear(_samples);
            Array.Clear(_shown);
            _smoothed = 0;
            _clock = 0;
            return false;
        }
        return true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsLiveProperty)
        {
            if (IsLive || !IsSettled)
            {
                _loop.Start();
            }
            else
            {
                _loop.Stop();
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (!IsLive)
        {
            Array.Clear(_samples);
            Array.Clear(_shown);
            _loop.Stop();
        }
    }

    public override void Render(DrawingContext context)
    {
        var brush = Foreground;
        if (brush is null)
        {
            return;
        }
        var width = Bounds.Width;
        var height = Bounds.Height;
        var bar = BarWidth;
        var pitch = bar + BarGap;
        var count = Math.Min(Capacity, Count(width));
        if (count <= 0 || height <= 0)
        {
            return;
        }
        var used = count * pitch - BarGap;
        var x0 = Math.Round((width - used) / 2);
        var min = Math.Min(BarMinHeight, height);
        var radius = bar / 2;
        for (var i = 0; i < count; i++)
        {
            var value = _shown[count - 1 - i];
            var h = Math.Max(min, Math.Min(1, value) * height);
            var rect = new Rect(x0 + i * pitch, (height - h) / 2, bar, h);
            context.DrawRectangle(brush, null, rect, radius, radius);
        }
    }

    private int Count(double width)
    {
        var pitch = BarWidth + BarGap;
        if (pitch <= 0 || width <= 0)
        {
            return 0;
        }
        return Math.Max(0, (int)Math.Floor((width + BarGap) / pitch));
    }

    /// <summary>Speech sits low on a linear scale; lift quiet levels a little so the bars read.</summary>
    private static double Shape(double level) => Math.Clamp(Math.Pow(Math.Clamp(level, 0, 1), 0.8) * 1.1, 0, 1);

    /// <summary>The in-place pulse (reduced motion): a soft hump across the bars with a fixed ripple.</summary>
    internal static double Profile(int fromRight, int count)
    {
        if (count <= 1)
        {
            return 1;
        }
        var x = fromRight / (double)(count - 1);
        var hump = 0.45 + 0.55 * Math.Sin(Math.PI * x);
        var ripple = 0.8 + 0.2 * Math.Sin(fromRight * 2.3);
        return hump * ripple;
    }
}
