using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The shadows of a <see cref="WorkflowNodeCard"/>, drawn as one picture made once per card size and kept: the outer
/// shadows under the frame, and the inset ones (the dark theme's top highlight) over the frame's fill, where the card's
/// padding keeps them clear of the content.
/// </summary>
/// <remarks>
/// Avalonia draws a box shadow with a blur image filter every time it draws it, which costs the GPU an offscreen pass per
/// card per frame: 500 cards panning took 80 ms a frame, 8 without shadows (<c>.gpubench</c>, Workflow). The picture is the
/// same Skia shadow, drawn once at twice the size and scaled with the view (a blur stays smooth when scaled).
/// </remarks>
internal sealed class WorkflowCardShadow : Control
{
    public static readonly StyledProperty<BoxShadows> ShadowsProperty =
        AvaloniaProperty.Register<WorkflowCardShadow, BoxShadows>(nameof(Shadows));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<WorkflowCardShadow, CornerRadius>(nameof(CornerRadius));

    /// <summary>The frame's border: inset shadows are drawn inside it, as CSS draws them inside the padding box.</summary>
    public static readonly StyledProperty<Thickness> BorderThicknessProperty =
        AvaloniaProperty.Register<WorkflowCardShadow, Thickness>(nameof(BorderThickness));

    /// <summary>Pixels per unit of the pictures.</summary>
    private const double Scale = 2;

    /// <summary>The pictures made so far, by size, corners and shadows; the oldest goes first beyond the limit.</summary>
    private static readonly Dictionary<(Size Size, CornerRadius Radius, Thickness Border, BoxShadows Shadows), (RenderTargetBitmap Bitmap, Thickness Pad)> s_pictures = new();
    private static readonly Queue<(Size, CornerRadius, Thickness, BoxShadows)> s_order = new();
    private const int Limit = 48;

    static WorkflowCardShadow()
    {
        AffectsRender<WorkflowCardShadow>(ShadowsProperty, CornerRadiusProperty, BorderThicknessProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<WorkflowCardShadow>(false);
        ClipToBoundsProperty.OverrideDefaultValue<WorkflowCardShadow>(false);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<WorkflowCardShadow>(AccessibilityView.Raw);
    }

    public BoxShadows Shadows
    {
        get => GetValue(ShadowsProperty);
        set => SetValue(ShadowsProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    // A sharp inset (no blur, no spread: Arc's dark top highlight) is a band of the frame's inside, drawn as geometry.
    private (Size Size, Geometry Band, IBrush Brush)[] _bands = [];

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (Shadows.Count == 0 || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var blurred = new List<BoxShadow>();
        var bands = new List<BoxShadow>();
        foreach (var shadow in Shadows)
        {
            (shadow.IsInset && shadow.Blur == 0 && shadow.Spread == 0 ? bands : blurred).Add(shadow);
        }
        if (blurred.Count > 0)
        {
            var (bitmap, pad) = Picture(size, CornerRadius, BorderThickness, Of(blurred));
            context.DrawImage(bitmap, new Rect(-pad.Left, -pad.Top, size.Width + pad.Left + pad.Right, size.Height + pad.Top + pad.Bottom));
        }
        if (bands.Count > 0)
        {
            if (_bands.Length != bands.Count || _bands[0].Size != size)
            {
                _bands = bands.Select(b => (size, Band(size, b), (IBrush)new ImmutableSolidColorBrush(b.Color))).ToArray();
            }
            foreach (var (_, band, brush) in _bands)
            {
                context.DrawGeometry(brush, null, band);
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ShadowsProperty || change.Property == CornerRadiusProperty || change.Property == BorderThicknessProperty)
        {
            _bands = [];
        }
    }

    /// <summary>What a sharp inset shadow shows: the frame's inside less the same shape moved by the shadow's offset.</summary>
    private Geometry Band(Size size, BoxShadow shadow)
    {
        var border = BorderThickness;
        var inside = new Rect(size).Deflate(border);
        var radius = Math.Max(0, CornerRadius.TopLeft - border.Left);
        return new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(inside, radius, radius),
            new RectangleGeometry(inside.Translate(new Vector(shadow.OffsetX, shadow.OffsetY)), radius, radius));
    }

    private static (RenderTargetBitmap, Thickness) Picture(Size size, CornerRadius radius, Thickness border, BoxShadows shadows)
    {
        var key = (size, radius, border, shadows);
        if (s_pictures.TryGetValue(key, out var picture))
        {
            return picture;
        }
        // Room for each outer shadow's blur, spread and offset on its side (a blur reaches about its radius past the edge);
        // inset shadows stay inside the card.
        double left = 0, top = 0, right = 0, bottom = 0;
        foreach (var shadow in shadows)
        {
            if (shadow.IsInset)
            {
                continue;
            }
            var reach = shadow.Blur + Math.Max(0, shadow.Spread) + 1;
            left = Math.Max(left, reach - shadow.OffsetX);
            right = Math.Max(right, reach + shadow.OffsetX);
            top = Math.Max(top, reach - shadow.OffsetY);
            bottom = Math.Max(bottom, reach + shadow.OffsetY);
        }
        var pad = new Thickness(Math.Ceiling(left), Math.Ceiling(top), Math.Ceiling(right), Math.Ceiling(bottom));
        var full = new Size(size.Width + pad.Left + pad.Right, size.Height + pad.Top + pad.Bottom);
        var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(full.Width * Scale), (int)Math.Ceiling(full.Height * Scale)),
            new Vector(96 * Scale, 96 * Scale));
        using (var context = bitmap.CreateDrawingContext())
        {
            // Outer shadows are drawn outside the card's outline, inset ones inside its border, as in CSS; the fill is clear.
            var card = new RoundedRect(new Rect(new Point(pad.Left, pad.Top), size), radius);
            var outer = Outer(shadows);
            if (outer.Count > 0)
            {
                context.DrawRectangle(Brushes.Transparent, null, card, outer);
            }
            var inset = Inset(shadows);
            if (inset.Count > 0)
            {
                var inside = card.Rect.Deflate(border);
                var r = new CornerRadius(Math.Max(0, radius.TopLeft - border.Left), Math.Max(0, radius.TopRight - border.Right),
                    Math.Max(0, radius.BottomRight - border.Right), Math.Max(0, radius.BottomLeft - border.Left));
                context.DrawRectangle(Brushes.Transparent, null, new RoundedRect(inside, r), inset);
            }
        }
        picture = (bitmap, pad);
        s_pictures[key] = picture;
        s_order.Enqueue(key);
        if (s_order.Count > Limit)
        {
            // Not disposed: a frame being drawn may still hold it; the collector frees it.
            s_pictures.Remove(s_order.Dequeue());
        }
        return picture;
    }

    /// <summary>The outer shadows of <paramref name="shadows"/>.</summary>
    internal static BoxShadows Outer(BoxShadows shadows) => Pick(shadows, inset: false);

    /// <summary>The inset shadows of <paramref name="shadows"/>.</summary>
    internal static BoxShadows Inset(BoxShadows shadows) => Pick(shadows, inset: true);

    private static BoxShadows Pick(BoxShadows shadows, bool inset)
    {
        var picked = new List<BoxShadow>(shadows.Count);
        foreach (var shadow in shadows)
        {
            if (shadow.IsInset == inset)
            {
                picked.Add(shadow);
            }
        }
        return Of(picked);
    }

    private static BoxShadows Of(List<BoxShadow> picked)
        => picked.Count switch
        {
            0 => default,
            1 => new BoxShadows(picked[0]),
            _ => new BoxShadows(picked[0], picked.Skip(1).ToArray())
        };
}
