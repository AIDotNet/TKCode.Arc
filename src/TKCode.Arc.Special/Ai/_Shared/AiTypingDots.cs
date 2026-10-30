using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Three dots that breathe one after another while someone types (Arc's <c>breathe</c> keyframes): each dot rises to full
/// opacity and <see cref="Lift"/> px up at 30% of the <see cref="Period"/>, falls back by 60%, rests for the remaining
/// 40%, and starts <see cref="Stagger"/> after the one before it; opacity runs 0.35 to 1 on a smooth ease in and out.
/// Drawn in <see cref="Render"/> and driven by a Core <see cref="LiveLoop"/>, so the dots move only while on screen at
/// full motion (code-driven because three phase-shifted loops on one drawn control are not a ControlTheme transition);
/// otherwise they hold still at 0.6 opacity. The defaults are the agent handoff's measured values (1.2s, 160ms apart, 2px).
/// The control is decoration: name the element around it ("Jordan is typing").
/// </summary>
internal class AiTypingDots : Control
{
    /// <summary>The dots' color.</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<AiTypingDots, IBrush?>(nameof(Fill));

    /// <summary>Each dot's diameter.</summary>
    public static readonly StyledProperty<double> DotSizeProperty =
        AvaloniaProperty.Register<AiTypingDots, double>(nameof(DotSize), 6);

    /// <summary>The gap between two dots.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<AiTypingDots, double>(nameof(Gap), 4);

    /// <summary>How far a dot rises at its peak.</summary>
    public static readonly StyledProperty<double> LiftProperty =
        AvaloniaProperty.Register<AiTypingDots, double>(nameof(Lift), 2);

    /// <summary>One breath of a dot, in seconds.</summary>
    public const double DefaultPeriod = 1.2;

    /// <summary>The delay between two dots, in seconds.</summary>
    public const double DefaultStagger = 0.16;

    /// <summary>The opacity of the dots while they hold still.</summary>
    public const double RestOpacity = 0.6;

    private readonly LiveLoop _loop;
    private double _time;
    private bool _running;

    static AiTypingDots()
    {
        AffectsRender<AiTypingDots>(FillProperty);
        AffectsMeasure<AiTypingDots>(DotSizeProperty, GapProperty, LiftProperty);
    }

    public AiTypingDots()
    {
        IsHitTestVisible = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        _loop = new LiveLoop(this, seconds =>
        {
            _time += seconds;
            InvalidateVisual();
            return true;
        });
        _loop.RunningChanged += running =>
        {
            _running = running;
            InvalidateVisual();
        };
        _loop.Start();
    }

    /// <inheritdoc cref="FillProperty"/>
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    /// <inheritdoc cref="DotSizeProperty"/>
    public double DotSize { get => GetValue(DotSizeProperty); set => SetValue(DotSizeProperty, value); }

    /// <inheritdoc cref="GapProperty"/>
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <inheritdoc cref="LiftProperty"/>
    public double Lift { get => GetValue(LiftProperty); set => SetValue(LiftProperty, value); }

    /// <summary>One breath of a dot, in seconds.</summary>
    public double Period { get; set; } = DefaultPeriod;

    /// <summary>The delay between two dots, in seconds.</summary>
    public double Stagger { get; set; } = DefaultStagger;

    /// <summary>True while the dots move (tests).</summary>
    public bool IsBreathing => _running;

    /// <summary>
    /// Opacity and lift of dot <paramref name="index"/> <paramref name="time"/> seconds into the loop: rest until its stagger,
    /// then up to the peak at 30% of the period, down by 60%, still for the rest.
    /// </summary>
    public static (double Opacity, double Lift) Sample(double time, int index, double period = DefaultPeriod,
        double stagger = DefaultStagger, double lift = 2)
    {
        var local = time - index * stagger;
        if (local < 0)
        {
            return (0.35, 0);
        }
        var phase = local % period / period;
        double t;
        if (phase < 0.3)
        {
            t = phase / 0.3;
        }
        else if (phase < 0.6)
        {
            t = 1 - (phase - 0.3) / 0.3;
        }
        else
        {
            t = 0;
        }
        t = t * t * (3 - 2 * t);
        return (0.35 + 0.65 * t, lift * t);
    }

    protected override Size MeasureOverride(Size availableSize) => new(3 * DotSize + 2 * Gap, DotSize + Lift);

    public override void Render(DrawingContext context)
    {
        if (Fill is not { } fill)
        {
            return;
        }
        var r = DotSize / 2;
        var lift = Lift;
        for (var i = 0; i < 3; i++)
        {
            var (opacity, rise) = _running ? Sample(_time, i, Period, Stagger, lift) : (RestOpacity, 0);
            using (context.PushOpacity(opacity))
            {
                context.DrawEllipse(fill, null, new Point(r + i * (DotSize + Gap), lift + r - rise), r, r);
            }
        }
    }
}
