using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc.Primitives;

/// <summary>
/// The surface of a container that flies in a <see cref="SharedElementScope"/> (a quick look growing out of a card):
/// background, border, shadow and a rounded clip for its child, drawn in <see cref="Render"/>. While the scope scales the
/// frame non-uniformly, its corners are scale-corrected so they stay circular, and the radius morphs from the source's
/// to <see cref="CornerRadius"/> (P8). At rest it behaves like a clipping <see cref="Border"/>.
/// The frame owns its child's <see cref="Visual.Clip"/>.
/// </summary>
public class SharedElementFrame : Decorator
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        Border.BackgroundProperty.AddOwner<SharedElementFrame>();

    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        Border.BorderBrushProperty.AddOwner<SharedElementFrame>();

    public static readonly StyledProperty<Thickness> BorderThicknessProperty =
        Border.BorderThicknessProperty.AddOwner<SharedElementFrame>();

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<SharedElementFrame>();

    public static readonly StyledProperty<BoxShadows> BoxShadowProperty =
        Border.BoxShadowProperty.AddOwner<SharedElementFrame>();

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    /// <summary>Uniform border width (the left value is used for every side).</summary>
    public Thickness BorderThickness { get => GetValue(BorderThicknessProperty); set => SetValue(BorderThicknessProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public BoxShadows BoxShadow { get => GetValue(BoxShadowProperty); set => SetValue(BoxShadowProperty, value); }

    // Two clip geometries used in turn: the next clip is updated off-screen and assigned, so no geometry is allocated per frame.
    private readonly RectangleGeometry[] _clips = [new RectangleGeometry(), new RectangleGeometry()];
    private int _clipIndex;
    private double _scaleX = 1;
    private double _scaleY = 1;
    private CornerRadius? _flightRadius;
    private Pen? _pen;
    private IBrush? _penBrush;
    private double _penThickness;

    static SharedElementFrame()
    {
        AffectsRender<SharedElementFrame>(BackgroundProperty, BorderBrushProperty, CornerRadiusProperty, BoxShadowProperty);
        AffectsMeasure<SharedElementFrame>(BorderThicknessProperty);
    }

    /// <summary>True while a scope is scaling this frame.</summary>
    public bool IsFlying => _flightRadius is not null;

    /// <summary>The radius currently drawn (the morphing radius while flying).</summary>
    public CornerRadius CurrentRadius => _flightRadius ?? CornerRadius;

    /// <summary>
    /// Called by <see cref="SharedElementScope"/> every frame: the frame is drawn scaled by
    /// (<paramref name="scaleX"/>, <paramref name="scaleY"/>) and should read as <paramref name="radius"/>.
    /// </summary>
    public void ApplyFlight(double scaleX, double scaleY, CornerRadius radius)
    {
        _scaleX = double.IsFinite(scaleX) && scaleX > 0 ? scaleX : 1;
        _scaleY = double.IsFinite(scaleY) && scaleY > 0 ? scaleY : 1;
        _flightRadius = radius;
        UpdateClip();
        InvalidateVisual();
    }

    /// <summary>Returns to the resting look (no scale correction, own radius).</summary>
    public void EndFlight()
    {
        if (_flightRadius is null && _scaleX == 1 && _scaleY == 1)
        {
            return;
        }
        _scaleX = 1;
        _scaleY = 1;
        _flightRadius = null;
        UpdateClip();
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CornerRadiusProperty || change.Property == BorderThicknessProperty)
        {
            UpdateClip();
        }
    }

    private double Stroke => Math.Max(0, BorderThickness.Left);

    protected override Size MeasureOverride(Size availableSize)
    {
        var inset = Padding + new Thickness(Stroke);
        if (Child is not { } child)
        {
            return new Size(inset.Left + inset.Right, inset.Top + inset.Bottom);
        }
        child.Measure(availableSize.Deflate(inset));
        return child.DesiredSize.Inflate(inset);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(finalSize).Deflate(Padding + new Thickness(Stroke)));
        UpdateClip();
        return finalSize;
    }

    private void UpdateClip()
    {
        if (Child is not { } child)
        {
            return;
        }
        var radius = Math.Max(0, CurrentRadius.TopLeft - Stroke);
        var radii = SharedElementMath.CompensateRadius(radius, _scaleX, _scaleY);
        var rect = new Rect(child.Bounds.Size);
        if (child.Clip is RectangleGeometry current && current.Rect == rect && current.RadiusX.Equals(radii.X) && current.RadiusY.Equals(radii.Y))
        {
            return;
        }
        _clipIndex ^= 1;
        var next = _clips[_clipIndex];
        next.Rect = rect;
        next.RadiusX = radii.X;
        next.RadiusY = radii.Y;
        child.Clip = next;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        var radius = CurrentRadius;
        var outer = Rounded(bounds, radius, 0);
        context.DrawRectangle(Background, null, outer, BoxShadow);
        var stroke = Stroke;
        if (stroke <= 0 || BorderBrush is not { } brush)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_penBrush, brush) || !_penThickness.Equals(stroke))
        {
            _pen = new Pen(brush, stroke);
            _penBrush = brush;
            _penThickness = stroke;
        }
        var half = stroke / 2;
        context.DrawRectangle(null, _pen, Rounded(bounds.Deflate(half), radius, half));
    }

    private RoundedRect Rounded(Rect rect, CornerRadius radius, double inset) => new(rect,
        SharedElementMath.CompensateRadius(radius.TopLeft - inset, _scaleX, _scaleY),
        SharedElementMath.CompensateRadius(radius.TopRight - inset, _scaleX, _scaleY),
        SharedElementMath.CompensateRadius(radius.BottomRight - inset, _scaleX, _scaleY),
        SharedElementMath.CompensateRadius(radius.BottomLeft - inset, _scaleX, _scaleY));
}
