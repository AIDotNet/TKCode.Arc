using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="LinkUnfurl"/> (Arc Link unfurl, design reference), as measured: a composer at most 576 wide
/// with 18 / 22 / 14 editor padding (16 / 18 / 12 under 560), Inter 16/26 text, 26px links at most 224 wide, cards at
/// most 440 wide with 8px padding, a 30px header and a 1.91 : 1 image, 10px between cards, and a 58px toolbar. Colors
/// come from the ArcDesign tokens in the theme; the lift shadow and the loading sweep are accent-independent mixes of the
/// neutrals, and the unfurled link's accent tint is a fraction of the accent (11% / 34% light, 20% / 48% dark).
/// </summary>
[ControlDesignToken]
internal sealed class LinkUnfurlToken : AbstractControlDesignToken
{
    /// <summary>The composer's widest size (576) and its frame: radius 26, 1px border.</summary>
    public double ComposerMaxWidth { get; set; }

    public CornerRadius ComposerCornerRadius { get; set; }

    /// <summary>The editor's padding: 18 22 14 (16 18 12 when the composer is under <see cref="CompactBreakpoint"/>).</summary>
    public Thickness EditorPadding { get; set; }

    public Thickness EditorPaddingCompact { get; set; }

    /// <summary>The text's line height (26) and letter spacing (−0.16px at 16px).</summary>
    public double EditorLineHeight { get; set; }

    public double EditorLetterSpacing { get; set; }

    /// <summary>The lane's side padding: 16 (12 when compact).</summary>
    public Thickness LanePadding { get; set; }

    public Thickness LanePaddingCompact { get; set; }

    /// <summary>The toolbar's padding: 10 12 12 18 (8 10 10 14 when compact).</summary>
    public Thickness ToolbarPadding { get; set; }

    public Thickness ToolbarPaddingCompact { get; set; }

    /// <summary>The composer width under which the padding steps down (560) and under which "Try" hides (360).</summary>
    public double CompactBreakpoint { get; set; }

    public double TryHideBreakpoint { get; set; }

    /// <summary>A card's widest size (440), its padding (8) and the space under it (10).</summary>
    public double CardMaxWidth { get; set; }

    public double CardGap { get; set; }

    /// <summary>The settled card's corner radius (18) and the media's (10).</summary>
    public CornerRadius CardCornerRadius { get; set; }

    public CornerRadius MediaCornerRadius { get; set; }

    /// <summary>The shadow under a card's plate, shown only while it travels (its opacity is sin(πp)).</summary>
    public BoxShadows LiftShadow { get; set; }

    /// <summary>The loading sweep's peak color: the text color at 9%.</summary>
    public Color SweepColor { get; set; }

    /// <summary>How much accent an unfurled link takes: fill and inner ring (11% / 34% light, 20% / 48% dark).</summary>
    public double LinkAccentFill { get; set; }

    public double LinkAccentRing { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ComposerMaxWidth = 576;
        ComposerCornerRadius = new CornerRadius(26);
        EditorPadding = new Thickness(22, 18, 22, 14);
        EditorPaddingCompact = new Thickness(18, 16, 18, 12);
        EditorLineHeight = 26;
        EditorLetterSpacing = -0.16;
        LanePadding = new Thickness(16, 0);
        LanePaddingCompact = new Thickness(12, 0);
        ToolbarPadding = new Thickness(18, 10, 12, 12);
        ToolbarPaddingCompact = new Thickness(14, 8, 10, 10);
        CompactBreakpoint = 560;
        TryHideBreakpoint = 360;
        CardMaxWidth = 440;
        CardGap = 10;
        CardCornerRadius = new CornerRadius(18);
        MediaCornerRadius = new CornerRadius(10);
        var n = ArcPalette.Neutrals(isDarkMode);
        LiftShadow = isDarkMode
            ? new BoxShadows(new BoxShadow { OffsetY = 18, Blur = 40, Spread = -16, Color = Oklch.WithAlpha(n.Background, 0.9) },
                [new BoxShadow { OffsetY = 4, Blur = 12, Spread = -6, Color = Oklch.WithAlpha(n.Background, 0.7) }])
            : new BoxShadows(new BoxShadow { OffsetY = 18, Blur = 40, Spread = -18, Color = Oklch.WithAlpha(n.Foreground, 0.32) },
                [new BoxShadow { OffsetY = 4, Blur = 12, Spread = -6, Color = Oklch.WithAlpha(n.Foreground, 0.18) }]);
        SweepColor = Oklch.WithAlpha(n.Foreground, 0.09);
        LinkAccentFill = isDarkMode ? 0.20 : 0.11;
        LinkAccentRing = isDarkMode ? 0.48 : 0.34;
    }
}
