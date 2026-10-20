using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Which dimensions of a <see cref="ProgressSizeFrame"/> spring.</summary>
internal enum ProgressSizeAxis
{
    Width,
    Height,
    Both
}

/// <summary>
/// P5 / P6 for the Progress category: the frame's size follows its child's desired size on springs (width on morph,
/// height on smooth by default). Only content-driven changes animate; the first measure and passive changes (a new
/// available size) jump. The child is aligned inside the moving frame (a status badge grows from its right edge, a
/// countdown shell from its center), and the frame clips only while it moves.
/// </summary>
internal sealed class ProgressSizeFrame : Decorator
{
    public static readonly StyledProperty<ProgressSizeAxis> AxisProperty =
        AvaloniaProperty.Register<ProgressSizeFrame, ProgressSizeAxis>(nameof(Axis));

    public static readonly StyledProperty<ArcSpring> WidthSpringProperty =
        AvaloniaProperty.Register<ProgressSizeFrame, ArcSpring>(nameof(WidthSpring), ArcSpring.Morph);

    public static readonly StyledProperty<ArcSpring> HeightSpringProperty =
        AvaloniaProperty.Register<ProgressSizeFrame, ArcSpring>(nameof(HeightSpring), ArcSpring.Smooth);

    public static readonly StyledProperty<HorizontalAlignment> ChildAlignmentProperty =
        AvaloniaProperty.Register<ProgressSizeFrame, HorizontalAlignment>(nameof(ChildAlignment), HorizontalAlignment.Center);

    public ProgressSizeAxis Axis { get => GetValue(AxisProperty); set => SetValue(AxisProperty, value); }
    public ArcSpring WidthSpring { get => GetValue(WidthSpringProperty); set => SetValue(WidthSpringProperty, value); }
    public ArcSpring HeightSpring { get => GetValue(HeightSpringProperty); set => SetValue(HeightSpringProperty, value); }
    public HorizontalAlignment ChildAlignment { get => GetValue(ChildAlignmentProperty); set => SetValue(ChildAlignmentProperty, value); }

    private MotionValue<double>? _width;
    private MotionValue<double>? _height;
    private Size? _lastAvailable;

    static ProgressSizeFrame()
    {
        AffectsMeasure<ProgressSizeFrame>(AxisProperty);
        AffectsArrange<ProgressSizeFrame>(ChildAlignmentProperty);
    }

    internal bool IsMoving => _width?.IsAnimating == true || _height?.IsAnimating == true;

    private static bool Same(double a, double b) => a.Equals(b) || Math.Abs(a - b) < 0.5;

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = Child;
        if (child is null)
        {
            return default;
        }
        var axis = Axis;
        child.Measure(new Size(
            axis == ProgressSizeAxis.Height ? availableSize.Width : double.PositiveInfinity,
            axis == ProgressSizeAxis.Width ? availableSize.Height : double.PositiveInfinity));
        var target = child.DesiredSize;
        var passive = _lastAvailable is { } last && !(Same(last.Width, availableSize.Width) && Same(last.Height, availableSize.Height));
        _lastAvailable = availableSize;
        var animate = !passive && IsLoaded && ArcMotion.IsFull(this);
        var width = Follow(ref _width, target.Width, animate && axis != ProgressSizeAxis.Height, WidthSpring);
        var height = Follow(ref _height, target.Height, animate && axis != ProgressSizeAxis.Width, HeightSpring);
        var size = new Size(axis == ProgressSizeAxis.Height ? target.Width : width, axis == ProgressSizeAxis.Width ? target.Height : height);
        Clip = IsMoving ? new RectangleGeometry(new Rect(size)) : null;
        return new Size(Math.Min(size.Width, availableSize.Width), Math.Min(size.Height, availableSize.Height));
    }

    private double Follow(ref MotionValue<double>? value, double target, bool animate, ArcSpring spring)
    {
        if (value is null)
        {
            value = new MotionValue<double>(this, target, MotionVectors.Double);
            value.Changed += _ => InvalidateMeasure();
            return target;
        }
        if (!Same(value.Target, target))
        {
            if (animate)
            {
                _ = value.AnimateTo(target, MotionSpec.Spring(spring));
            }
            else
            {
                value.Jump(target);
            }
        }
        return Math.Max(0, value.Value);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var child = Child;
        if (child is null)
        {
            return finalSize;
        }
        var desired = child.DesiredSize;
        var width = Axis == ProgressSizeAxis.Height ? finalSize.Width : desired.Width;
        var x = ChildAlignment switch
        {
            HorizontalAlignment.Left => 0,
            HorizontalAlignment.Right => finalSize.Width - width,
            HorizontalAlignment.Stretch => 0,
            _ => (finalSize.Width - width) / 2
        };
        var height = Axis == ProgressSizeAxis.Width ? finalSize.Height : desired.Height;
        child.Arrange(new Rect(x, Axis == ProgressSizeAxis.Width ? 0 : (finalSize.Height - height) / 2, width, height));
        if (Clip is not null)
        {
            Clip = new RectangleGeometry(new Rect(finalSize));
        }
        return finalSize;
    }
}
