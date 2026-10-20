using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc.Primitives;

/// <summary>
/// P7: one indicator that springs its position and size between items (selection rings, hover highlights,
/// active tabs). Place it in the same coordinate space as the items (for example as the first child of the
/// items panel host) and call <see cref="MoveTo(Rect, bool, ArcSpring?)"/>. Drawn in <see cref="Render"/>: the glide never relayouts.
/// </summary>
public class GlidingIndicator : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<GlidingIndicator, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<GlidingIndicator, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<GlidingIndicator, double>(nameof(StrokeThickness));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<GlidingIndicator, CornerRadius>(nameof(CornerRadius));

    public static readonly StyledProperty<BoxShadows> BoxShadowProperty =
        AvaloniaProperty.Register<GlidingIndicator, BoxShadows>(nameof(BoxShadow));

    public static readonly StyledProperty<ArcSpring> SpringProperty =
        AvaloniaProperty.Register<GlidingIndicator, ArcSpring>(nameof(Spring), ArcSpring.Morph);

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public BoxShadows BoxShadow { get => GetValue(BoxShadowProperty); set => SetValue(BoxShadowProperty, value); }
    public ArcSpring Spring { get => GetValue(SpringProperty); set => SetValue(SpringProperty, value); }

    private MotionValue<Rect>? _rect;
    private MotionValue<double>? _opacity;
    private bool _hasTarget;

    static GlidingIndicator()
    {
        AffectsRender<GlidingIndicator>(FillProperty, StrokeProperty, StrokeThicknessProperty, CornerRadiusProperty, BoxShadowProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<GlidingIndicator>(false);
    }

    public bool HasTarget => _hasTarget;

    /// <summary>Current (animated) rectangle in the indicator's coordinates.</summary>
    public Rect CurrentRect => _rect?.Value ?? default;

    /// <summary>
    /// Moves to <paramref name="target"/>. With <paramref name="animate"/> false it snaps (keyboard moves,
    /// container resizes). When it appears from nothing it lands in place and fades in.
    /// </summary>
    /// <param name="target">The new rectangle in the indicator's coordinates.</param>
    /// <param name="animate">False snaps there.</param>
    /// <param name="spring">A preset for this move; defaults to <see cref="Spring"/>.</param>
    public void MoveTo(Rect target, bool animate = true, ArcSpring? spring = null)
        => MoveTo(target, ArcMotionPresets.Get(spring ?? Spring), animate);

    /// <summary>
    /// Moves to <paramref name="target"/> on a custom spring (a component whose Arc source uses its own
    /// <c>visualDuration</c> / <c>bounce</c>, built with <see cref="SpringSpec.FromVisual"/>). Otherwise behaves like
    /// <see cref="MoveTo(Rect, bool, ArcSpring?)"/>: no animation snaps, appearing from nothing lands in place and fades in.
    /// </summary>
    /// <param name="target">The new rectangle in the indicator's coordinates.</param>
    /// <param name="spring">The spring for this move (position and size).</param>
    /// <param name="animate">False snaps there.</param>
    public void MoveTo(Rect target, SpringSpec spring, bool animate = true)
    {
        _rect ??= Create(target);
        _opacity ??= CreateOpacity();
        var full = animate && IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        if (!_hasTarget)
        {
            _rect.Jump(target);
            _hasTarget = true;
            if (full)
            {
                _opacity.Jump(0);
                _ = _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
            else
            {
                _opacity.Jump(1);
            }
            return;
        }
        if (_opacity.Target < 1)
        {
            _ = full ? _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)) : Task.CompletedTask;
            if (!full)
            {
                _opacity.Jump(1);
            }
        }
        if (full)
        {
            _ = _rect.AnimateTo(target, MotionSpec.Spring(spring));
        }
        else
        {
            _rect.Jump(target);
        }
    }

    /// <summary>Fades the indicator out; the next <see cref="MoveTo(Rect, bool, ArcSpring?)"/> lands in place again.</summary>
    public void Hide(bool animate = true)
    {
        if (_opacity is null)
        {
            return;
        }
        _hasTarget = false;
        if (animate && IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full)
        {
            _ = _opacity.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        else
        {
            _opacity.Jump(0);
        }
    }

    private MotionValue<Rect> Create(Rect initial)
    {
        var value = new MotionValue<Rect>(this, initial, MotionVectors.Rect);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private MotionValue<double> CreateOpacity()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    public override void Render(DrawingContext context)
    {
        if (_rect is null || _opacity is null || _opacity.Value <= 0.001)
        {
            return;
        }
        var rect = _rect.Value;
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }
        var radius = CornerRadius;
        var rounded = new RoundedRect(rect, radius.TopLeft, radius.TopRight, radius.BottomRight, radius.BottomLeft);
        var pen = Stroke is { } stroke && StrokeThickness > 0 ? new Pen(stroke, StrokeThickness) : null;
        using (context.PushOpacity(_opacity.Value))
        {
            context.DrawRectangle(Fill, pen, rounded, BoxShadow);
        }
    }
}
