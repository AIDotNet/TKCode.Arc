using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The circle of an <see cref="AvatarUpload"/>: the picture cut to a circle (covering it, centered), or with no picture a
/// muted disc with a person glyph and a dashed edge that invites a drop. A scrim with a camera glyph fades in over the
/// picture under the pointer, and a stronger scrim covers it while it uploads.
/// </summary>
internal sealed class AvatarUploadStage : Control
{
    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<AvatarUploadStage, IImage?>(nameof(Image));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<AvatarUploadStage, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> EdgeProperty =
        AvaloniaProperty.Register<AvatarUploadStage, IBrush?>(nameof(Edge));

    public static readonly StyledProperty<bool> IsDashedProperty =
        AvaloniaProperty.Register<AvatarUploadStage, bool>(nameof(IsDashed));

    public static readonly StyledProperty<IBrush?> GlyphBrushProperty =
        AvaloniaProperty.Register<AvatarUploadStage, IBrush?>(nameof(GlyphBrush));

    public static readonly StyledProperty<IBrush?> ScrimProperty =
        AvaloniaProperty.Register<AvatarUploadStage, IBrush?>(nameof(Scrim));

    public static readonly StyledProperty<IBrush?> ScrimGlyphBrushProperty =
        AvaloniaProperty.Register<AvatarUploadStage, IBrush?>(nameof(ScrimGlyphBrush));

    /// <summary>The person's initials, drawn in the disc while there is no picture; empty shows the person glyph instead.</summary>
    public static readonly StyledProperty<string?> InitialsProperty =
        AvaloniaProperty.Register<AvatarUploadStage, string?>(nameof(Initials));

    public static readonly StyledProperty<IBrush?> InitialsBrushProperty =
        AvaloniaProperty.Register<AvatarUploadStage, IBrush?>(nameof(InitialsBrush));

    /// <summary>How much of the scrim shows (0 to 1), animated by the theme.</summary>
    public static readonly StyledProperty<double> ScrimAmountProperty =
        AvaloniaProperty.Register<AvatarUploadStage, double>(nameof(ScrimAmount));

    /// <summary>Whether the scrim shows the camera glyph (hover) or nothing (upload, where the ring and percentage speak).</summary>
    public static readonly StyledProperty<bool> IsScrimGlyphVisibleProperty =
        AvaloniaProperty.Register<AvatarUploadStage, bool>(nameof(IsScrimGlyphVisible), true);

    private static readonly Geometry s_person = ArcIcons.User;
    private static readonly Geometry s_camera = ArcIcons.Camera;

    private EllipseGeometry? _clip;
    private Size _clipSize;
    private Pen? _edgePen;
    private Pen? _glyphPen;
    private Pen? _scrimGlyphPen;
    // The initials are laid out once per text, size, brush and font; the disc redraws on every hover and drag-over frame.
    private TextLayout? _initialsLayout;
    private (string Text, double Size, IBrush Brush, FontFamily Family) _initialsKey;

    static AvatarUploadStage()
    {
        AffectsRender<AvatarUploadStage>(ImageProperty, FillProperty, EdgeProperty, IsDashedProperty, GlyphBrushProperty, ScrimProperty,
            ScrimGlyphBrushProperty, ScrimAmountProperty, IsScrimGlyphVisibleProperty, InitialsProperty, InitialsBrushProperty);
    }

    public IImage? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public IBrush? Edge { get => GetValue(EdgeProperty); set => SetValue(EdgeProperty, value); }

    public bool IsDashed { get => GetValue(IsDashedProperty); set => SetValue(IsDashedProperty, value); }

    public IBrush? GlyphBrush { get => GetValue(GlyphBrushProperty); set => SetValue(GlyphBrushProperty, value); }

    public IBrush? Scrim { get => GetValue(ScrimProperty); set => SetValue(ScrimProperty, value); }

    public IBrush? ScrimGlyphBrush { get => GetValue(ScrimGlyphBrushProperty); set => SetValue(ScrimGlyphBrushProperty, value); }

    public string? Initials { get => GetValue(InitialsProperty); set => SetValue(InitialsProperty, value); }

    public IBrush? InitialsBrush { get => GetValue(InitialsBrushProperty); set => SetValue(InitialsBrushProperty, value); }

    public double ScrimAmount { get => GetValue(ScrimAmountProperty); set => SetValue(ScrimAmountProperty, value); }

