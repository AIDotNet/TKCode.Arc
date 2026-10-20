using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The divider line of an <see cref="ImageCompare"/>: a 2px line through the pivot at the current angle, long enough to
/// cross the frame at any angle, with a half-pixel dark edge so it reads on light photos. Drawn in one Render call;
/// the owner moves it with <see cref="Place"/> (no layout).
/// </summary>
internal sealed class ImageCompareDivider : Control
{
    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<ImageCompareDivider, IBrush?>(nameof(LineBrush));

    public static readonly StyledProperty<IBrush?> EdgeBrushProperty =
        AvaloniaProperty.Register<ImageCompareDivider, IBrush?>(nameof(EdgeBrush));

    private Pen? _line;
    private Pen? _edge;
    private Point _pivot;
    private double _angle;

    static ImageCompareDivider()
    {
        AffectsRender<ImageCompareDivider>(LineBrushProperty, EdgeBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ImageCompareDivider>(false);
    }

    public IBrush? LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public IBrush? EdgeBrush
    {
        get => GetValue(EdgeBrushProperty);
        set => SetValue(EdgeBrushProperty, value);
    }

    /// <summary>Center of the line in the divider's coordinates.</summary>
    public Point Pivot => _pivot;

    /// <summary>Rotation in degrees: 0 is a vertical line (horizontal compare), 90 a horizontal one.</summary>
    public double Angle => _angle;

    public void Place(Point pivot, double angle)
    {
        if (pivot == _pivot && angle == _angle)
        {
            return;
        }
        _pivot = pivot;
        _angle = angle;
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LineBrushProperty)
        {
            _line = null;
        }
        else if (change.Property == EdgeBrushProperty)
        {
            _edge = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0 || LineBrush is not { } lineBrush)
        {
            return;
        }
        _line ??= new Pen(lineBrush, 2, lineCap: PenLineCap.Flat);
        var radians = _angle * Math.PI / 180;
        var reach = size.Width + size.Height;
        var direction = new Vector(-Math.Sin(radians), Math.Cos(radians)) * reach;
        var from = _pivot - direction;
        var to = _pivot + direction;
        if (EdgeBrush is { } edgeBrush)
        {
            // box-shadow: 0 0 0 .5px — a 3px line under the 2px one leaves half a pixel either side.
            _edge ??= new Pen(edgeBrush, 3, lineCap: PenLineCap.Flat);
            context.DrawLine(_edge, from, to);
        }
        context.DrawLine(_line, from, to);
    }
}
