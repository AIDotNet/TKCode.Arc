using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="UndoToast"/>: the width limits, the padding, the countdown ring and the buttons. Colors, radii,
/// shadows and fonts come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class UndoToastToken : AbstractControlDesignToken
{
    /// <summary>The widest the toast grows (416px) and the narrowest it gets (280px, or the space it has).</summary>
    public double MaxToastWidth { get; set; }

    public double MinToastWidth { get; set; }

    /// <summary>The padding around the ring, the message and the buttons.</summary>
    public Thickness SurfacePadding { get; set; }

    /// <summary>The countdown ring (28px) and its stroke (2.5px).</summary>
    public double RingSize { get; set; }

    public double RingThickness { get; set; }

    /// <summary>The seconds in the ring (11px).</summary>
    public double SecondsFontSize { get; set; }

    /// <summary>The space around the message.</summary>
    public Thickness MessageMargin { get; set; }

    /// <summary>The message's line height.</summary>
    public double MessageLineHeight { get; set; }

    /// <summary>The space between the undo and dismiss buttons.</summary>
    public double ActionsSpacing { get; set; }

    /// <summary>The undo button's glyph (14px).</summary>
    public double UndoGlyphSize { get; set; }

    /// <summary>The round dismiss button (28px) and its glyph (16px).</summary>
    public double CloseSize { get; set; }

    public double CloseGlyphSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MaxToastWidth = 416;
        MinToastWidth = 280;
        SurfacePadding = new Thickness(10, 8, 8, 8);
        RingSize = 28;
        RingThickness = 2.5;
        SecondsFontSize = 11;
        MessageMargin = new Thickness(12, 0, 12, 0);
        MessageLineHeight = 20;
        ActionsSpacing = 4;
        UndoGlyphSize = 14;
        CloseSize = 28;
        CloseGlyphSize = 16;
    }
}
