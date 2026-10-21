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
/// animate; passive changes (a different available size from the parent) and the first measure jump. An unbounded
/// available size (a stack panel, an auto row) compares equal to itself, so content changes inside stacks spring.
/// Height mode with <see cref="IsExpanded"/> = false collapses to zero height (disclosure panels).
/// While the width moves, the child keeps its own width and is pinned by its <c>HorizontalAlignment</c>: Left and
/// Right hold that edge (a chip that grows to the right), Center and Stretch keep it centered (a button label).
/// The decorator clips only while it moves (or is collapsed), so focus rings stay whole at rest.
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

    /// <summary>
    /// Overflow kept visible across the moving axis while clipping: left and right of a height animation (a blurred
    /// line of text, a focus ring), above and below a width animation. NaN (default) keeps none for Height and half
    /// the height for Width.
    /// </summary>
    public static readonly StyledProperty<double> CrossClipOverflowProperty =
        AvaloniaProperty.Register<SpringSizeDecorator, double>(nameof(CrossClipOverflow), double.NaN);

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

    /// <inheritdoc cref="CrossClipOverflowProperty"/>
    public double CrossClipOverflow
    {
        get => GetValue(CrossClipOverflowProperty);
        set => SetValue(CrossClipOverflowProperty, value);
    }

    /// <summary>True while the size is springing to a new content size.</summary>
    public bool IsMoving => _size?.IsAnimating == true;

    static SpringSizeDecorator()
    {
        AffectsMeasure<SpringSizeDecorator>(AxisProperty, IsExpandedProperty);
    }

    private MotionValue<Size>? _size;
    private Size? _lastAvailable;
    private Size _lastTarget;
    private readonly RectangleGeometry _clip = new();
    private double _measuredHeight;

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
        _measuredHeight = result.Height;
        // A height spring clips to the arranged width (a stretched child is wider than it asked for), so it updates in
        // ArrangeOverride; the other axes clip to what the child asked for.
        if (axis != SpringSizeAxis.Height)
        {
            UpdateClip(result);
        }
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
            // The child keeps its width while the slot catches up: pinned to an edge, or centered (overflowing evenly).
            SpringSizeAxis.Width => new Rect(child.HorizontalAlignment switch
            {
                Avalonia.Layout.HorizontalAlignment.Left => 0,
                Avalonia.Layout.HorizontalAlignment.Right => finalSize.Width - desired.Width,
                _ => (finalSize.Width - desired.Width) / 2
            }, 0, desired.Width, finalSize.Height),
            SpringSizeAxis.Height => new Rect(0, 0, finalSize.Width, Math.Max(desired.Height, 0)),
            _ => new Rect(0, 0, desired.Width, desired.Height)
        };
        child.Arrange(rect);
        if (Axis == SpringSizeAxis.Height)
        {
            UpdateClip(new Size(finalSize.Width, _measuredHeight));
        }
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
        var cross = CrossClipOverflow;
        Rect? clip;
        if (Axis == SpringSizeAxis.Width)
        {
            cross = double.IsNaN(cross) ? size.Height * 0.5 : cross;
            clip = animating ? new Rect(-ClipOverflow, -cross, size.Width + 2 * ClipOverflow, size.Height + 2 * cross) : null;
        }
        else
        {
            cross = Axis == SpringSizeAxis.Height && !double.IsNaN(cross) ? cross : 0;
            clip = animating || !IsExpanded ? new Rect(-cross, 0, size.Width + 2 * cross, size.Height) : null;
        }
        if (clip is not { } rect)
        {
            Clip = null;
            return;
        }
        // One geometry, updated in place: a moving clip allocates nothing per frame.
        _clip.Rect = rect;
        Clip = _clip;
        InvalidateVisual();
    }

    // Equals first: an unbounded axis is ∞ on both sides, and ∞ − ∞ is NaN (which would read as "changed").
    private static bool SameSize(Size a, Size b) => Same(a.Width, b.Width) && Same(a.Height, b.Height);

    private static bool Same(double a, double b) => a.Equals(b) || Math.Abs(a - b) < 0.5;
}
