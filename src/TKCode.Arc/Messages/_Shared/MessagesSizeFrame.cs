using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// P5 / P6 size follow: the frame's height (or width) springs to its content's size when the content changes
/// (new copy, a longer message), and follows instantly when only the available cross size changes (window resize,
/// font swap) or on the first measure. It clips only while moving, so focus rings stay whole at rest.
/// <para>
/// Local stand-in for Core <c>SpringSizeDecorator</c>, whose passive-change test treats an infinite available size
/// (any StackPanel, any auto-sized parent) as changed on every pass and therefore never animates.
/// </para>
/// </summary>
internal sealed class MessagesSizeFrame : Decorator
{
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<MessagesSizeFrame, Orientation>(nameof(Orientation), Orientation.Vertical);

    /// <summary>Overflow kept visible across the axis while clipping (focus rings, descenders).</summary>
    public static readonly StyledProperty<double> ClipOverflowProperty =
        AvaloniaProperty.Register<MessagesSizeFrame, double>(nameof(ClipOverflow), 12);

    /// <summary>Vertical: the height follows the content (default); Horizontal: the width does.</summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double ClipOverflow
    {
        get => GetValue(ClipOverflowProperty);
        set => SetValue(ClipOverflowProperty, value);
    }

    /// <summary>Spring for the size (default Arc smooth; chips use morph).</summary>
    public SpringSpec Spring { get; set; } = ArcMotionPresets.Smooth;

    /// <summary>Use the morph spring (XAML-friendly switch for chips that morph their width).</summary>
    public bool UseMorphSpring
    {
        get => Spring == ArcMotionPresets.Morph;
        set => Spring = value ? ArcMotionPresets.Morph : ArcMotionPresets.Smooth;
    }

    private MotionValue<double>? _size;
    private double _target = double.NaN;
    private double _lastCross = double.NaN;
    private readonly RectangleGeometry _clip = new();

    static MessagesSizeFrame()
    {
        AffectsMeasure<MessagesSizeFrame>(OrientationProperty);
    }

    internal bool IsMoving => _size?.IsAnimating ?? false;

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = Child;
        if (child is null)
        {
            return default;
        }
        var vertical = Orientation == Orientation.Vertical;
        child.Measure(vertical ? availableSize.WithHeight(double.PositiveInfinity) : availableSize.WithWidth(double.PositiveInfinity));
        var desired = child.DesiredSize;
        var target = vertical ? desired.Height : desired.Width;
        var cross = vertical ? availableSize.Width : availableSize.Height;
        if (_size is null)
        {
            _size = new MotionValue<double>(this, target, MotionVectors.Double);
            _size.Changed += _ => InvalidateMeasure();
            _target = target;
        }
        else if (!Same(target, _target))
        {
            // Only content changes spring; a different available cross size (resize, reflow) follows at once.
            var passive = !Same(cross, _lastCross);
            _target = target;
            if (passive || !IsLoaded || !ArcMotion.IsFull(this))
            {
                _size.Jump(target);
            }
            else
            {
                _ = _size.AnimateTo(target, MotionSpec.Spring(Spring));
            }
        }
        _lastCross = cross;
        var along = Math.Max(0, _size.Value);
        var result = vertical ? new Size(desired.Width, along) : new Size(along, desired.Height);
        UpdateClip(result);
        return result;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var child = Child;
        if (child is null)
        {
            return finalSize;
        }
        var desired = child.DesiredSize;
        child.Arrange(Orientation == Orientation.Vertical
            ? new Rect(0, 0, finalSize.Width, Math.Max(desired.Height, finalSize.Height))
            : new Rect(0, 0, Math.Max(desired.Width, finalSize.Width), finalSize.Height));
        return finalSize;
    }

    private void UpdateClip(Size size)
    {
        if (_size?.IsAnimating != true)
        {
            Clip = null;
            return;
        }
        var overflow = ClipOverflow;
        _clip.Rect = Orientation == Orientation.Vertical
            ? new Rect(-overflow, 0, size.Width + 2 * overflow, size.Height)
            : new Rect(0, -overflow, size.Width, size.Height + 2 * overflow);
        Clip = _clip;
        InvalidateVisual();
    }

    private static bool Same(double a, double b) => a.Equals(b) || Math.Abs(a - b) < 0.5;
}
