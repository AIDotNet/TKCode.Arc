using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="MegaMenuMorph"/>: the top-level triggers, the gap between the bar and the panel, the panel's
/// padding and width limit, how far content slides when the panel morphs to a neighbour, and how close the panel may come
/// to the edges of the viewport. Colors, radii, the shadow and the fonts come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class MegaMenuMorphToken : AbstractControlDesignToken
{
    /// <summary>Height of a top-level trigger.</summary>
    public double TriggerHeight { get; set; }

    /// <summary>Inner padding of a top-level trigger.</summary>
    public Thickness TriggerPadding { get; set; }

    /// <summary>Space between neighbouring triggers.</summary>
    public double TriggerGap { get; set; }

    /// <summary>Space between the label and the chevron of a trigger that opens a panel.</summary>
    public double ChevronGap { get; set; }

    /// <summary>Size of the chevron glyph.</summary>
    public double ChevronSize { get; set; }

    /// <summary>The ring sits this far outside a trigger while it has keyboard focus.</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>Gap between the bar and the panel under it.</summary>
    public double PanelGap { get; set; }

    /// <summary>Space between the panel's edge and its content.</summary>
    public Thickness PanelPadding { get; set; }

    /// <summary>The panel is never wider than this, whatever its content asks for.</summary>
    public double PanelMaxWidth { get; set; }

    /// <summary>How far content travels sideways while the panel morphs to the next item.</summary>
    public double SlideDistance { get; set; }

    /// <summary>The panel keeps at least this much room to the left and right edges of the viewport.</summary>
    public double ViewportMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TriggerHeight = 36;
        TriggerPadding = new Thickness(14, 0);
        TriggerGap = 2;
        ChevronGap = 6;
        ChevronSize = 14;
        FocusRingMargin = new Thickness(-3);
        PanelGap = 10;
        PanelPadding = new Thickness(12);
        PanelMaxWidth = 760;
        SlideDistance = 36;
        ViewportMargin = 12;
    }
}

/// <summary>
/// Geometry of <see cref="MegaMenuMorphLink"/>: its padding, the square that holds the icon, the icon itself, the gap to
/// the text and the space between title and description. Colors, radii and fonts come from the ArcDesign tokens.
/// </summary>
[ControlDesignToken]
internal sealed class MegaMenuMorphLinkToken : AbstractControlDesignToken
{
    /// <summary>Space between the link's edge and its icon and text.</summary>
    public Thickness LinkPadding { get; set; }

    /// <summary>Side of the square tile behind the icon.</summary>
    public double IconTileSize { get; set; }

    /// <summary>Size of the icon glyph.</summary>
    public double IconSize { get; set; }

    /// <summary>Space between the icon tile and the text (the tile's outer margin).</summary>
    public Thickness IconMargin { get; set; }

    /// <summary>Space between the title and the description.</summary>
    public double TextGap { get; set; }

    /// <summary>The ring sits this far outside the link while it has keyboard focus.</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LinkPadding = new Thickness(10);
        IconTileSize = 36;
        IconSize = 18;
        IconMargin = new Thickness(0, 0, 12, 0);
        TextGap = 2;
        FocusRingMargin = new Thickness(-2);
    }
}
