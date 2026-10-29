using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class SplitButtonToken : AbstractControlDesignToken
{
    /// <summary>Inline padding of the main half: space-4 (primary) / space-3 (secondary).</summary>
    public Thickness PrimaryInset { get; set; }
    public Thickness SecondaryInset { get; set; }

    /// <summary>Width of the chevron half: 38px (primary) / 32px (secondary).</summary>
    public double TriggerExtentPrimary { get; set; }
    public double TriggerExtentSecondary { get; set; }

    /// <summary>Minimum width of the secondary main half (Arc: 142px).</summary>
    public double SecondaryActionMinWidth { get; set; }

    /// <summary>Gap between the main icon and the label (space-2).</summary>
    public double LabelGap { get; set; }

    /// <summary>Main icon and chevron: 16px glyphs with a 1.75 stroke.</summary>
    public double GlyphExtent { get; set; }
    public double GlyphStroke { get; set; }

    /// <summary>Clip overflow while the label slot springs (Arc: clip-path inset -space-2).</summary>
    public double MorphOverflow { get; set; }

    /// <summary>Half radii inside the 1px group border: radius-control − 1 on the outer corners.</summary>
    public CornerRadius StartRadius { get; set; }
    public CornerRadius EndRadius { get; set; }
    public CornerRadius PillStartRadius { get; set; }
    public CornerRadius PillEndRadius { get; set; }

    /// <summary>Focus rings (2px, offset 2px) follow the shape of each half.</summary>
    public CornerRadius StartRingRadius { get; set; }
    public CornerRadius EndRingRadius { get; set; }
    public CornerRadius PillStartRingRadius { get; set; }
    public CornerRadius PillEndRingRadius { get; set; }
    public Thickness FocusRingMargin { get; set; }

    public double DisabledOpacity { get; set; }

    /// <summary>Primary hover: foreground 88% into background.</summary>
    public Color PrimaryHoverColor { get; set; }

    /// <summary>Primary pressed and open: foreground 80% into background.</summary>
    public Color PrimaryPressedColor { get; set; }

    /// <summary>Primary divider: background 18% into foreground.</summary>
    public Color PrimaryDividerColor { get; set; }

    /// <summary>Secondary pressed and open: surface-muted mixed with 55% border.</summary>
    public Color SecondaryPressedColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        PrimaryInset = new Thickness(16, 0);
        SecondaryInset = new Thickness(12, 0);
        TriggerExtentPrimary = 38;
        TriggerExtentSecondary = 32;
        SecondaryActionMinWidth = 142;
        LabelGap = 8;
        GlyphExtent = 16;
        GlyphStroke = 1.75;
        MorphOverflow = 8;
        StartRadius = new CornerRadius(17, 0, 0, 17);
        EndRadius = new CornerRadius(0, 17, 17, 0);
        PillStartRadius = new CornerRadius(9999, 0, 0, 9999);
        PillEndRadius = new CornerRadius(0, 9999, 9999, 0);
        StartRingRadius = new CornerRadius(21, 4, 4, 21);
        EndRingRadius = new CornerRadius(4, 21, 21, 4);
        PillStartRingRadius = new CornerRadius(9999, 4, 4, 9999);
        PillEndRingRadius = new CornerRadius(4, 9999, 9999, 4);
        FocusRingMargin = new Thickness(-4);
        DisabledOpacity = 0.52;
        PrimaryHoverColor = Oklch.MixOklab(n.Background, n.Foreground, 0.88);
        PrimaryPressedColor = Oklch.MixOklab(n.Background, n.Foreground, 0.80);
        PrimaryDividerColor = Oklch.MixOklab(n.Foreground, n.Background, 0.18);
        SecondaryPressedColor = Oklch.MixOklab(n.SurfaceMuted, n.Border, 0.55);
    }
}
