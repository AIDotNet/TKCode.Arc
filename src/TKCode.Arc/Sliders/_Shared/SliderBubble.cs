using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The value bubble that rides a thumb. It pops in from <see cref="EnterScale"/>, 6px low and slightly blurred
/// (scale and lift on the snappy spring, opacity and blur on a fast enter ease, P26) and leaves on instant toward
/// <see cref="ExitScale"/>. Reduced motion keeps only the fade. Position, scale, lift and sway are one render matrix
/// pivoting on the bubble's bottom edge, so the bubble never relayouts while it moves.
/// </summary>
internal sealed class SliderBubble : Border
{
    private readonly MatrixTransform _transform = new();
    private readonly MotionValue<double> _opacity;
    private readonly MotionValue<double> _scale;
    private readonly MotionValue<double> _lift;
    private readonly MotionValue<double> _blur;
    private BlurEffect? _blurEffect;
    private double _x;
    private double _y;
    private double _angle;

    public SliderBubble()
    {
        IsHitTestVisible = false;
        Opacity = 0;
        RenderTransform = _transform;
        RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
        _opacity = new MotionValue<double>(this, 0, MotionVectors.Double);
        _opacity.Changed += v => Opacity = Math.Clamp(v, 0, 1);
        _scale = new MotionValue<double>(this, 1, MotionVectors.Double);
        _scale.Changed += _ => UpdateTransform();
        _lift = new MotionValue<double>(this, 0, MotionVectors.Double);
        _lift.Changed += _ => UpdateTransform();
        _blur = new MotionValue<double>(this, 0, MotionVectors.Double);
        _blur.Changed += UpdateBlur;
    }

    public double EnterScale { get; set; } = 0.6;

    public double ExitScale { get; set; } = 0.85;

    /// <summary>Fade-in used under reduced motion.</summary>
    public TimeSpan ReducedEnter { get; set; } = ArcMotionPresets.Instant;

    /// <summary>Pivot of scale and sway as a fraction of the bubble height (1 = bottom edge).</summary>
    public double PivotY { get; set; } = 1;

    public bool IsShown { get; private set; }

    /// <summary>Places the bubble's top-left corner (before scale and lift) and its sway angle in degrees.</summary>
    public void SetPlacement(double x, double y, double angle = 0)
    {
        _x = x;
        _y = y;
        _angle = angle;
        UpdateTransform();
    }

    public void Show(bool shown)
    {
        if (IsShown == shown)
        {
            return;
        }
        IsShown = shown;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || level == ArcMotionLevel.Off)
        {
            _opacity.Jump(shown ? 1 : 0);
            _scale.Jump(1);
            _lift.Jump(0);
            _blur.Jump(0);
            return;
        }
        if (level == ArcMotionLevel.Reduced)
        {
            _scale.Jump(1);
            _lift.Jump(0);
            _blur.Jump(0);
            _ = _opacity.AnimateTo(shown ? 1 : 0, MotionSpec.Tween(shown ? ReducedEnter : ArcMotionPresets.Instant, ArcEasings.Standard));
            return;
        }
        if (shown)
        {
            if (_opacity.Value <= 0.001)
            {
                _scale.Jump(EnterScale);
                _lift.Jump(6);
                _blur.Jump(2);
            }
            _ = _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
            _ = _blur.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
            _ = _scale.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
            _ = _lift.AnimateTo(0, MotionSpec.Spring(ArcSpring.Snappy));
        }
        else
        {
            var exit = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
            _ = _opacity.AnimateTo(0, exit);
            _ = _blur.AnimateTo(2, exit);
            _ = _scale.AnimateTo(ExitScale, exit);
            _ = _lift.AnimateTo(4, exit);
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        UpdateTransform();
        return size;
    }

    private void UpdateTransform()
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        var ox = width / 2;
        var oy = height * PivotY;
        var scale = _scale.Value;
        var matrix = Matrix.CreateTranslation(-ox, -oy) * Matrix.CreateScale(scale, scale);
        if (_angle != 0)
        {
            matrix *= Matrix.CreateRotation(_angle * Math.PI / 180);
        }
        matrix *= Matrix.CreateTranslation(ox + _x, oy + _y + _lift.Value);
        if (_transform.Matrix != matrix)
        {
            _transform.Matrix = matrix;
        }
    }

    private void UpdateBlur(double radius)
    {
        if (radius <= 0.05)
        {
            if (Effect is not null)
            {
                Effect = null;
            }
            return;
        }
        _blurEffect ??= new BlurEffect();
        _blurEffect.Radius = radius;
        if (!ReferenceEquals(Effect, _blurEffect))
        {
            Effect = _blurEffect;
        }
    }
}
