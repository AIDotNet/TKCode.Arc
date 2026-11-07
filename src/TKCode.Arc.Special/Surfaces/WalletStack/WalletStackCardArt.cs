using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>The two font families a card face uses: the display face (issuer, number, wordmark) and the body face.</summary>
internal readonly record struct WalletStackFonts(FontFamily Display, FontFamily Body);

/// <summary>
/// The static face of one card (Arc Wallet stack, design reference), rendered once into a bitmap per card width, theme
/// and render scaling: the material gradients, the procedural texture (blended), the sweep, the EMV chip, the
/// contactless mark, the embossed number, holder, expiry, the fictional network mark and the bevelled edge. Every
/// detail is a multiple of the card width (Arc's <c>cqw</c>), so a narrower wallet scales the whole face. Only the
/// transforms, the opacity and the pointer light move per frame.
/// </summary>
internal sealed class WalletStackCardArt : IDisposable
{
    /// <summary>Card corner radius in cqw (3.18 mm on an 85.6 mm card).</summary>
    public const double CornerCqw = 3.715;

    private WalletStackCardArt(Bitmap face, WalletStackMaterialStyle style)
    {
        Face = face;
        Style = style;
    }

    /// <summary>The rendered face (device pixels; draw it with its <see cref="Bitmap.PixelSize"/> as the source rect).</summary>
    public Bitmap Face { get; }

    /// <summary>The material's light blend and colors.</summary>
    public WalletStackMaterialStyle Style { get; }

    /// <summary>The blurred, saturated copy that frosted glass shows (created on first use; null when unavailable).</summary>
    public Bitmap? Frosted { get; private set; }

    /// <summary>The transparent margin around <see cref="Frosted"/> in DIPs.</summary>
    public const double FrostPad = 48;

    private bool _frostTried;

