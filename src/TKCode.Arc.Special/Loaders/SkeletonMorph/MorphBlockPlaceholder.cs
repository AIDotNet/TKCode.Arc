using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The skeleton of one <see cref="MorphBlock"/>, drawn over its content: a filled shape (avatar, button) or one rounded
/// bar per text line. The block writes the box (the skeleton box while loading, then the spring from it to the content
/// box) and the opacity (pulse × fade); the box is drawn from the top-left corner and may overflow the block while it
/// stretches, so the placeholder takes no space and no input.
/// </summary>
internal sealed class MorphBlockPlaceholder : Avalonia.Controls.Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<MorphBlockPlaceholder, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<double> BarMaxHeightProperty =
        AvaloniaProperty.Register<MorphBlockPlaceholder, double>(nameof(BarMaxHeight), 12);

    public static readonly StyledProperty<double> BarInsetProperty =
        AvaloniaProperty.Register<MorphBlockPlaceholder, double>(nameof(BarInset), 6);

    public static readonly StyledProperty<double> LastLineRatioProperty =
        AvaloniaProperty.Register<MorphBlockPlaceholder, double>(nameof(LastLineRatio), 0.62);

    static MorphBlockPlaceholder()
    {
        AffectsRender<MorphBlockPlaceholder>(FillProperty, BarMaxHeightProperty, BarInsetProperty, LastLineRatioProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<MorphBlockPlaceholder>(false);
        FocusableProperty.OverrideDefaultValue<MorphBlockPlaceholder>(false);
    }

    /// <summary>The skeleton fill (SurfaceMuted).</summary>
    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>Tallest text bar.</summary>
    public double BarMaxHeight
    {
        get => GetValue(BarMaxHeightProperty);
        set => SetValue(BarMaxHeightProperty, value);
    }

    /// <summary>How much shorter than its line box a bar is.</summary>
    public double BarInset
    {
        get => GetValue(BarInsetProperty);
        set => SetValue(BarInsetProperty, value);
    }

    /// <summary>Width fraction of the last bar of several.</summary>
    public double LastLineRatio
    {
        get => GetValue(LastLineRatioProperty);
        set => SetValue(LastLineRatioProperty, value);
    }

    private Size _box;
    private int _lines;
    private double _lineHeight = 20;
    private double _radius = 6;
    private bool _isCircle;

    /// <summary>The drawn box (top-left anchored).</summary>
    public Size Box => _box;

    public void SetBox(Size box)
    {
        if (_box != box)
        {
            _box = box;
            InvalidateVisual();
        }
    }

    public void SetShape(int lines, double lineHeight, double radius, bool isCircle)
    {
        if (_lines != lines || !_lineHeight.Equals(lineHeight) || !_radius.Equals(radius) || _isCircle != isCircle)
        {
            _lines = lines;
            _lineHeight = lineHeight;
            _radius = radius;
            _isCircle = isCircle;
            InvalidateVisual();
        }
    }

    /// <summary>The bars a text skeleton draws (tests and the snapshot tool read them).</summary>
    internal Rect BarRect(int index)
        => SkeletonMorphMath.Bar(index, _lines, _box.Width, _lineHeight,
            SkeletonMorphMath.BarHeight(_lineHeight, BarMaxHeight, BarInset), LastLineRatio);

    protected override Size MeasureOverride(Size availableSize) => default;

    public override void Render(DrawingContext context)
    {
        var fill = Fill;
        if (fill is null || _box.Width <= 0 || _box.Height <= 0)
        {
            return;
        }
        if (_lines > 0)
        {
            var barHeight = SkeletonMorphMath.BarHeight(_lineHeight, BarMaxHeight, BarInset);
            var ratio = LastLineRatio;
            for (var i = 0; i < _lines; i++)
            {
                var bar = SkeletonMorphMath.Bar(i, _lines, _box.Width, _lineHeight, barHeight, ratio);
                if (bar.Width > 0 && bar.Height > 0)
                {
                    context.DrawRectangle(fill, null, new RoundedRect(bar, bar.Height / 2));
                }
            }
            return;
        }
        var rect = new Rect(_box);
        context.DrawRectangle(fill, null, new RoundedRect(rect, SkeletonMorphMath.ShapeRadius(_box, _radius, _isCircle)));
    }
}
