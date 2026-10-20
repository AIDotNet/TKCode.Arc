using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class BillingToggleToken : AbstractControlDesignToken
{
    public Thickness TrackInset { get; set; }
    public Thickness TrackInsetLG { get; set; }
    public double OptionHeight { get; set; }
    public double OptionHeightLG { get; set; }
    public Thickness OptionInset { get; set; }
    public Thickness OptionInsetBadge { get; set; }
    public Thickness OptionInsetLG { get; set; }
    public Thickness OptionInsetBadgeLG { get; set; }
    public double LabelGap { get; set; }
    public double BadgeHeight { get; set; }
    public double BadgeHeightLG { get; set; }
    public Thickness BadgeInset { get; set; }

    /// <summary>The quiet badge: <c>color-mix(in oklab, var(--foreground) 6%, transparent)</c>.</summary>
    public Color BadgeBackground { get; set; }

    /// <summary>Opacity of the badge accent behind a selected badge (13% light, 18% dark).</summary>
    public double BadgeTintOpacity { get; set; }

    /// <summary>How much foreground mixes into the selected badge text (12% light; dark uses the accent itself).</summary>
    public double BadgeInkMix { get; set; }

    /// <summary>The raised thumb's ring and drop shadows (light and dark differ in Arc).</summary>
    public BoxShadows ThumbShadow { get; set; }
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        TrackInset = new Thickness(3);
        TrackInsetLG = new Thickness(4);
        OptionHeight = 34;
        OptionHeightLG = 42;
        OptionInset = new Thickness(14, 0);
        OptionInsetBadge = new Thickness(14, 0, 5, 0);
        OptionInsetLG = new Thickness(18, 0);
        OptionInsetBadgeLG = new Thickness(18, 0, 6, 0);
        LabelGap = 8;
        BadgeHeight = 24;
        BadgeHeightLG = 32;
        BadgeInset = new Thickness(8, 0);
        BadgeBackground = Oklch.WithAlpha(n.Foreground, 0.06);
        BadgeTintOpacity = isDarkMode ? 0.18 : 0.13;
        BadgeInkMix = isDarkMode ? 0 : 0.12;
        ThumbShadow = isDarkMode
            ? new BoxShadows(new BoxShadow { Spread = 1, Color = n.Border },
                [
                    new BoxShadow { OffsetY = 1, Color = Color.FromArgb(13, 255, 255, 255), IsInset = true },
                    new BoxShadow { OffsetY = 1, Blur = 2, Color = Color.FromArgb(77, 0, 0, 0) }
                ])
            : new BoxShadows(new BoxShadow { Spread = 1, Color = n.BorderSubtle },
                [
                    new BoxShadow { OffsetY = 1, Blur = 2, Color = Color.FromArgb(15, 0, 0, 0) },
                    new BoxShadow { OffsetY = 2, Blur = 6, Color = Color.FromArgb(10, 0, 0, 0) }
                ]);
        FocusRingMargin = new Thickness(-4);
    }
}
