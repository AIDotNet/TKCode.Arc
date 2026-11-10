using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The selection lens of <see cref="GlassSegmented"/> and <see cref="GlassTabBar"/>: a bubble of brighter glass drawn in
/// <see cref="Render"/>. Its two side edges spring separately, the leading edge quicker than the trailing one, so the
/// lens stretches toward its new place and draws itself together as it lands. Pressed, it swells a little and its rim
/// brightens; dragged, it follows the pointer. Reduced motion fades it out and in at the new place; no motion jumps.
/// </summary>
internal sealed class GlassLens : Avalonia.Controls.Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<GlassLens, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> RimProperty =
        AvaloniaProperty.Register<GlassLens, IBrush?>(nameof(Rim));

    public static readonly StyledProperty<IBrush?> SolidFillProperty =
        AvaloniaProperty.Register<GlassLens, IBrush?>(nameof(SolidFill));

    public static readonly StyledProperty<IBrush?> SolidStrokeProperty =
        AvaloniaProperty.Register<GlassLens, IBrush?>(nameof(SolidStroke));

    public static readonly StyledProperty<bool> ReduceTransparencyProperty =
        AvaloniaProperty.Register<GlassLens, bool>(nameof(ReduceTransparency));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<GlassLens, CornerRadius>(nameof(CornerRadius));

    public static readonly StyledProperty<BoxShadows> BoxShadowProperty =
        AvaloniaProperty.Register<GlassLens, BoxShadows>(nameof(BoxShadow));

    /// <summary>How far the lens grows on every side while pressed, in px.</summary>
    public static readonly StyledProperty<double> SwellProperty =
        AvaloniaProperty.Register<GlassLens, double>(nameof(Swell), 2);

    /// <summary>The leading edge: quick, with a little give.</summary>
    internal static readonly SpringSpec LeadSpring = SpringSpec.FromVisual(0.28, 0.16);

    /// <summary>The trailing edge: slower and calmer, so the lens stretches on the way.</summary>
    internal static readonly SpringSpec TrailSpring = SpringSpec.FromVisual(0.46, 0.08);

    /// <summary>Following a dragging pointer: both edges, tight.</summary>
    internal static readonly SpringSpec DragSpring = SpringSpec.FromVisual(0.18, 0);

    private readonly MotionValue<double> _left;
    private readonly MotionValue<double> _right;
    private readonly MotionValue<double> _opacity;
    private readonly MotionValue<double> _lift;
    private double _top;
    private double _height;
    private bool _hasTarget;
    private bool _brushesValid;
    private IImmutableBrush? _fill;
    private ImmutablePen? _rimPen;
    private ImmutablePen? _solidPen;
    private int _fadeGeneration;

    static GlassLens()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<GlassLens>(false);
        AffectsRender<GlassLens>(CornerRadiusProperty, BoxShadowProperty, SwellProperty);
    }

    public GlassLens()
    {
        _left = new MotionValue<double>(this, 0, MotionVectors.Double);
        _right = new MotionValue<double>(this, 0, MotionVectors.Double);
        _opacity = new MotionValue<double>(this, 0, MotionVectors.Double);
        _lift = new MotionValue<double>(this, 0, MotionVectors.Double);
        _left.Changed += _ => InvalidateVisual();
        _right.Changed += _ => InvalidateVisual();
        _opacity.Changed += _ => InvalidateVisual();
        _lift.Changed += _ => InvalidateVisual();
    }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public Color FillColor => GlassPaneColors.Of(Fill);
    public IBrush? Rim { get => GetValue(RimProperty); set => SetValue(RimProperty, value); }
    public Color RimColor => GlassPaneColors.Of(Rim);
    public IBrush? SolidFill { get => GetValue(SolidFillProperty); set => SetValue(SolidFillProperty, value); }
    public IBrush? SolidStroke { get => GetValue(SolidStrokeProperty); set => SetValue(SolidStrokeProperty, value); }
    public bool ReduceTransparency { get => GetValue(ReduceTransparencyProperty); set => SetValue(ReduceTransparencyProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public BoxShadows BoxShadow { get => GetValue(BoxShadowProperty); set => SetValue(BoxShadowProperty, value); }
    public double Swell { get => GetValue(SwellProperty); set => SetValue(SwellProperty, value); }

    /// <summary>Whether the lens was placed at least once.</summary>
    public bool HasTarget => _hasTarget;

    /// <summary>The lens as drawn now, without the swell.</summary>
    public Rect CurrentRect => new(_left.Value, _top, Math.Max(0, _right.Value - _left.Value), _height);

    /// <summary>Where the lens is heading.</summary>
    public Rect TargetRect => new(_left.Target, _top, Math.Max(0, _right.Target - _left.Target), _height);

    /// <summary>The lens's opacity now.</summary>
    public double CurrentOpacity => _opacity.Value;

    /// <summary>How far the press swell has grown, 0..1.</summary>
    public double CurrentLift => _lift.Value;

    /// <summary>True while either edge is still moving.</summary>
    public bool IsMoving => _left.IsAnimating || _right.IsAnimating;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FillProperty || change.Property == RimProperty || change.Property == SolidFillProperty ||
            change.Property == SolidStrokeProperty || change.Property == ReduceTransparencyProperty)
        {
            _brushesValid = false;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Moves the lens onto <paramref name="target"/>. Without <paramref name="animate"/> (first placement, a resize) it is
    /// there at once. At full motion the edge on the side of travel leads and the other trails; reduced motion fades the
    /// lens out and in at the new place; no motion jumps.
    /// </summary>
    public void MoveTo(Rect target, bool animate)
    {
        _top = target.Y;
        _height = target.Height;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!_hasTarget || !animate || level == ArcMotionLevel.Off)
        {
            _hasTarget = true;
            _fadeGeneration++;
            _left.Jump(target.Left);
            _right.Jump(target.Right);
            _opacity.Jump(1);
            InvalidateVisual();
            return;
        }
        if (level == ArcMotionLevel.Reduced)
        {
            _ = FadeTo(target);
            return;
        }
        var forward = target.Center.X >= (_left.Value + _right.Value) / 2;
        _ = _left.AnimateTo(target.Left, MotionSpec.Spring(forward ? TrailSpring : LeadSpring));
        _ = _right.AnimateTo(target.Right, MotionSpec.Spring(forward ? LeadSpring : TrailSpring));
        if (_opacity.Target < 1)
        {
            _ = _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
    }

    /// <summary>Follows a dragging pointer to <paramref name="target"/> on a tight spring (jumps below full motion).</summary>
    public void DragTo(Rect target)
    {
        _top = target.Y;
        _height = target.Height;
        if (!ArcMotion.IsFull(this))
        {
            _left.Jump(target.Left);
            _right.Jump(target.Right);
            InvalidateVisual();
            return;
        }
        _ = _left.AnimateTo(target.Left, MotionSpec.Spring(DragSpring));
        _ = _right.AnimateTo(target.Right, MotionSpec.Spring(DragSpring));
    }

    /// <summary>Swells the lens while it is pressed (full motion only) and lets it settle when released.</summary>
    public void SetLifted(bool lifted)
    {
        var target = lifted && ArcMotion.IsFull(this) ? 1.0 : 0.0;
        if (Math.Abs(_lift.Target - target) < 0.001)
        {
            return;
        }
        if (ArcMotion.IsFull(this))
        {
            _ = _lift.AnimateTo(target, MotionSpec.Spring(ArcSpring.Snappy));
        }
        else
        {
            _lift.Jump(target);
        }
    }

    private async Task FadeTo(Rect target)
    {
        var generation = ++_fadeGeneration;
        await _opacity.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant / 2, ArcEasings.Exit));
        if (generation != _fadeGeneration)
        {
            return;
        }
        _left.Jump(target.Left);
        _right.Jump(target.Right);
        await _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Enter));
    }

    private void EnsureBrushes()
    {
        if (_brushesValid)
        {
            return;
        }
        _brushesValid = true;
        _fill = new ImmutableSolidColorBrush(FillColor);
        var rim = RimColor;
        _rimPen = new ImmutablePen(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(rim, 0),
                new GradientStop(Color.FromArgb((byte)(rim.A * 0.25), rim.R, rim.G, rim.B), 0.6),
                new GradientStop(Color.FromArgb((byte)(rim.A * 0.5), rim.R, rim.G, rim.B), 1)
            }
        }.ToImmutable(), 1);
        _solidPen = SolidStroke is { } stroke ? new ImmutablePen(stroke.ToImmutable(), 1) : null;
    }

    public override void Render(DrawingContext context)
    {
        var opacity = _opacity.Value;
        if (!_hasTarget || opacity <= 0.001)
        {
            return;
        }
        var width = _right.Value - _left.Value;
        if (width <= 0 || _height <= 0)
        {
            return;
        }
        EnsureBrushes();
        var grow = _lift.Value * Swell;
        var rect = new Rect(_left.Value - grow, _top - grow, width + grow * 2, _height + grow * 2);
        var radius = Clamp(CornerRadius, rect);
        var shape = new RoundedRect(rect, radius);
        var rim = new RoundedRect(rect.Deflate(0.5), Clamp(new CornerRadius(Math.Max(0, radius.TopLeft - 0.5)), rect));
        using (context.PushOpacity(opacity))
        {
            if (ReduceTransparency)
            {
                context.DrawRectangle(SolidFill, _solidPen, shape, BoxShadow);
                return;
            }
            context.DrawRectangle(_fill, null, shape, BoxShadow);
            using (context.PushOpacity(0.75 + 0.25 * _lift.Value))
            {
                context.DrawRectangle(null, _rimPen, rim);
            }
        }
    }

    /// <summary>A pill radius larger than half the height draws as a pill; keep every corner within the shape.</summary>
    private static CornerRadius Clamp(CornerRadius radius, Rect rect)
    {
        var max = Math.Min(rect.Width, rect.Height) / 2;
        return new CornerRadius(Math.Min(radius.TopLeft, max), Math.Min(radius.TopRight, max),
            Math.Min(radius.BottomRight, max), Math.Min(radius.BottomLeft, max));
    }
}
