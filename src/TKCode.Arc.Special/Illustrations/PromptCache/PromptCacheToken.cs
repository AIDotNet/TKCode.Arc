using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="PromptCache"/> (Arc Prompt cache, design reference) and its one accent-independent derived
/// color. Surface, borders, text, series and status colors, radii of the card and type come from the ArcDesign tokens in
/// the theme; the bars' cached tints are derived from the series colors in code.
/// </summary>
[ControlDesignToken]
internal sealed class PromptCacheToken : AbstractControlDesignToken
{
    /// <summary>The card's padding (24) and the space between its sections (20).</summary>
    public Thickness CardPadding { get; set; }

    public double SectionGap { get; set; }

    /// <summary>The cache panel: 192 wide beside the requests, 24 away from them; padding 16, radius 22.</summary>
    public double PanelWidth { get; set; }

    public double PanelGap { get; set; }

    public Thickness PanelPadding { get; set; }

    public CornerRadius PanelRadius { get; set; }

    /// <summary>The ring: 36 across, 3 thick, 12 from the panel's name.</summary>
    public double RingDiameter { get; set; }

    public double RingThickness { get; set; }

    /// <summary>Bars are 10 high.</summary>
    public double BarHeight { get; set; }

    /// <summary>The header's title: 16/17.6 (the display face); the model line 14/19.6.</summary>
    public double TitleLineHeight { get; set; }

    public double BodyLineHeight { get; set; }

    /// <summary>Small text (legend, column heads, details): 12/16.8.</summary>
    public double SmallLineHeight { get; set; }

    /// <summary>Statistic values: 22/26.4.</summary>
    public double StatLineHeight { get; set; }

    /// <summary>The two statistics are 192 wide with 24 between them.</summary>
    public double StatWidth { get; set; }

    /// <summary>The change toggle: 36 high, 12 to the sides, 8 between icon and label, icon 15.</summary>
    public Thickness TogglePadding { get; set; }

    public double ToggleIconSize { get; set; }

    /// <summary>The fill of the new message at the end of each prompt: the foreground mixed 76% into the surface.</summary>
    public Color MessageFill { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardPadding = new Thickness(24);
        SectionGap = 20;
        PanelWidth = 192;
        PanelGap = 24;
        PanelPadding = new Thickness(16);
        PanelRadius = new CornerRadius(22);
        RingDiameter = 36;
        RingThickness = 3;
        BarHeight = 10;
        TitleLineHeight = 17.6;
        BodyLineHeight = 19.6;
        SmallLineHeight = 16.8;
        StatLineHeight = 26.4;
        StatWidth = 192;
        TogglePadding = new Thickness(12, 0);
        ToggleIconSize = 15;
        MessageFill = Oklch.MixOklab(n.Surface, n.Foreground, 0.76);
    }
}