    public bool IsScrimGlyphVisible { get => GetValue(IsScrimGlyphVisibleProperty); set => SetValue(IsScrimGlyphVisibleProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EdgeProperty || change.Property == IsDashedProperty)
        {
            _edgePen = null;
        }
        else if (change.Property == GlyphBrushProperty)
        {
            _glyphPen = null;
        }
        else if (change.Property == ScrimGlyphBrushProperty)
        {
            _scrimGlyphPen = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        var d = Math.Min(size.Width, size.Height);
        if (d <= 0)
        {
            return;
        }
        var circle = new Rect((size.Width - d) / 2, (size.Height - d) / 2, d, d);
        var center = circle.Center;
        var r = d / 2;
        // The whole square answers the pointer (a click picks a file).
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        if (Image is { } image && image.Size.Width > 0 && image.Size.Height > 0)
        {
            using (context.PushGeometryClip(CircleClip(size, circle)))
            {
                var scale = Math.Max(d / image.Size.Width, d / image.Size.Height);
                var w = image.Size.Width * scale;
                var h = image.Size.Height * scale;
                context.DrawImage(image, new Rect(center.X - w / 2, center.Y - h / 2, w, h));
            }
        }
        else
        {
            if (Fill is { } fill)
            {
                context.DrawEllipse(fill, null, center, r, r);
            }
            if (!string.IsNullOrEmpty(Initials) && InitialsBrush is { } initialsBrush)
            {
                var layout = InitialsLayout(Initials, d * 0.36, initialsBrush);
                layout.Draw(context, new Point(center.X - layout.WidthIncludingTrailingWhitespace / 2, center.Y - layout.Height / 2));
            }
            else if (GlyphBrush is { } glyph)
            {
                var glyphSize = d * 0.42;
                _glyphPen = GlyphPen(_glyphPen, glyph, glyphSize);
                DrawGlyph(context, s_person, _glyphPen, center, glyphSize);
            }
        }
        if (Edge is { } edge)
        {
            _edgePen ??= IsDashed
                ? new Pen(edge, 1.5, new DashStyle(new double[] { 3, 3 }, 0), PenLineCap.Round)
                : new Pen(edge, 1);
            var inset = IsDashed ? 0.75 : 0.5;
            context.DrawEllipse(null, _edgePen, center, r - inset, r - inset);
        }
        var amount = Math.Clamp(ScrimAmount, 0, 1);
        if (amount > 0.001 && Scrim is { } scrim)
        {
            using (context.PushOpacity(amount))
            {
                context.DrawEllipse(scrim, null, center, r, r);
                if (IsScrimGlyphVisible && ScrimGlyphBrush is { } scrimGlyph)
                {
                    var glyphSize = Math.Min(24, d * 0.3);
                    _scrimGlyphPen = GlyphPen(_scrimGlyphPen, scrimGlyph, glyphSize);
                    DrawGlyph(context, s_camera, _scrimGlyphPen, center, glyphSize);
                }
            }
        }
    }

    private TextLayout InitialsLayout(string text, double size, IBrush brush)
    {
        var family = TextElement.GetFontFamily(this);
        var key = (text, Math.Round(size * 4) / 4, brush, family);
        if (_initialsLayout is null || _initialsKey != key)
        {
            _initialsLayout?.Dispose();
            _initialsKey = key;
            _initialsLayout = new TextLayout(text, new Typeface(family, FontStyle.Normal, FontWeight.SemiBold), size, brush);
        }
        return _initialsLayout;
    }

    private EllipseGeometry CircleClip(Size size, Rect circle)
    {
        if (_clip is null || _clipSize != size)
        {
            _clipSize = size;
            _clip = new EllipseGeometry(circle);
        }
        return _clip;
    }

    /// <summary>A pen that draws a 1.75px stroke once the 24-unit glyph is scaled to <paramref name="size"/> (cached while the size holds).</summary>
    private static Pen GlyphPen(Pen? cached, IBrush brush, double size)
    {
        var thickness = 1.75 * 24 / Math.Max(1, size);
        if (cached is not null && Math.Abs(cached.Thickness - thickness) < 1e-6)
        {
            return cached;
        }
        return new Pen(brush, thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
    }

    /// <summary>Draws a 24-unit Lucide glyph at <paramref name="size"/> around <paramref name="center"/>.</summary>
    private static void DrawGlyph(DrawingContext context, Geometry glyph, Pen pen, Point center, double size)
    {
        var scale = size / 24;
        var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushTransform(matrix))
        {
            context.DrawGeometry(null, pen, glyph);
        }
    }
}
