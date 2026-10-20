using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc.Primitives;

/// <summary>Which dimension of <see cref="SpringSizeDecorator"/> follows its content on a spring.</summary>
public enum SpringSizeAxis
{
    Width,
    Height,
    Both
}

/// <summary>
/// P5 / P6: the decorator's size follows its child's desired size on a spring. Only content-driven changes
/// animate; passive changes (a different available size from the parent) and the first measure jump.
/// Height mode with <see cref="IsExpanded"/> = false collapses to zero height (disclosure panels).
/// </summary>
public class SpringSizeDecorator : Decorator
{
    public static readonly StyledProperty<SpringSizeAxis> AxisProperty =
        AvaloniaProperty.Register<SpringSizeDecorator, SpringSizeAxis>(nameof(Axis), SpringSizeAxis.Width);

    public static readonly StyledProperty<ArcSpring> SpringProperty =
        AvaloniaProperty.Register<SpringSizeDecorator, ArcSpring>(nameof(Spring), ArcSpring.Morph);

    /// <summary>Spring used when the size shrinks; defaults to <see cref="Spring"/>.</summary>
    public static readonly StyledProperty<ArcSpring?> ShrinkSpringProperty =
        AvaloniaProperty.Register<SpringSizeDecorator, ArcSpring?>(nameof(ShrinkSpring));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<SpringSizeDecorator, bool>(nameof(IsExpanded), true);

    /// <summary>Horizontal overflow allowed while a width animation runs (Arc clips with inset -12px).</summary>
    public static readonly StyledProperty<double> ClipOverflowProperty =
        AvaloniaProperty.Register<SpringSizeDecorator, double>(nameof(ClipOverflow), 12);

    public SpringSizeAxis Axis
    {
        get => GetValue(AxisProperty);
        set => SetValue(AxisProperty, value);
    }

    public ArcSpring Spring
    {
        get => GetValue(SpringProperty);
        set => SetValue(SpringProperty, value);
    }

    public ArcSpring? ShrinkSpring
    {
        get => GetValue(ShrinkSpringProperty);
        set => SetValue(ShrinkSpringProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public double ClipOverflow
    {
        get => GetValue(ClipOverflowProperty);
        set => SetValue(ClipOverflowProperty, value);
    }

    static SpringSizeDecorator()
    {
        AffectsMeasure<SpringSizeDecorator>(AxisProperty, IsExpandedProperty);
    }

    private MotionValue<Size>? _size;
    private Size? _lastAvailable;
    private Size _lastTarget;

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = Child;
        if (child is null)
        {
            return default;
        }
        var axis = Axis;
        var measureSize = axis switch
        {
            SpringSizeAxis.Width => availableSize.WithWidth(double.PositiveInfinity),
            SpringSizeAxis.Height => availableSize.WithHeight(double.PositiveInfinity),
            _ => new Size(double.PositiveInfinity, double.PositiveInfinity)
        };
        child.Measure(measureSize);
        var target = child.DesiredSize;
        if (axis != SpringSizeAxis.Width && !IsExpanded)
        {
            target = target.WithHeight(0);
        }
        _size ??= CreateValue(target);
        var passive = _lastAvailable is { } last && !SameSize(last, availableSize);
        _lastAvailable = availableSize;
        if (!SameSize(target, _lastTarget))
        {
            var shrinking = target.Width < _lastTarget.Width || target.Height < _lastTarget.Height;
            _lastTarget = target;
            if (!IsLoaded || passive || !ArcMotion.IsFull(this))
            {
                _size.Jump(target);
            }
            else
            {
                var spring = shrinking && ShrinkSpring is { } shrink ? shrink : Spring;
                _ = _size.AnimateTo(target, MotionSpec.Spring(spring));
            }
        }
        var current = _size.Value;
        var result = axis switch
        {
            SpringSizeAxis.Width => new Size(current.Width, target.Height),
            SpringSizeAxis.Height => new Size(target.Width, current.Height),
            _ => current
        };
        UpdateClip(result);
        return new Size(Math.Min(result.Width, availableSize.Width), Math.Min(result.Height, availableSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var child = Child;
        if (child is null)
        {
            return finalSize;
        }
        var desired = child.DesiredSize;
        var rect = Axis switch
        {
            // The label stays centered and overflows evenly while the slot catches up.
            SpringSizeAxis.Width => new Rect((finalSize.Width - desired.Width) / 2, 0, desired.Width, finalSize.Height),
            SpringSizeAxis.Height => new Rect(0, 0, finalSize.Width, Math.Max(desired.Height, 0)),
            _ => new Rect(0, 0, desired.Width, desired.Height)
        };
        child.Arrange(rect);
        return finalSize;
    }

    private MotionValue<Size> CreateValue(Size initial)
    {
        _lastTarget = initial;
        var value = new MotionValue<Size>(this, initial, MotionVectors.Size);
        value.Changed += _ => InvalidateMeasure();
        return value;
    }

    private void UpdateClip(Size size)
    {
        var animating = _size?.IsAnimating == true;
        if (Axis == SpringSizeAxis.Width)
        {
            Clip = animating
                ? new RectangleGeometry(new Rect(-ClipOverflow, -size.Height * 0.5, size.Width + 2 * ClipOverflow, size.Height * 2))
                : null;
        }
        else
        {
            var collapsedOrMoving = animating || !IsExpanded;
            Clip = collapsedOrMoving ? new RectangleGeometry(new Rect(size)) : null;
        }
    }

    // Equals first: an unbounded axis is ∞ on both sides, and ∞ − ∞ is NaN (which would read as "changed").
    private static bool SameSize(Size a, Size b) => Same(a.Width, b.Width) && Same(a.Height, b.Height);

    private static bool Same(double a, double b) => a.Equals(b) || Math.Abs(a - b) < 0.5;
}
