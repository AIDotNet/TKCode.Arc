using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="AvatarUpload"/>: the framing area, the progress ring around the circle and the spacing of the
/// text and buttons. Colors, radii and fonts come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class AvatarUploadToken : AbstractControlDesignToken
{
    /// <summary>The square framing area (256px) and the space between its edges and the circle (20px).</summary>
    public double CropSize { get; set; }

    public double CropInset { get; set; }

    /// <summary>The space between the framing area, the zoom row and the buttons.</summary>
    public double EditorSpacing { get; set; }

    /// <summary>The zoom buttons' glyph (16px).</summary>
    public double ZoomGlyphSize { get; set; }

    /// <summary>The space between the circle and the text beside it.</summary>
    public Thickness TextMargin { get; set; }

    /// <summary>The space between the title and the status line.</summary>
    public double TextSpacing { get; set; }

    /// <summary>The space above the buttons and between them.</summary>
    public Thickness ActionsMargin { get; set; }

    public double ActionsSpacing { get; set; }

    /// <summary>How far the progress ring sits outside the circle (5px) and its stroke (3px).</summary>
    public Thickness RingMargin { get; set; }

    public double RingThickness { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CropSize = 256;
        CropInset = 20;
        EditorSpacing = 12;
        ZoomGlyphSize = 16;
        TextMargin = new Thickness(20, 0, 0, 0);
        TextSpacing = 2;
        ActionsMargin = new Thickness(0, 10, 0, 0);
        ActionsSpacing = 8;
        RingMargin = new Thickness(-5);
        RingThickness = 3;
    }
}
