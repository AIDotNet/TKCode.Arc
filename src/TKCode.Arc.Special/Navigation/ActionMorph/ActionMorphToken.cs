using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ActionMorph"/>. Its colors, radii and shadows come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ActionMorphToken : AbstractControlDesignToken
{
    /// <summary>Height of the button face.</summary>
    public double FaceHeight { get; set; }

    /// <summary>Space around the face's glyph and label.</summary>
    public Thickness FacePadding { get; set; }

    /// <summary>Gap between the face's glyph and its label.</summary>
    public double FaceSpacing { get; set; }

    /// <summary>Size of the face's glyph.</summary>
    public double GlyphSize { get; set; }

    /// <summary>Space around the open panel's content.</summary>
    public Thickness PanelPadding { get; set; }

    /// <summary>The open panel is at least this wide.</summary>
    public double PanelMinWidth { get; set; }

    /// <summary>Gap between the panel's content and its close button.</summary>
    public double PanelSpacing { get; set; }

    /// <summary>Margin of the face's keyboard focus ring (negative: outside the surface).</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>Tint laid over the face while the pointer is on it; it reads on the plain and the accent face alike.</summary>
    public Color HoverTint { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        HoverTint = Oklch.WithAlpha(ArcPalette.Neutrals(isDarkMode).Foreground, 0.07);
        FaceHeight = 36;
        FacePadding = new Thickness(14, 0, 16, 0);
        FaceSpacing = 8;
        GlyphSize = 16;
        PanelPadding = new Thickness(12, 10, 8, 10);
        PanelMinWidth = 240;
        PanelSpacing = 8;
        FocusRingMargin = new Thickness(-3);
    }
}