    /// <summary>Creates (once) and returns the frosted copy of the face.</summary>
    public Bitmap? GetFrosted(Size size)
    {
        if (!_frostTried)
        {
            _frostTried = true;
            try
            {
                Frosted = WalletStackTextures.Frost(Face, size, FrostPad, 24, 1.4);
            }
            catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or NotImplementedException)
            {
                // Platforms that cannot read pixels back (the headless test renderer) show glass without the blur.
                Frosted = null;
            }
        }
        return Frosted;
    }

    public void Dispose()
    {
        Face.Dispose();
        Frosted?.Dispose();
    }

    /// <summary>The rounded rectangle of a card of <paramref name="size"/>.</summary>
    public static RoundedRect Shape(Size size) => new(new Rect(size), size.Width / 100 * CornerCqw);

    /// <summary>
    /// Renders the face of <paramref name="card"/> at <paramref name="width"/> DIPs and <paramref name="scaling"/>.
    /// Returns null when the platform cannot render offscreen.
    /// </summary>
    public static WalletStackCardArt? Render(WalletStackCard card, double width, double scaling, bool dark,
                                            WalletStackFonts fonts, WalletStackLabels labels)
    {
        var g = new WalletStackGeometry(width);
        var size = new Size(g.CardWidth, g.CardHeight);
        var pixel = new PixelSize(Math.Max(1, (int)Math.Ceiling(size.Width * scaling)), Math.Max(1, (int)Math.Ceiling(size.Height * scaling)));
        var style = WalletStackMaterialStyle.For(card, dark);
        RenderTargetBitmap bitmap;
        try
        {
            bitmap = new RenderTargetBitmap(pixel, new Vector(96 * scaling, 96 * scaling));
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or NotImplementedException)
        {
            return null;
        }
        using (var context = bitmap.CreateDrawingContext())
        {
            Draw(context, card, g, style, scaling, fonts, labels);
        }
        return new WalletStackCardArt(bitmap, style);
    }

    private static void Draw(DrawingContext context, WalletStackCard card, in WalletStackGeometry g, WalletStackMaterialStyle style,
                             double scaling, WalletStackFonts fonts, WalletStackLabels labels)
    {
        var size = new Size(g.CardWidth, g.CardHeight);
        var rect = new Rect(size);
        var shape = Shape(size);
        using (context.PushClip(shape))
        {
            // Material.
            foreach (var brush in style.Material(size))
            {
                context.DrawRectangle(brush, null, rect);
            }
            if (card.Artwork is { } artwork)
            {
                DrawFill(context, artwork, rect);
            }
            // Texture: blended procedural noise (alpha = the layer's opacity).
            DrawNoise(context, style, size, scaling);
            if (style.Kind == WalletStackMaterial.Black)
            {
                var guilloche = WalletStackTextures.Guilloche();
                var fit = Math.Max(size.Width / 856, size.Height / 540);
                var offset = new Point((size.Width - 856 * fit) / 2, (size.Height - 540 * fit) / 2);
                using (context.PushTransform(Matrix.CreateScale(fit, fit) * Matrix.CreateTranslation(offset.X, offset.Y)))
                {
                    context.DrawGeometry(null, new Pen(new ImmutableSolidColorBrush(Oklch.ToColor(0.8, 0.03, 250, 0.2)), 1.7), guilloche);
                }
            }
            // Sweep (static reflection bands).
            foreach (var brush in style.Sweep(size))
            {
                context.DrawRectangle(brush, null, rect);
            }

            DrawContent(context, card, g, style, fonts, labels);

            // The bevelled edge: an inner hairline, a light top lip, a dark bottom lip and faint side lips.
            Lip(context, shape, new Vector(0, 1), style.EdgeTop);
            Lip(context, shape, new Vector(0, -1), style.EdgeBottom);
            Lip(context, shape, new Vector(1, 0), Oklch.ToColor(1, 0, 0, 0.12));
            Lip(context, shape, new Vector(-1, 0), Oklch.ToColor(0, 0, 0, 0.06));
            var hairline = new RoundedRect(rect.Deflate(0.25), shape.RadiiTopLeft.X - 0.25);
            context.DrawRectangle(null, new Pen(new ImmutableSolidColorBrush(style.EdgeLine), 0.5), hairline);
        }
    }

    private static void DrawFill(DrawingContext context, IImage image, Rect rect)
    {
        var source = image.Size;
        if (source.Width <= 0 || source.Height <= 0)
        {
            return;
        }
        var scale = Math.Max(rect.Width / source.Width, rect.Height / source.Height);
        var crop = new Size(rect.Width / scale, rect.Height / scale);
        var sourceRect = new Rect((source.Width - crop.Width) / 2, (source.Height - crop.Height) / 2, crop.Width, crop.Height);
        context.DrawImage(image, sourceRect, rect);
    }

    private static void DrawNoise(DrawingContext context, WalletStackMaterialStyle style, Size size, double scaling)
    {
        var pixel = new PixelSize(Math.Max(1, (int)Math.Ceiling(size.Width * scaling)), Math.Max(1, (int)Math.Ceiling(size.Height * scaling)));
        WriteableBitmap? noise;
        try
        {
            noise = WalletStackTextures.Noise(pixel, scaling, style.NoiseKind, style.NoiseFrequency, style.NoiseOpacity, style.NoiseSeed);
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or NotImplementedException)
        {
            return;
        }
        if (noise is null)
        {
            return;
        }
        using (noise)
        using (context.PushRenderOptions(new RenderOptions { BitmapBlendingMode = style.NoiseBlend }))
        {
            context.DrawImage(noise, new Rect(0, 0, pixel.Width, pixel.Height), new Rect(size));
        }
    }

    // An inset shadow with an offset and no blur: the part of the card that the card moved by the offset does not cover.
    private static void Lip(DrawingContext context, RoundedRect shape, Vector offset, Color color)
    {
        if (color.A == 0)
        {
            return;
        }
        var whole = new RectangleGeometry(shape.Rect, shape.RadiiTopLeft.X, shape.RadiiTopLeft.Y);
        var moved = new RectangleGeometry(shape.Rect.Translate(offset), shape.RadiiTopLeft.X, shape.RadiiTopLeft.Y);
        context.DrawGeometry(new ImmutableSolidColorBrush(color), null, new CombinedGeometry(GeometryCombineMode.Exclude, whole, moved));
    }

    private static void DrawContent(DrawingContext context, WalletStackCard card, in WalletStackGeometry g, WalletStackMaterialStyle style,
                                    WalletStackFonts fonts, WalletStackLabels labels)
    {
        var q = g.Cqw;
        var w = g.CardWidth;
        var h = g.CardHeight;
        var ink = new ImmutableSolidColorBrush(style.Ink);
        var muted = new ImmutableSolidColorBrush(Oklch.WithAlpha(style.Ink, style.ProductAlpha));
        var display = new Typeface(fonts.Display, FontStyle.Normal, FontWeight.Medium);
        var body = new Typeface(fonts.Body);

        // Issuer (top left) and product (top right), line-height 1.
        using (var issuer = Layout(new Text(card.Issuer, display, 5.4 * q, -0.02, MaxWidth: w * 0.56), ink))
        {
            issuer.Draw(context, new Point(7 * q, LineTop(6.8 * q, 5.4 * q, issuer)));
        }
        using (var product = Layout(new Text(card.Product, body, 3.3 * q, 0, MaxWidth: w * 0.34), muted))
        {
            product.Draw(context, new Point(w - 7 * q - product.WidthIncludingTrailingWhitespace, LineTop(7.6 * q, 3.3 * q, product)));
        }

        // EMV chip.
        DrawChip(context, new Rect(8.5 * q, 21.5 * q, 13.6 * q, 13.6 * q * 0.76), style.GoldChip);

        // Contactless mark: four concentric arcs opening to the right.
        DrawContactless(context, new Rect(25.5 * q, 23.4 * q, 6.6 * q, 6.6 * q), muted);

        // Number: the masked groups and the last four digits, embossed.
        var numberInk = new ImmutableSolidColorBrush(style.NumberInk);
        var maskInk = new ImmutableSolidColorBrush(Oklch.WithAlpha(style.NumberInk, 0.82));
        var emboss = new Emboss(new ImmutableSolidColorBrush(style.EmbossLight), new ImmutableSolidColorBrush(style.EmbossDark));
        var digits = new Text(card.LastFour, display, 5.6 * q, 0.09, Tabular: true);
        var dots = new Text("••••", display, 4.4 * q, 0.02);
        double digitsHeight, dotsHeight, dotsWidth;
        using (var measure = Layout(digits, numberInk))
        {
            digitsHeight = measure.Height;
        }
        using (var measure = Layout(dots, maskInk))
        {
            dotsHeight = measure.Height;
            dotsWidth = measure.WidthIncludingTrailingWhitespace;
        }
        var numberTop = 37.6 * q;
        var x = 7 * q;
        for (var group = 0; group < 3; group++)
        {
            Embossed(context, dots, new Point(x, numberTop + (digitsHeight - dotsHeight) / 2), 0.18 * q, emboss, maskInk);
            x += dotsWidth + 2.2 * q;
        }
        Embossed(context, digits, new Point(x, numberTop), 0.18 * q, emboss, numberInk);

        // Holder (bottom left) and expiry (bottom, 52cqw from the left); line boxes end 6.6cqw above the bottom.
        var bottom = h - 6.6 * q;
        var holder = new Text(card.Holder, body, 3.5 * q, 0.02, MaxWidth: w * 0.42);
        Embossed(context, holder, new Point(7 * q, bottom - Height(holder)), 0.14 * q, emboss, ink);
        if (!string.IsNullOrEmpty(card.Expires))
        {
            var value = new Text(card.Expires!, body, 3.5 * q, 0.04, Tabular: true);
            var valueTop = bottom - Height(value);
            Embossed(context, value, new Point(52 * q, valueTop), 0.14 * q, emboss, ink);
            using var label = Layout(new Text(labels.ValidThru, body, 2.2 * q, 0), muted);
            label.Draw(context, new Point(52 * q, valueTop - 0.8 * q - label.Height));
        }

        // Network mark (bottom right).
        if (card.ShowNetwork)
        {
            var markSize = 6.2 * q;
            var right = w - 6.4 * q;
            var markBottom = h - 5.6 * q;
            if (card.NetworkMark is { } custom)
            {
                var ratio = custom.Size.Height > 0 ? custom.Size.Width / custom.Size.Height : 1;
                var markWidth = markSize * ratio;
                context.DrawImage(custom, new Rect(right - markWidth, markBottom - markSize, markWidth, markSize));
            }
            else
            {
                using var word = Layout(new Text(labels.NetworkName, display, 4.2 * q, -0.03), ink);
                var wordLeft = right - word.WidthIncludingTrailingWhitespace;
                word.Draw(context, new Point(wordLeft, markBottom - markSize / 2 - word.Height / 2));
                DrawOrbit(context, new Rect(wordLeft - q - markSize, markBottom - markSize, markSize, markSize), style.Ink);
            }
        }
    }

    // The top of a text layout set in a CSS line box of line-height 1 whose top is lineTop.
    private static double LineTop(double lineTop, double fontSize, TextLayout layout) => lineTop + (fontSize - layout.Height) / 2;

    private readonly record struct Text(string Value, Typeface Typeface, double Size, double LetterSpacingEm,
                                        double MaxWidth = double.PositiveInfinity, bool Tabular = false);

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    private readonly record struct Emboss(IBrush Light, IBrush Dark);

    private static double Height(Text text)
    {
        using var layout = Layout(text, Brushes.Black);
        return layout.Height;
    }

    private static void Embossed(DrawingContext context, Text text, Point origin, double depth, Emboss emboss, IBrush ink)
    {
        // Text-shadow pair: a light copy just below, a dark copy just above (pressed into the card), then the text.
        using (var light = Layout(text, emboss.Light))
        {
            light.Draw(context, origin + new Vector(0, depth));
        }
        using (var dark = Layout(text, emboss.Dark))
        {
            dark.Draw(context, origin + new Vector(0, -depth));
        }
        using var layout = Layout(text, ink);
        layout.Draw(context, origin);
    }

    private static TextLayout Layout(Text text, IBrush brush)
    {
        var trimmed = !double.IsInfinity(text.MaxWidth);
        return new TextLayout(text.Value, text.Typeface, text.Size, brush, TextAlignment.Left, TextWrapping.NoWrap,
            trimmed ? TextTrimming.CharacterEllipsis : TextTrimming.None, null, FlowDirection.LeftToRight,
            trimmed ? text.MaxWidth : double.PositiveInfinity, double.PositiveInfinity, double.NaN,
            text.LetterSpacingEm * text.Size, 1, text.Tabular ? s_tabular : null, null);
    }

    private static readonly double[] s_goldStops = { 0.90, 0.08, 90, 0.76, 0.11, 78, 0.93, 0.06, 95, 0.70, 0.10, 70, 0.84, 0.09, 85 };
    private static readonly double[] s_silverStops = { 0.95, 0.004, 260, 0.74, 0.006, 260, 0.97, 0.003, 260, 0.68, 0.006, 260, 0.86, 0.005, 260 };

    private static ImmutableLinearGradientBrush ChipBrush(bool gold, RelativePoint start, RelativePoint end)
    {
        var values = gold ? s_goldStops : s_silverStops;
        var stops = new List<ImmutableGradientStop>(5);
        for (var i = 0; i < 5; i++)
        {
            stops.Add(new ImmutableGradientStop(i / 4.0, Oklch.ToColor(values[i * 3], values[i * 3 + 1], values[i * 3 + 2])));
        }
        return new ImmutableLinearGradientBrush(stops, 1, null, null, GradientSpreadMethod.Pad, start, end);
    }

    // Our EMV chip: a rounded plate with a diagonal metal gradient, a centre pad with the reverse gradient, contact
    // separators, a top gloss and an outline (design box 50×38).
    private static void DrawChip(DrawingContext context, Rect box, bool gold)
    {
        var s = box.Width / 50;
        using var _ = context.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(box.X, box.Y));
        var plate = new RoundedRect(new Rect(0.5, 0.5, 49, 37), 7);
        var shadow = new BoxShadows(new BoxShadow { OffsetY = 0.25 * 100 / 50 * 0.5, Blur = 0.6, Color = Oklch.ToColor(0, 0, 0, 0.28) });
        context.DrawRectangle(ChipBrush(gold, RelativePoint.TopLeft, RelativePoint.BottomRight), null, plate, shadow);
        var pad = new RoundedRect(new Rect(16, 8, 18, 22), 3.5);
        var line = new Pen(new ImmutableSolidColorBrush(Oklch.ToColor(0, 0, 0, 0.32)), 0.7);
        context.DrawRectangle(ChipBrush(gold, new RelativePoint(1, 0, RelativeUnit.Relative), new RelativePoint(0, 1, RelativeUnit.Relative)), line, pad);
        context.DrawLine(line, new Point(0.5, 12.5), new Point(16, 12.5));
        context.DrawLine(line, new Point(0.5, 25.5), new Point(16, 25.5));
        context.DrawLine(line, new Point(34, 12.5), new Point(49.5, 12.5));
        context.DrawLine(line, new Point(34, 25.5), new Point(49.5, 25.5));
        context.DrawLine(line, new Point(25, 0.5), new Point(25, 8));
        context.DrawLine(line, new Point(25, 30), new Point(25, 37.5));
        context.DrawLine(line, new Point(13, 19), new Point(16, 19));
        context.DrawLine(line, new Point(34, 19), new Point(37, 19));
        var gloss = new ImmutableLinearGradientBrush(
            new[] { new ImmutableGradientStop(0, Oklch.ToColor(1, 0, 0, 0.55)), new ImmutableGradientStop(0.45, Oklch.ToColor(1, 0, 0, 0)) },
            1, null, null, GradientSpreadMethod.Pad, RelativePoint.TopLeft, new RelativePoint(0, 1, RelativeUnit.Relative));
        context.DrawRectangle(gloss, new Pen(new ImmutableSolidColorBrush(Oklch.ToColor(0, 0, 0, 0.28)), 0.7), plate);
    }

    private static void DrawContactless(DrawingContext context, Rect box, IBrush ink)
    {
        var s = box.Width / 24;
        using var _ = context.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(box.X, box.Y));
        var pen = new Pen(ink, 1.7, lineCap: PenLineCap.Round);
        ReadOnlySpan<double> xs = stackalloc double[] { 5.5, 9, 12.5, 16 };
        ReadOnlySpan<double> hs = stackalloc double[] { 6, 11, 16, 21 };
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            for (var i = 0; i < 4; i++)
            {
                var half = hs[i] / 2;
                g.BeginFigure(new Point(xs[i], 12 - half), false);
                g.ArcTo(new Point(xs[i], 12 + half), new Size(hs[i] * 0.62, hs[i] * 0.62), 0, false, SweepDirection.Clockwise);
                g.EndFigure(false);
            }
        }
        context.DrawGeometry(null, pen, geometry);
    }

    // Our fictional network mark "orbit": two equal stroked rings side by side, the right one fainter.
    private static void DrawOrbit(DrawingContext context, Rect box, Color ink)
    {
        var radius = box.Width * 0.31;
        var stroke = box.Width * 2.2 / 24;
        var cy = box.Center.Y;
        var left = new Point(box.Center.X - box.Width * 0.18, cy);
        var right = new Point(box.Center.X + box.Width * 0.18, cy);
        context.DrawEllipse(null, new Pen(new ImmutableSolidColorBrush(ink), stroke), left, radius - stroke / 2, radius - stroke / 2);
        context.DrawEllipse(null, new Pen(new ImmutableSolidColorBrush(Oklch.WithAlpha(ink, 0.7 * ink.A / 255.0)), stroke), right,
            radius - stroke / 2, radius - stroke / 2);
    }

    /// <summary>The CSS linear-gradient line for <paramref name="degrees"/> over <paramref name="size"/>, as absolute points.</summary>
    public static (RelativePoint Start, RelativePoint End) CssLine(double degrees, Size size)
    {
        var a = degrees * Math.PI / 180;
        var dx = Math.Sin(a);
        var dy = -Math.Cos(a);
        var half = (Math.Abs(size.Width * dx) + Math.Abs(size.Height * dy)) / 2;
        var c = new Point(size.Width / 2, size.Height / 2);
        return (new RelativePoint(c.X - dx * half, c.Y - dy * half, RelativeUnit.Absolute),
                new RelativePoint(c.X + dx * half, c.Y + dy * half, RelativeUnit.Absolute));
    }
}
