using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The highlight of a <see cref="GlassMenu"/>: one rounded block of brighter glass that glides from row to row on a snappy
/// spring, with a soft glow inside it that follows the pointer along the row. It fades in where it first lands and fades
/// out when the pointer and the focus leave the menu. Reduced motion moves it at once (no glide, no glow); no motion also
/// skips the fades. Drawn in <see cref="Render"/>, so gliding never lays anything out.
/// </summary>
internal sealed class GlassMenuHighlight : Avalonia.Controls.Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<GlassMenuHighlight, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> GlowProperty =
        AvaloniaProperty.Register<GlassMenuHighlight, IBrush?>(nameof(Glow));

    public static readonly StyledProperty<IBrush?> SolidFillProperty =
        AvaloniaProperty.Register<GlassMenuHighlight, IBrush?>(nameof(SolidFill));

    public static readonly StyledProperty<bool> ReduceTransparencyProperty =
        AvaloniaProperty.Register<GlassMenuHighlight, bool>(nameof(ReduceTransparency));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<GlassMenuHighlight, CornerRadius>(nameof(CornerRadius));

    /// <summary>Radius of the pointer glow, in px.</summary>
    public static readonly StyledProperty<double> GlowRadiusProperty =
        AvaloniaProperty.Register<GlassMenuHighlight, double>(nameof(GlowRadius), 90);

    private readonly MotionValue<Rect> _rect;
    private readonly MotionValue<double> _opacity;
    private readonly MotionValue<double> _glowX;
    private readonly MotionValue<double> _glowAlpha;
    private bool _shown;
    private bool _brushesValid;
    private IImmutableBrush? _fill;
    private IImmutableBrush? _glow;

    static GlassMenuHighlight()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<GlassMenuHighlight>(false);
        AffectsRender<GlassMenuHighlight>(CornerRadiusProperty, GlowRadiusProperty);
    }

    public GlassMenuHighlight()
    {
        _rect = new MotionValue<Rect>(this, default, MotionVectors.Rect);
        _opacity = new MotionValue<double>(this, 0, MotionVectors.Double);
        _glowX = new MotionValue<double>(this, 0, MotionVectors.Double);
        _glowAlpha = new MotionValue<double>(this, 0, MotionVectors.Double);
        _rect.Changed += _ => InvalidateVisual();
        _opacity.Changed += _ => InvalidateVisual();
        _glowX.Changed += _ => InvalidateVisual();
        _glowAlpha.Changed += _ => InvalidateVisual();
    }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public Color FillColor => GlassPaneColors.Of(Fill);
    public IBrush? Glow { get => GetValue(GlowProperty); set => SetValue(GlowProperty, value); }
    public Color GlowColor => GlassPaneColors.Of(Glow);
    public IBrush? SolidFill { get => GetValue(SolidFillProperty); set => SetValue(SolidFillProperty, value); }
    public bool ReduceTransparency { get => GetValue(ReduceTransparencyProperty); set => SetValue(ReduceTransparencyProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public double GlowRadius { get => GetValue(GlowRadiusProperty); set => SetValue(GlowRadiusProperty, value); }

    /// <summary>The highlight as drawn now.</summary>
    public Rect CurrentRect => _rect.Value;

    /// <summary>Where the highlight is heading.</summary>
    public Rect TargetRect => _rect.Target;

    /// <summary>The highlight's opacity now.</summary>
    public double CurrentOpacity => _opacity.Value;

    /// <summary>The pointer glow's strength now, 0..1.</summary>
    public double CurrentGlow => _glowAlpha.Value;

    /// <summary>True while the highlight is placed (it may still be fading in).</summary>
    public bool IsShown => _shown;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FillProperty || change.Property == GlowProperty || change.Property == SolidFillProperty ||
            change.Property == ReduceTransparencyProperty)
        {
            _brushesValid = false;
            InvalidateVisual();
        }
    }

    /// <summary>Moves onto <paramref name="target"/>: glides when shown at full motion, lands and fades in otherwise.</summary>
    public void MoveTo(Rect target, bool animate)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (_shown && animate && level == ArcMotionLevel.Full && _opacity.Value > 0.05)
        {
            _ = _rect.AnimateTo(target, MotionSpec.Spring(ArcSpring.Snappy));
        }
        else
        {
            _rect.Jump(target);
        }
        _shown = true;
        if (level == ArcMotionLevel.Off || !animate)
        {
            _opacity.Jump(1);
        }
        else if (_opacity.Target < 1)
        {
            _ = _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        InvalidateVisual();
    }

    /// <summary>Fades out (at once with no motion).</summary>
    public void Hide()
    {
        if (!_shown)
        {
            return;
        }
        _shown = false;
        GlowAt(null);
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _opacity.Jump(0);
        }
        else
        {
            _ = _opacity.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Exit));
        }
    }

    /// <summary>
    /// Moves the glow toward <paramref name="x"/> (this control's coordinates) along the highlighted row, or fades it out
    /// when null. Full motion only.
    /// </summary>
    public void GlowAt(double? x)
    {
        if (x is { } target && ArcMotion.IsFull(this) && !ReduceTransparency)
        {
            if (_glowAlpha.Value <= 0.001)
            {
                _glowX.Jump(target);
            }
            else
            {
                _ = _glowX.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
            }
            if (_glowAlpha.Target < 1)
            {
                _ = _glowAlpha.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
            return;
        }
        if (_glowAlpha.Target > 0 || _glowAlpha.Value > 0)
        {
            if (ArcMotion.IsFull(this))
            {
                _ = _glowAlpha.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
            }
            else
            {
                _glowAlpha.Jump(0);
            }
        }
    }

    private void EnsureBrushes()
    {
        if (_brushesValid)
        {
            return;
        }
        _brushesValid = true;
        _fill = ReduceTransparency && SolidFill is { } solid ? solid.ToImmutable() : new ImmutableSolidColorBrush(FillColor);
        var glow = GlowColor;
        _glow = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(glow, 0),
                new GradientStop(Color.FromArgb(0, glow.R, glow.G, glow.B), 1)
            }
        }.ToImmutable();
    }

    public override void Render(DrawingContext context)
    {
        var opacity = _opacity.Value;
        var rect = _rect.Value;
        if (opacity <= 0.001 || rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }
        EnsureBrushes();
        var max = Math.Min(rect.Width, rect.Height) / 2;
        var radius = CornerRadius;
        var shape = new RoundedRect(rect, new CornerRadius(Math.Min(radius.TopLeft, max), Math.Min(radius.TopRight, max),
            Math.Min(radius.BottomRight, max), Math.Min(radius.BottomLeft, max)));
        using (context.PushOpacity(opacity))
        {
            context.DrawRectangle(_fill, null, shape);
            var glow = _glowAlpha.Value;
            if (glow > 0.002 && !ReduceTransparency)
            {
                var r = Math.Max(1, GlowRadius);
                var x = Math.Clamp(_glowX.Value, rect.Left, rect.Right);
                using (context.PushClip(shape))
                using (context.PushOpacity(glow))
                {
                    context.DrawEllipse(_glow, null, new Point(x, rect.Center.Y), r, r);
                }
            }
        }
    }
}
