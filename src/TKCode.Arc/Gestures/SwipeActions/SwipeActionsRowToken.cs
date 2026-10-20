using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class SwipeActionsRowToken : AbstractControlDesignToken
{
    /// <summary>Width of one revealed action while the row rests open (Arc: 76px).</summary>
    public double ActionExtent { get; set; }

    /// <summary>Revealed action glyph: 20px icon, 3px gap, 12px label.</summary>
    public double ActionGlyphExtent { get; set; }
    public double ActionGlyphGap { get; set; }
    public double GlyphStroke { get; set; }

    /// <summary>Row body: at least 64px tall, padding space-3 / space-2 / space-3 / space-4, space-2 gap.</summary>
    public double MinRowHeight { get; set; }
    public Thickness BodyInset { get; set; }
    public double BodyGap { get; set; }

    /// <summary>More actions button: 32px square, radius-control − 6, an 18px ellipsis.</summary>
    public double MoreExtent { get; set; }
    public CornerRadius MoreRadius { get; set; }
    public double MoreGlyphExtent { get; set; }
    public CornerRadius MoreRingRadius { get; set; }
    public Thickness MoreRingMargin { get; set; }

    /// <summary>Neutral action fill: foreground 20% into the surface.</summary>
    public Color NeutralActionColor { get; set; }

    /// <summary>Menu minimum width (11rem) and gap below the More button.</summary>
    public double MenuMinWidth { get; set; }
    public double MenuGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ActionExtent = 76;
        ActionGlyphExtent = 20;
        ActionGlyphGap = 3;
        GlyphStroke = 1.75;
        MinRowHeight = 64;
        BodyInset = new Thickness(16, 12, 8, 12);
        BodyGap = 8;
        MoreExtent = 32;
        MoreRadius = new CornerRadius(12);
        MoreGlyphExtent = 18;
        MoreRingRadius = new CornerRadius(16);
        MoreRingMargin = new Thickness(-4);
        NeutralActionColor = Oklch.MixOklab(n.Surface, n.Foreground, 0.20);
        MenuMinWidth = 176;
        MenuGap = 6;
    }
}
