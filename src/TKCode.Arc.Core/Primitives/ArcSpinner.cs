using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Styling;

namespace TKCode.Arc.Primitives;

/// <summary>
/// P21 loading ring: a 1.5px ring with one open quarter, spinning once every 0.7s (linear).
/// Spins only while visible and attached; static under reduced motion.
/// </summary>
public class ArcSpinner : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<ArcSpinner>();

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<ArcSpinner, double>(nameof(Thickness), 1.5);

    /// <summary>Current rotation in degrees (animated while spinning).</summary>
    public static readonly StyledProperty<double> AngleProperty =
        AvaloniaProperty.Register<ArcSpinner, double>(nameof(Angle));

    public double Angle
    {
        get => GetValue(AngleProperty);
        set => SetValue(AngleProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    private static readonly Animation s_spin = new()
    {
        Duration = TimeSpan.FromSeconds(0.7),
        IterationCount = IterationCount.Infinite,
        Children =
        {
            new KeyFrame { Cue = new Cue(0), Setters = { new Setter(AngleProperty, 0d) } },
            new KeyFrame { Cue = new Cue(1), Setters = { new Setter(AngleProperty, 360d) } }
        }
    };

    private CancellationTokenSource? _spinCts;

    static ArcSpinner()
    {
        AffectsRender<ArcSpinner>(ForegroundProperty, ThicknessProperty, AngleProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ArcSpinner>(false);
        WidthProperty.OverrideDefaultValue<ArcSpinner>(16);
        HeightProperty.OverrideDefaultValue<ArcSpinner>(16);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateSpin();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopSpin();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty || change.Property == ArcMotion.LevelProperty)
        {
            UpdateSpin();
        }
    }

    private void UpdateSpin()
    {
        var shouldSpin = IsVisible && TopLevel.GetTopLevel(this) is not null && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        if (shouldSpin && _spinCts is null)
        {
            _spinCts = new CancellationTokenSource();
            _ = s_spin.RunAsync(this, _spinCts.Token);
        }
        else if (!shouldSpin)
        {
            StopSpin();
        }
    }

    private void StopSpin()
    {
        _spinCts?.Cancel();
        _spinCts = null;
    }

    public override void Render(DrawingContext context)
    {
        var brush = Foreground;
        if (brush is null)
        {
            return;
        }
        var t = Thickness;
        var size = Math.Min(Bounds.Width, Bounds.Height);
        var radius = (size - t) / 2;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            // Three quarters of the ring; the right quarter is open (border-right-color: transparent).
            var start = new Point(center.X + radius * Math.Cos(Math.PI / 4), center.Y + radius * Math.Sin(Math.PI / 4));
            var end = new Point(center.X + radius * Math.Cos(-Math.PI / 4), center.Y + radius * Math.Sin(-Math.PI / 4));
            ctx.BeginFigure(start, false);
            ctx.ArcTo(end, new Size(radius, radius), 0, true, SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }
        using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) *
                                     Matrix.CreateRotation(Angle * Math.PI / 180.0) *
                                     Matrix.CreateTranslation(center.X, center.Y)))
        {
            context.DrawGeometry(null, new Pen(brush, t), geometry);
        }
    }
}
