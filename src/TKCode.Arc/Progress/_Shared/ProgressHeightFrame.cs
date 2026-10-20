using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Follows its child's height. Shortly after <see cref="Arm"/> (a content change) a height change springs from the
/// old height to the new one (smooth, P6); any other change (resize, font swap, wrapping) follows at once, like
/// Arc's HeightFrame / SwapText slots. It clips only while moving, so focus rings stay visible at rest.
/// </summary>
internal sealed class ProgressHeightFrame : Decorator
{
    /// <summary>How long after <see cref="Arm"/> a height change still springs (Skeleton 120ms, Stepper 700ms).</summary>
    public static readonly StyledProperty<double> ArmWindowProperty =
        AvaloniaProperty.Register<ProgressHeightFrame, double>(nameof(ArmWindow), 120);

    public double ArmWindow { get => GetValue(ArmWindowProperty); set => SetValue(ArmWindowProperty, value); }

    private MotionValue<double>? _height;
    private double _lastTarget = double.NaN;
    private DateTime _armedAt = DateTime.MinValue;

    /// <summary>Allows content-driven height changes during the next <see cref="ArmWindow"/> ms to spring.</summary>
    public void Arm() => _armedAt = DateTime.UtcNow;

    internal bool IsHeightAnimating => _height?.IsAnimating == true;

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = Child;
        if (child is null)
        {
            return default;
        }
        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        var target = child.DesiredSize.Height;
        if (_height is null)
        {
            _height = new MotionValue<double>(this, target, MotionVectors.Double);
            _height.Changed += _ => InvalidateMeasure();
            _lastTarget = target;
        }
        else if (Math.Abs(target - _lastTarget) > 0.5)
        {
            var armed = (DateTime.UtcNow - _armedAt).TotalMilliseconds <= ArmWindow;
            _lastTarget = target;
            if (armed && IsLoaded && ArcMotion.IsFull(this))
            {
                _ = _height.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
            }
            else
            {
                _height.Jump(target);
            }
        }
        var height = _height.IsAnimating ? Math.Max(0, _height.Value) : target;
        Clip = _height.IsAnimating ? new RectangleGeometry(new Rect(0, 0, child.DesiredSize.Width, height)) : null;
        return new Size(child.DesiredSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var child = Child;
        if (child is not null)
        {
            child.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(child.DesiredSize.Height, finalSize.Height)));
        }
        if (Clip is not null)
        {
            Clip = new RectangleGeometry(new Rect(finalSize));
        }
        return finalSize;
    }
}
