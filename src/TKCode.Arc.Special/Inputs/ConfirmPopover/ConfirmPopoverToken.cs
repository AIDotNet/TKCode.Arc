using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ConfirmPopover"/>: the panel width, the mark, the spacing of the text and the buttons. Colors,
/// radii and fonts come from the ArcDesign tokens in the theme; the surface itself is the popover's.
/// </summary>
[ControlDesignToken]
internal sealed class ConfirmPopoverToken : AbstractControlDesignToken
{
    /// <summary>The panel's width inside the popover surface (272px), so questions wrap the same way everywhere.</summary>
    public double PanelWidth { get; set; }

    /// <summary>The round mark beside the question (32px) and its glyph (16px).</summary>
    public double MarkSize { get; set; }

    public double MarkGlyphSize { get; set; }

    /// <summary>The space between the mark and the text.</summary>
    public Thickness TextMargin { get; set; }

    /// <summary>The space between the question and the line under it.</summary>
    public double TextSpacing { get; set; }

    /// <summary>The line heights of the question and the line under it.</summary>
    public double TitleLineHeight { get; set; }

    public double MessageLineHeight { get; set; }

    /// <summary>The space above the buttons and between them.</summary>
    public Thickness ActionsMargin { get; set; }

    public double ActionsSpacing { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PanelWidth = 272;
        MarkSize = 32;
        MarkGlyphSize = 16;
        TextMargin = new Thickness(12, 0, 0, 0);
        TextSpacing = 2;
        TitleLineHeight = 20;
        MessageLineHeight = 20;
        ActionsMargin = new Thickness(0, 16, 0, 0);
        ActionsSpacing = 8;
    }
}
