using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The Lucide glyphs the player draws (24×24, Lucide 0.5xx paths).</summary>
internal static class MediaPlayerGlyphs
{
    public static Geometry Play => ArcIcons.Play;

    public static Geometry Pause => ArcIcons.Pause;

    /// <summary>Lucide "skip-back".</summary>
    public static Geometry SkipBack { get; } =
        ArcIcons.Parse("M17.971 4.285A2 2 0 0 1 21 6v12a2 2 0 0 1-3.029 1.715l-9.997-5.998a2 2 0 0 1-.003-3.432z M3 20V4");

    /// <summary>Lucide "skip-forward".</summary>
    public static Geometry SkipForward { get; } =
        ArcIcons.Parse("M21 4v16 M6.029 4.285A2 2 0 0 0 3 6v12a2 2 0 0 0 3.029 1.715l9.997-5.998a2 2 0 0 0 .003-3.432z");

    public static Geometry Volume => ArcIcons.Volume2;

    /// <summary>Lucide "volume-x".</summary>
    public static Geometry VolumeOff { get; } =
        ArcIcons.Parse("M11 4.702a.7.7 0 0 0-1.203-.498L6.413 7.587A1.4 1.4 0 0 1 5.416 8H3a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h2.416a1.4 1.4 0 0 1 .997.413l3.383 3.384A.7.7 0 0 0 11 19.298z M16.5 14.5l5-5 M16.5 9.5l5 5");
}

/// <summary>
/// An <see cref="ArcGlyph"/> that can also fill its shape: the transport glyphs are solid (filled and stroked in the same
/// color, like an SVG with <c>fill="currentColor"</c>), the volume glyphs stay outlined.
/// </summary>
internal sealed class MediaPlayerGlyph : ArcGlyph
{
    /// <summary>Whether the glyph's closed shapes are filled with its foreground.</summary>
    public static readonly StyledProperty<bool> IsFilledProperty =
        AvaloniaProperty.Register<MediaPlayerGlyph, bool>(nameof(IsFilled));

    static MediaPlayerGlyph()
    {
        AffectsRender<MediaPlayerGlyph>(IsFilledProperty);
    }

    /// <inheritdoc cref="IsFilledProperty"/>
    public bool IsFilled { get => GetValue(IsFilledProperty); set => SetValue(IsFilledProperty, value); }

    public override void Render(DrawingContext context)
    {
        var data = Data;
        if (IsFilled && data is not null && Foreground is { } brush)
        {
            var scale = Math.Min(Bounds.Width, Bounds.Height) / 24.0;
            var offsetX = (Bounds.Width - 24 * scale) / 2;
            var offsetY = (Bounds.Height - 24 * scale) / 2;
            using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
            {
                context.DrawGeometry(brush, null, data);
            }
        }
        base.Render(context);
    }
}
