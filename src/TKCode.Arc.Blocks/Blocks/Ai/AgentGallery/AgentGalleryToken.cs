using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and timing of <see cref="AgentGallery"/> (an original TKCode design), and the accent-independent tints it
/// derives from the neutrals. Colors that follow the accent, radii and fonts come from ArcDesign tokens in the theme.
/// Paddings are Avalonia thicknesses (left, top, right, bottom); the compact ones apply below <see cref="CompactWidth"/>.
/// </summary>
[ControlDesignToken]
internal sealed class AgentGalleryToken : AbstractControlDesignToken
{
    /// <summary>The gallery is at most this wide (1120) and centered.</summary>
    public double FrameMaxWidth { get; set; }

    /// <summary>Below this width of the gallery the header stacks and the paddings tighten (600).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Padding of the header (24 24 24 16).</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>Padding of the header in a narrow gallery (16 16 16 12).</summary>
    public Thickness CompactHeaderPadding { get; set; }

    /// <summary>Width of the search field beside the title (280).</summary>
    public double SearchWidth { get; set; }

    /// <summary>Space around the filter pills, the count row and the cards (20 at the sides).</summary>
    public Thickness SectionPadding { get; set; }

    /// <summary>The same in a narrow gallery (12 at the sides).</summary>
    public Thickness CompactSectionPadding { get; set; }

    /// <summary>Space under the cards (24).</summary>
    public Thickness GridPadding { get; set; }

    /// <summary>Space under the cards in a narrow gallery (12 at the sides, 16 below).</summary>
    public Thickness CompactGridPadding { get; set; }

    /// <summary>A column is at least this wide; the grid fits as many as it can (248).</summary>
    public double CardMinWidth { get; set; }

    /// <summary>Space between cards, across and down (12).</summary>
    public double CardGap { get; set; }

    /// <summary>The most columns the grid uses (4).</summary>
    public int MaxColumns { get; set; }

    /// <summary>Padding inside a card (16).</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>Side of a card's mark (40).</summary>
    public double MarkSize { get; set; }

    /// <summary>Side of the mark in the detail view (56).</summary>
    public double DetailMarkSize { get; set; }

    /// <summary>The detail view's text column is at most this wide (720).</summary>
    public double DetailMaxWidth { get; set; }

    /// <summary>Padding of the detail view (20 8 24 28).</summary>
    public Thickness DetailPadding { get; set; }

    /// <summary>Padding of the detail view in a narrow gallery (12 8 16 20).</summary>
    public Thickness CompactDetailPadding { get; set; }

    /// <summary>The keyboard focus ring sits this far outside a card (-3 on every side).</summary>
    public Thickness FocusRingOutset { get; set; }

    /// <summary>Share of the tint in a mark's background (0.14).</summary>
    public double MarkTintAlpha { get; set; }

    /// <summary>The tint behind a hovered card: the muted surface at 60%.</summary>
    public Color CardHoverBackground { get; set; }

    /// <summary>The background of a capability tag: the muted surface.</summary>
    public Color TagBackground { get; set; }

    /// <summary>After a filter, search or sort the cards fade in from this opacity (0.25).</summary>
    public double ResultsFadeFrom { get; set; }

    /// <summary>The detail view rises this far as it comes in (8).</summary>
    public double DetailRise { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        FrameMaxWidth = 1120;
        CompactWidth = 600;
        HeaderPadding = new Thickness(24, 24, 24, 16);
        CompactHeaderPadding = new Thickness(16, 16, 16, 12);
        SearchWidth = 280;
        SectionPadding = new Thickness(20, 0, 20, 0);
        CompactSectionPadding = new Thickness(12, 0, 12, 0);
        GridPadding = new Thickness(20, 12, 20, 24);
        CompactGridPadding = new Thickness(12, 12, 12, 16);
        CardMinWidth = 248;
        CardGap = 12;
        MaxColumns = 4;
        CardPadding = new Thickness(16);
        MarkSize = 40;
        DetailMarkSize = 56;
        DetailMaxWidth = 720;
        DetailPadding = new Thickness(20, 4, 24, 28);
        CompactDetailPadding = new Thickness(12, 4, 16, 20);
        FocusRingOutset = new Thickness(-3);
        MarkTintAlpha = 0.14;
        CardHoverBackground = Oklch.WithAlpha(n.SurfaceMuted, 0.6);
        TagBackground = n.SurfaceMuted;
        ResultsFadeFrom = 0.25;
        DetailRise = 8;
    }
}
