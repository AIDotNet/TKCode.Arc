using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class TabItemToken : AbstractControlDesignToken
{
    /// <summary>Arc: min-width 5.5rem.</summary>
    public double TriggerMinWidth { get; set; }

    /// <summary>Arc: padding 0 space-3.</summary>
    public Thickness InlinePadding { get; set; }

    /// <summary>Arc: radius-control − 3px.</summary>
    public CornerRadius TriggerRadius { get; set; }

    public Thickness FocusRingMargin { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    public double DisabledOpacity { get; set; }

    /// <summary>Padding of a closable tab: the close button brings its own room on the end side.</summary>
    public Thickness ClosableInlinePadding { get; set; }

    /// <summary>Space between the header and the close button.</summary>
    public double CloseGap { get; set; }

    /// <summary>Side of the square close button.</summary>
    public double CloseButtonSize { get; set; }

    /// <summary>Size of the close button's X glyph.</summary>
    public double CloseGlyphSize { get; set; }

    public CornerRadius CloseButtonRadius { get; set; }

    /// <summary>Fill of the close button under the pointer (reads on the selected card, the pill and a hovered card).</summary>
    public Color CloseHoverBackground { get; set; }

    /// <summary>Fill of the close button while pressed.</summary>
    public Color ClosePressedBackground { get; set; }

    /// <summary>Card variant: corners of a tab (rounded on top only).</summary>
    public CornerRadius CardRadius { get; set; }

    /// <summary>Card variant: margin of a tab; the bottom edge keeps a hovered card off the strip's baseline.</summary>
    public Thickness CardMargin { get; set; }

    /// <summary>Card variant: fill of a tab that is not selected, under the pointer.</summary>
    public Color CardHoverBackground { get; set; }

    /// <summary>Card variant: focus ring margin; flush with the bottom edge, which the strip clips.</summary>
    public Thickness CardFocusRingMargin { get; set; }

    /// <summary>Card variant: focus ring corners (the card's radius plus the ring offset on top).</summary>
    public CornerRadius CardFocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TriggerMinWidth = 88;
        InlinePadding = new Thickness(12, 0);
        TriggerRadius = new CornerRadius(15);
        FocusRingMargin = new Thickness(-4);
        FocusRingRadius = new CornerRadius(19);
        DisabledOpacity = 0.5;
        var n = ArcPalette.Neutrals(isDarkMode);
        ClosableInlinePadding = new Thickness(12, 0, 6, 0);
        CloseGap = 6;
        CloseButtonSize = 18;
        CloseGlyphSize = 12;
        CloseButtonRadius = new CornerRadius(6);
        CloseHoverBackground = Oklch.MixOklab(n.SurfaceMuted, n.Foreground, 0.09);
        ClosePressedBackground = Oklch.MixOklab(n.SurfaceMuted, n.Foreground, 0.14);
        CardRadius = new CornerRadius(10, 10, 0, 0);
        CardMargin = new Thickness(0, 0, 0, 1);
        CardHoverBackground = Oklch.MixOklab(n.SurfaceMuted, n.Foreground, 0.04);
        CardFocusRingMargin = new Thickness(-3, -3, -3, 0);
        CardFocusRingRadius = new CornerRadius(13, 13, 0, 0);
    }
}
