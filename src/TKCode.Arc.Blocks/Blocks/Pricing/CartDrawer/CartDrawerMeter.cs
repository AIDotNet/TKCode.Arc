using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The free-shipping bar: a 6px pill track with a fill that grows from the left on the smooth spring and turns green over
/// 480ms once the threshold is reached (Arc Cart drawer, measured).
/// </summary>
/// <remarks>Code-driven: the fill keeps its velocity when the subtotal changes again mid-spring, and the color tween runs on its own clock.</remarks>
internal sealed class CartDrawerMeter : Control
{
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<CartDrawerMeter, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<CartDrawerMeter, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<IBrush?> CompleteBrushProperty =
        AvaloniaProperty.Register<CartDrawerMeter, IBrush?>(nameof(CompleteBrush));

    private readonly SolidColorBrush _fill = new();
    private MotionValue<double>? _fraction;
    private MotionValue<double>? _complete;
    private double _target;
    private bool _isComplete;

    static CartDrawerMeter()
    {
        AffectsRender<CartDrawerMeter>(TrackBrushProperty);
    }

    public CartDrawerMeter()
    {
        Height = 6;
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public IBrush? FillBrush
    {
        get => GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public IBrush? CompleteBrush
    {
        get => GetValue(CompleteBrushProperty);
        set => SetValue(CompleteBrushProperty, value);
    }

    /// <summary>The fill drawn now (0–1).</summary>
    public double DisplayedFraction => _fraction?.Value ?? _target;

    /// <summary>The fill the bar is heading for.</summary>
    public double Fraction => _target;

    /// <summary>Moves the fill to <paramref name="fraction"/> and sets the reached color; <paramref name="animate"/> false jumps.</summary>
    public void Set(double fraction, bool complete, bool animate)
    {
        _target = Math.Clamp(fraction, 0, 1);
        _fraction ??= CreateValue(_target);
        _complete ??= CreateValue(complete ? 1 : 0);
        if (animate)
        {
            _ = _fraction.AnimateTo(_target, MotionSpec.Spring(ArcSpring.Smooth));
        }
        else
        {
            _fraction.Jump(_target);
        }
        if (complete != _isComplete || !animate)
        {
            _isComplete = complete;
            if (animate)
            {
                _ = _complete.AnimateTo(complete ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.Standard));
            }
            else
            {
                _complete.Jump(complete ? 1 : 0);
            }
        }
        UpdateFill();
        InvalidateVisual();
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ =>
        {
            UpdateFill();
            InvalidateVisual();
        };
        return value;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FillBrushProperty || change.Property == CompleteBrushProperty)
        {
            UpdateFill();
            InvalidateVisual();
        }
    }

    private void UpdateFill()
    {
        var a = (FillBrush as ISolidColorBrush)?.Color ?? Colors.Gray;
        var b = (CompleteBrush as ISolidColorBrush)?.Color ?? a;
        var t = Math.Clamp(_complete?.Value ?? (_isComplete ? 1 : 0), 0, 1);
        _fill.Color = Color.FromArgb(
            (byte)Math.Round(a.A + (b.A - a.A) * t),
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0)
        {
            return;
        }
        var r = h / 2;
        if (TrackBrush is { } track)
        {
            context.DrawRectangle(track, null, new RoundedRect(new Rect(0, 0, w, h), r));
        }
        var fw = w * Math.Clamp(DisplayedFraction, 0, 1);
        if (fw > 0.5)
        {
            context.DrawRectangle(_fill, null, new RoundedRect(new Rect(0, 0, fw, h), Math.Min(r, fw / 2)));
        }
    }
}
