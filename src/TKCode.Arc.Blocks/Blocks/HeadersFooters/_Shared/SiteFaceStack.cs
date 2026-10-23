// Ported from Arc's free cta-section / newsletter-signup blocks (MIT, Copyright (c) 2026 Elia Kuratli): the overlapping faces.
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A row of overlapping round pictures (Arc's <c>.faces</c>): each is <see cref="Diameter"/> wide including its ring, overlaps
/// the previous one by <see cref="Overlap"/>, and a <see cref="Ring"/>-wide ring in <see cref="RingBrush"/> (the surface
/// behind it) cuts it from its neighbour. Later faces draw over earlier ones. Decorative: no input, hidden from screen
/// readers. Each face can be revealed separately (<see cref="SetReveal"/>): the CTA setup card slides its teammates in.
/// </summary>
internal sealed class SiteFaceStack : Control
{
    /// <summary>The pictures, in drawing order.</summary>
    public static readonly StyledProperty<IList<IImage>?> ImagesProperty =
        AvaloniaProperty.Register<SiteFaceStack, IList<IImage>?>(nameof(Images));

    /// <summary>Outer diameter of one face, ring included (Arc: 26 in the CTA note, 22 on the setup card, 28 for readers).</summary>
    public static readonly StyledProperty<double> DiameterProperty =
        AvaloniaProperty.Register<SiteFaceStack, double>(nameof(Diameter), 26);

    /// <summary>How far each face overlaps the previous one (6).</summary>
    public static readonly StyledProperty<double> OverlapProperty =
        AvaloniaProperty.Register<SiteFaceStack, double>(nameof(Overlap), 6);

    /// <summary>Width of the ring drawn inside the outer edge (2).</summary>
    public static readonly StyledProperty<double> RingProperty =
        AvaloniaProperty.Register<SiteFaceStack, double>(nameof(Ring), 2);

    /// <summary>Ring color: the surface the stack sits on.</summary>
    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<SiteFaceStack, IBrush?>(nameof(RingBrush));

    /// <summary>Color under a picture while it loads or when it has transparent parts.</summary>
    public static readonly StyledProperty<IBrush?> PlaceholderBrushProperty =
        AvaloniaProperty.Register<SiteFaceStack, IBrush?>(nameof(PlaceholderBrush));

    /// <summary>At most this many faces are drawn (0 = all).</summary>
    public static readonly StyledProperty<int> MaxCountProperty =
        AvaloniaProperty.Register<SiteFaceStack, int>(nameof(MaxCount));

    private readonly List<FaceReveal> _reveals = new();
    private INotifyCollectionChanged? _observed;

    static SiteFaceStack()
    {
        AffectsMeasure<SiteFaceStack>(ImagesProperty, DiameterProperty, OverlapProperty, MaxCountProperty);
        AffectsRender<SiteFaceStack>(RingProperty, RingBrushProperty, PlaceholderBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<SiteFaceStack>(false);
        FocusableProperty.OverrideDefaultValue<SiteFaceStack>(false);
    }

    public SiteFaceStack()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <inheritdoc cref="ImagesProperty"/>
    public IList<IImage>? Images { get => GetValue(ImagesProperty); set => SetValue(ImagesProperty, value); }

    /// <inheritdoc cref="DiameterProperty"/>
    public double Diameter { get => GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }

    /// <inheritdoc cref="OverlapProperty"/>
    public double Overlap { get => GetValue(OverlapProperty); set => SetValue(OverlapProperty, value); }

    /// <inheritdoc cref="RingProperty"/>
    public double Ring { get => GetValue(RingProperty); set => SetValue(RingProperty, value); }

    /// <inheritdoc cref="RingBrushProperty"/>
    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }

    /// <inheritdoc cref="PlaceholderBrushProperty"/>
    public IBrush? PlaceholderBrush { get => GetValue(PlaceholderBrushProperty); set => SetValue(PlaceholderBrushProperty, value); }

    /// <inheritdoc cref="MaxCountProperty"/>
    public int MaxCount { get => GetValue(MaxCountProperty); set => SetValue(MaxCountProperty, value); }

    /// <summary>Number of faces drawn.</summary>
    public int Count
    {
        get
        {
            var count = Images?.Count ?? 0;
            return MaxCount > 0 ? Math.Min(count, MaxCount) : count;
        }
    }

    /// <summary>
    /// Sets how face <paramref name="index"/> is drawn: <paramref name="opacity"/>, a horizontal <paramref name="offsetX"/>
    /// and a <paramref name="scale"/> around its centre (rest: 1, 0, 1). The caller animates these (Arc's setup card:
    /// opacity 0→1, x −6→0, scale .9→1, staggered).
    /// </summary>
    public void SetReveal(int index, double opacity, double offsetX, double scale)
    {
        if (index < 0)
        {
            return;
        }
        while (_reveals.Count <= index)
        {
            _reveals.Add(FaceReveal.Rest);
        }
        _reveals[index] = new FaceReveal(opacity, offsetX, scale);
        InvalidateVisual();
    }

    /// <summary>Puts every face back at rest.</summary>
    public void ClearReveal()
    {
        _reveals.Clear();
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ImagesProperty)
        {
            if (_observed is not null)
            {
                _observed.CollectionChanged -= OnImagesChanged;
            }
            _observed = Images as INotifyCollectionChanged;
            if (_observed is not null)
            {
                _observed.CollectionChanged += OnImagesChanged;
            }
        }
    }

    private void OnImagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Count;
        if (count == 0)
        {
            return default;
        }
        var d = Diameter;
        return new Size(d + (count - 1) * (d - Overlap), d);
    }

    public override void Render(DrawingContext context)
    {
        var images = Images;
        var count = Count;
        if (images is null || count == 0)
        {
            return;
        }
        var d = Diameter;
        var ring = Math.Clamp(Ring, 0, d / 2);
        var ringBrush = RingBrush;
        var placeholder = PlaceholderBrush;
        var step = d - Overlap;
        var top = (Bounds.Height - d) / 2;
        for (var i = 0; i < count; i++)
        {
            var reveal = i < _reveals.Count ? _reveals[i] : FaceReveal.Rest;
            if (reveal.Opacity <= 0.001)
            {
                continue;
            }
            var outer = new Rect(i * step + reveal.OffsetX, top, d, d);
            var center = outer.Center;
            using var transform = Math.Abs(reveal.Scale - 1) > 1e-4
                ? context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(reveal.Scale, reveal.Scale) * Matrix.CreateTranslation(center.X, center.Y))
                : default(DrawingContext.PushedState?);
            using var opacity = reveal.Opacity < 0.999 ? context.PushOpacity(reveal.Opacity) : default(DrawingContext.PushedState?);
            if (ringBrush is not null && ring > 0)
            {
                context.DrawEllipse(ringBrush, null, center, d / 2, d / 2);
            }
            var inner = outer.Deflate(ring);
            var radius = inner.Width / 2;
            if (placeholder is not null)
            {
                context.DrawEllipse(placeholder, null, center, radius, radius);
            }
            using (context.PushGeometryClip(new EllipseGeometry(inner)))
            {
                DrawCover(context, images[i], inner);
            }
        }
    }

    /// <summary>Draws <paramref name="image"/> to fill <paramref name="target"/>, cropping the longer side (CSS <c>object-fit: cover</c>).</summary>
    public static void DrawCover(DrawingContext context, IImage image, Rect target)
    {
        var size = image.Size;
        if (size.Width <= 0 || size.Height <= 0 || target.Width <= 0 || target.Height <= 0)
        {
            return;
        }
        var scale = Math.Max(target.Width / size.Width, target.Height / size.Height);
        var w = target.Width / scale;
        var h = target.Height / scale;
        var source = new Rect((size.Width - w) / 2, (size.Height - h) / 2, w, h);
        if (image is Avalonia.Media.Imaging.Bitmap bitmap)
        {
            // DrawImage reads the source rectangle in pixels; Bitmap.Size is in DIPs (implementation guide pitfall table).
            var sx = bitmap.PixelSize.Width / size.Width;
            var sy = bitmap.PixelSize.Height / size.Height;
            source = new Rect(source.X * sx, source.Y * sy, source.Width * sx, source.Height * sy);
        }
        context.DrawImage(image, source, target);
    }

    private readonly record struct FaceReveal(double Opacity, double OffsetX, double Scale)
    {
        public static readonly FaceReveal Rest = new(1, 0, 1);
    }
}
