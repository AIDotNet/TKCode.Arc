using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry and accent-independent colors of <see cref="Card"/> and its quick look (values from Arc's card.module.css).</summary>
[ControlDesignToken]
internal sealed class CardToken : AbstractControlDesignToken
{
    /// <summary>Card content padding (space-5).</summary>
    public Thickness ContentPadding { get; set; }

    /// <summary>Quick look content padding (space-6).</summary>
    public Thickness QuickLookContentPadding { get; set; }

    /// <summary>Minimum media height (Arc's .zoom min-height).</summary>
    public double MediaMinHeight { get; set; }

    /// <summary>Title line height: 1.3 × text-lg in the card.</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>Title line height: 1.3 × text-xl in the quick look.</summary>
    public double QuickLookTitleLineHeight { get; set; }

    /// <summary>Description and details line height: leading-body × text-sm.</summary>
    public double BodyLineHeight { get; set; }

    /// <summary>Byline line height: leading-body × text-xs.</summary>
    public double BylineLineHeight { get; set; }

    public Thickness DescriptionMargin { get; set; }

    /// <summary>Gap above the footer (space-5).</summary>
    public Thickness FooterMargin { get; set; }

    /// <summary>Gap between avatar, byline and action (space-3).</summary>
    public double FooterGap { get; set; }

    /// <summary>The footer's leading avatar slot (space-8).</summary>
    public double AvatarSlotSize { get; set; }

    /// <summary>Quick look width cap (30rem); the viewport keeps <see cref="QuickLookMargin"/> on each side.</summary>
    public double QuickLookMaxWidth { get; set; }

    /// <summary>Viewport gutter around the quick look (min(100vw − space-8, 30rem), max-height 100dvh − space-8).</summary>
    public Thickness QuickLookMargin { get; set; }

    /// <summary>Details block: margin-top space-6, padding-top space-5 under a 1px rule.</summary>
    public Thickness DetailsMargin { get; set; }

    public Thickness DetailsPadding { get; set; }

    /// <summary>The close slot sits space-4 from the top-right corner.</summary>
    public Thickness CloseSlotMargin { get; set; }

    public double CloseButtonSize { get; set; }

    /// <summary>Close button rest fill: color-mix(surface-raised 74%, transparent).</summary>
    public Color CloseButtonBackground { get; set; }

    /// <summary>Close button border: color-mix(border 70%, transparent).</summary>
    public Color CloseButtonBorder { get; set; }

    /// <summary>Quick look overlay: oklch(10% 0 0 / 0.46) in both appearances.</summary>
    public Color ScrimColor { get; set; }

    /// <summary>Keyboard focus ring: 2px, 2px outside the card's 26px corners.</summary>
    public Thickness FocusRingMargin { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ContentPadding = new Thickness(20);
        QuickLookContentPadding = new Thickness(24);
        MediaMinHeight = 112;
        TitleLineHeight = 18 * 1.3;
        QuickLookTitleLineHeight = 22 * 1.3;
        BodyLineHeight = 14 * 1.4;
        BylineLineHeight = 12 * 1.4;
        DescriptionMargin = new Thickness(0, 8, 0, 0);
        FooterMargin = new Thickness(0, 20, 0, 0);
        FooterGap = 12;
        AvatarSlotSize = 32;
        QuickLookMaxWidth = 480;
        QuickLookMargin = new Thickness(16);
        DetailsMargin = new Thickness(0, 24, 0, 0);
        DetailsPadding = new Thickness(0, 20, 0, 0);
        CloseSlotMargin = new Thickness(0, 16, 16, 0);
        CloseButtonSize = 32;
        CloseButtonBackground = Oklch.WithAlpha(n.SurfaceRaised, 0.74);
        CloseButtonBorder = Oklch.WithAlpha(n.Border, 0.7);
        ScrimColor = Oklch.ToColor(0.10, 0, 0, 0.46);
        FocusRingMargin = new Thickness(-4);
        FocusRingRadius = new CornerRadius(30);
    }
}
