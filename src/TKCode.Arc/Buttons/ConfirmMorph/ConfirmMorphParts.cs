using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>A ring that drains with the countdown (track plus a remaining arc that starts at the top).</summary>
internal sealed class ConfirmMorphRing : Control
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ConfirmMorphRing, double>(nameof(Progress), 1);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<ConfirmMorphRing, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<ConfirmMorphRing, IBrush?>(nameof(FillBrush));

    /// <summary>Remaining fraction, 1 to 0.</summary>
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    private const double Radius = 6.25;
    private const double Thickness = 1.75;
    private static readonly double Circumference = 2 * Math.PI * Radius;

    private readonly AvaloniaList<double> _dashes = new() { 0, 0 };
    private readonly DashStyle _dash;
    private Pen? _trackPen;
    private Pen? _fillPen;

    public ConfirmMorphRing()
    {
        _dash = new DashStyle(_dashes, 0);
        Width = 16;
        Height = 16;
        IsHitTestVisible = false;
        UpdateDash();
    }

    static ConfirmMorphRing()
    {
        AffectsRender<ConfirmMorphRing>(ProgressProperty, TrackBrushProperty, FillBrushProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ProgressProperty)
        {
            UpdateDash();
        }
        else if (change.Property == TrackBrushProperty)
        {
            _trackPen = TrackBrush is { } track ? new Pen(track, Thickness) : null;
        }
        else if (change.Property == FillBrushProperty)
        {
            _fillPen = FillBrush is { } fill ? new Pen(fill, Thickness, _dash, PenLineCap.Round) : null;
        }
    }

    /// <summary>Dash lengths are in units of the pen thickness: the visible dash is the remaining arc.</summary>
    private void UpdateDash()
    {
        var progress = Math.Clamp(Progress, 0, 1);
        _dashes[0] = progress * Circumference / Thickness;
        _dashes[1] = 2 * Circumference / Thickness;
    }

    public override void Render(DrawingContext context)
    {
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 16.0;
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            if (_trackPen is not null)
            {
                context.DrawEllipse(null, _trackPen, new Point(8, 8), Radius, Radius);
            }
            if (_fillPen is not null && Progress > 0.001)
            {
                using (context.PushTransform(Matrix.CreateTranslation(-8, -8) * Matrix.CreateRotation(-Math.PI / 2) * Matrix.CreateTranslation(8, 8)))
                {
                    context.DrawEllipse(null, _fillPen, new Point(8, 8), Radius, Radius);
                }
            }
        }
    }
}

/// <summary>A success disc that pops in with a tick drawing across it, the moment the action lands.</summary>
internal sealed class ConfirmMorphCheck : Control
{
    public static readonly StyledProperty<IBrush?> DiscBrushProperty =
        AvaloniaProperty.Register<ConfirmMorphCheck, IBrush?>(nameof(DiscBrush));

    public static readonly StyledProperty<IBrush?> TickBrushProperty =
        AvaloniaProperty.Register<ConfirmMorphCheck, IBrush?>(nameof(TickBrush));

    public IBrush? DiscBrush { get => GetValue(DiscBrushProperty); set => SetValue(DiscBrushProperty, value); }

    public IBrush? TickBrush { get => GetValue(TickBrushProperty); set => SetValue(TickBrushProperty, value); }

    private static readonly Point[] Tick = [new(5.6, 9.3), new(7.8, 11.4), new(12.4, 6.7)];
    private static readonly SpringSpec Pop = ArcMotionPresets.Snappy;

    private MotionValue<double>? _scale;
    private MotionValue<double>? _opacity;
    private MotionValue<double>? _draw;
    private Pen? _tickPen;

    public ConfirmMorphCheck()
    {
        Width = 18;
        Height = 18;
        IsHitTestVisible = false;
    }

    static ConfirmMorphCheck()
    {
        AffectsRender<ConfirmMorphCheck>(DiscBrushProperty, TickBrushProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TickBrushProperty)
        {
            _tickPen = TickBrush is { } brush ? new Pen(brush, 1.9, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round) : null;
        }
    }

    private MotionValue<double> Value(ref MotionValue<double>? field, double initial)
    {
        if (field is null)
        {
            field = new MotionValue<double>(this, initial, MotionVectors.Double);
            field.Changed += _ => InvalidateVisual();
        }
        else
        {
            field.Jump(initial);
        }
        return field;
    }

    /// <summary>Pops the disc and draws the tick (full motion), or shows it at rest.</summary>
    public void Play(bool animate)
    {
        var scale = Value(ref _scale, animate ? 0.5 : 1);
        var opacity = Value(ref _opacity, animate ? 0 : 1);
        var draw = Value(ref _draw, animate ? 0 : 1);
        if (!animate)
        {
            return;
        }
        _ = scale.AnimateTo(1, MotionSpec.Spring(Pop));
        _ = opacity.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromSeconds(0.12), ArcEasings.Standard));
        _ = draw.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromSeconds(0.3), ArcEasings.Enter, TimeSpan.FromSeconds(0.14)));
    }

    public override void Render(DrawingContext context)
    {
        var s = Math.Min(Bounds.Width, Bounds.Height) / 18.0;
        var scale = _scale?.Value ?? 1;
        var opacity = Math.Clamp(_opacity?.Value ?? 1, 0, 1);
        var matrix = Matrix.CreateTranslation(-9, -9) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(9, 9) * Matrix.CreateScale(s, s);
        using (context.PushTransform(matrix))
        using (context.PushOpacity(opacity))
        {
            if (DiscBrush is { } disc)
            {
                context.DrawEllipse(disc, null, new Point(9, 9), 8, 8);
            }
            if (_tickPen is not null)
            {
                DrawPartial(context, _tickPen, Math.Clamp(_draw?.Value ?? 1, 0, 1));
            }
        }
    }

    private static void DrawPartial(DrawingContext context, Pen pen, double fraction)
    {
        if (fraction <= 0.001)
        {
            return;
        }
        double total = 0;
        for (var i = 1; i < Tick.Length; i++)
        {
            total += PathMorphContour.Distance(Tick[i - 1], Tick[i]);
        }
        var remaining = total * fraction;
        for (var i = 1; i < Tick.Length && remaining > 0; i++)
        {
            var a = Tick[i - 1];
            var b = Tick[i];
            var length = PathMorphContour.Distance(a, b);
            var f = Math.Min(1, remaining / length);
            context.DrawLine(pen, a, new Point(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f));
            remaining -= length;
        }
    }
}
