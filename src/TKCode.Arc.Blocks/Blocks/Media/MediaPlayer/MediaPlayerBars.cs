using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The "playing" indicator of the current playlist row (Arc Media player, design reference): three 2×12 bars standing on
/// their bottom edge, each sweeping between 30% and full height and back (900 ms each way, in-out curve), a third of a
/// sweep apart. Self-drawn on a <see cref="LiveLoop"/>, so it only runs while shown, on screen and at full motion; under
/// reduced motion (or before the loop runs) the bars rest at 60%.
/// </summary>
internal sealed class MediaPlayerBars : Control
{
    /// <summary>The bars' color.</summary>
    public static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<MediaPlayerBars, IBrush?>(nameof(Brush));

    /// <summary>A bar's width (2).</summary>
    public static readonly StyledProperty<double> BarWidthProperty =
        AvaloniaProperty.Register<MediaPlayerBars, double>(nameof(BarWidth), 2);

    /// <summary>A bar's full height (12).</summary>
    public static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<MediaPlayerBars, double>(nameof(BarHeight), 12);

    /// <summary>Between the bars (2).</summary>
    public static readonly StyledProperty<double> BarGapProperty =
        AvaloniaProperty.Register<MediaPlayerBars, double>(nameof(BarGap), 2);

    /// <summary>One sweep (900 ms).</summary>
    public static readonly StyledProperty<TimeSpan> PeriodProperty =
        AvaloniaProperty.Register<MediaPlayerBars, TimeSpan>(nameof(Period), TimeSpan.FromMilliseconds(900));

    private const double Low = 0.3;
    private const double Rest = 0.6;
    private static readonly Easing s_curve = ArcEasings.InOut;
    private static readonly double[] s_phases = [0, 0.3, 0.6];

    private readonly LiveLoop _loop;
    private double _time;

    public MediaPlayerBars()
    {
        _loop = new LiveLoop(this, Step);
        _loop.RunningChanged += _ => InvalidateVisual();
    }

    static MediaPlayerBars()
    {
        AffectsMeasure<MediaPlayerBars>(BarWidthProperty, BarHeightProperty, BarGapProperty);
        AffectsRender<MediaPlayerBars>(BrushProperty);
    }

    /// <inheritdoc cref="BrushProperty"/>
    public IBrush? Brush { get => GetValue(BrushProperty); set => SetValue(BrushProperty, value); }

    /// <inheritdoc cref="BarWidthProperty"/>
    public double BarWidth { get => GetValue(BarWidthProperty); set => SetValue(BarWidthProperty, value); }

    /// <inheritdoc cref="BarHeightProperty"/>
    public double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }

    /// <inheritdoc cref="BarGapProperty"/>
    public double BarGap { get => GetValue(BarGapProperty); set => SetValue(BarGapProperty, value); }

    /// <inheritdoc cref="PeriodProperty"/>
    public TimeSpan Period { get => GetValue(PeriodProperty); set => SetValue(PeriodProperty, value); }

    /// <summary>Whether the loop is drawing frames now (false when hidden, off screen or under reduced motion).</summary>
    internal bool IsLooping => _loop.IsRunning;

    protected override Size MeasureOverride(Size availableSize) =>
        new(BarWidth * s_phases.Length + BarGap * (s_phases.Length - 1), BarHeight);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _loop.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _loop.Stop();
    }

    public override void Render(DrawingContext context)
    {
        if (Brush is not { } brush)
        {
            return;
        }
        var period = Math.Max(0.001, Period.TotalSeconds);
        var height = BarHeight;
        var bottom = Bounds.Height;
        for (var i = 0; i < s_phases.Length; i++)
        {
            var scale = _loop.IsRunning ? ScaleAt(_time + s_phases[i], period) : Rest;
            var h = height * scale;
            var x = i * (BarWidth + BarGap);
            context.DrawRectangle(brush, null, new RoundedRect(new Rect(x, bottom - h, BarWidth, h), BarWidth / 2));
        }
    }

    /// <summary>The height at a time: up for one period, down for the next (CSS <c>alternate</c>).</summary>
    internal static double ScaleAt(double seconds, double period)
    {
        var cycle = seconds / period;
        var whole = Math.Floor(cycle);
        var t = cycle - whole;
        var progress = s_curve.Ease(((long)whole & 1) == 0 ? t : 1 - t);
        return Low + (1 - Low) * progress;
    }

    private bool Step(double elapsed)
    {
        _time += elapsed;
        InvalidateVisual();
        return true;
    }
}
