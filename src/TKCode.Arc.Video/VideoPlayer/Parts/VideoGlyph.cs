using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The Lucide glyphs the video player draws (24×24, from lucide-static 1.59, ISC).</summary>
internal static class VideoGlyphs
{
    public static Geometry Play => ArcIcons.Play;

    public static Geometry Pause => ArcIcons.Pause;

    public static Geometry Replay => ArcIcons.RotateCcw;

    public static Geometry Volume => ArcIcons.Volume2;

    public static Geometry Speed => ArcIcons.Gauge;

    public static Geometry Error => ArcIcons.CircleAlert;

    /// <summary>Lucide "volume-1".</summary>
    public static Geometry VolumeLow { get; } =
        ArcIcons.Parse("M11 4.702a.705.705 0 0 0-1.203-.498L6.413 7.587A1.4 1.4 0 0 1 5.416 8H3a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h2.416a1.4 1.4 0 0 1 .997.413l3.383 3.384A.705.705 0 0 0 11 19.298z M16 9a5 5 0 0 1 0 6");

    /// <summary>Lucide "volume-x".</summary>
    public static Geometry VolumeOff { get; } =
        ArcIcons.Parse("M11 4.702a.7.7 0 0 0-1.203-.498L6.413 7.587A1.4 1.4 0 0 1 5.416 8H3a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h2.416a1.4 1.4 0 0 1 .997.413l3.383 3.384A.7.7 0 0 0 11 19.298z M16.5 14.5l5-5 M16.5 9.5l5 5");

    /// <summary>Lucide "captions".</summary>
    public static Geometry Captions { get; } =
        ArcIcons.Parse("M5 5h14a2 2 0 0 1 2 2v10a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-10a2 2 0 0 1 2 -2z M7 15h4M15 15h2M7 11h2M13 11h4");

    /// <summary>Lucide "maximize".</summary>
    public static Geometry FullScreen { get; } =
        ArcIcons.Parse("M8 3H5a2 2 0 0 0-2 2v3 M21 8V5a2 2 0 0 0-2-2h-3 M3 16v3a2 2 0 0 0 2 2h3 M16 21h3a2 2 0 0 0 2-2v-3");

    /// <summary>Lucide "minimize".</summary>
    public static Geometry ExitFullScreen { get; } =
        ArcIcons.Parse("M8 3v3a2 2 0 0 1-2 2H3 M21 8h-3a2 2 0 0 1-2-2V3 M3 16h3a2 2 0 0 1 2 2v3 M16 21v-3a2 2 0 0 1 2-2h3");

    /// <summary>Lucide "rewind".</summary>
    public static Geometry Rewind { get; } =
        ArcIcons.Parse("M12 6a2 2 0 0 0-3.414-1.414l-6 6a2 2 0 0 0 0 2.828l6 6A2 2 0 0 0 12 18z M22 6a2 2 0 0 0-3.414-1.414l-6 6a2 2 0 0 0 0 2.828l6 6A2 2 0 0 0 22 18z");

    /// <summary>Lucide "fast-forward".</summary>
    public static Geometry FastForward { get; } =
        ArcIcons.Parse("M12 6a2 2 0 0 1 3.414-1.414l6 6a2 2 0 0 1 0 2.828l-6 6A2 2 0 0 1 12 18z M2 6a2 2 0 0 1 3.414-1.414l6 6a2 2 0 0 1 0 2.828l-6 6A2 2 0 0 1 2 18z");
}

/// <summary>
/// An <see cref="ArcGlyph"/> that can also fill its shape: the transport glyphs (play, pause) are solid, filled and stroked
/// in the same color like an SVG with <c>fill="currentColor"</c>; the others stay outlined. Copied from the Blocks
/// MediaPlayer's glyph (internal there, ADR 0036 decision 5).
/// </summary>
internal sealed class VideoGlyph : ArcGlyph
{
    /// <summary>Whether the glyph's closed shapes are filled with its foreground.</summary>
    public static readonly StyledProperty<bool> IsFilledProperty =
        AvaloniaProperty.Register<VideoGlyph, bool>(nameof(IsFilled));

    static VideoGlyph()
    {
        AffectsRender<VideoGlyph>(IsFilledProperty);
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
