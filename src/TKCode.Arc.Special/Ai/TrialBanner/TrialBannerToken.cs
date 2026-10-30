using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="TrialBanner"/>: the banner row (its padding, the container query and the gaps), the rings and
/// what sits in them, the copy's type metrics, the actions, the card and its feature rows, and the pill. Colors, radii,
/// fonts and the focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class TrialBannerToken : AbstractControlDesignToken
{
    /// <summary>The banner's padding and its smallest height (one line of actions).</summary>
    public Thickness BannerPadding { get; set; }

    public double BannerMinHeight { get; set; }

    /// <summary>The banner row's width under which the actions move under the copy.</summary>
    public double CompactBelow { get; set; }

    /// <summary>The space between the ring, the copy, the actions and the tools, and between the stacked copy and actions.</summary>
    public double BannerGap { get; set; }

    public double BannerRowGap { get; set; }

    /// <summary>The banner's ring: its size and stroke, the days in it and the lock or check that replaces them.</summary>
    public double BannerRingSize { get; set; }

    public double BannerRingThickness { get; set; }

    public double BannerDaysFontSize { get; set; }

    public double BannerGlyphSize { get; set; }

    /// <summary>The first line: 14/20.</summary>
    public double TitleFontSize { get; set; }

    public double TitleLineHeight { get; set; }

    /// <summary>The second line: 13/18, one line wide and two lines (reserved) when compact.</summary>
    public double DetailFontSize { get; set; }

    public double DetailLineHeight { get; set; }

    public double DetailCompactHeight { get; set; }

    /// <summary>Compare plans and Add payment method: 30px high, 12px in from the sides, 8px apart.</summary>
    public double ActionHeight { get; set; }

    public Thickness ActionPadding { get; set; }

    public double ActionGap { get; set; }

    public double ActionFontSize { get; set; }

    /// <summary>Snooze and Dismiss: 28px square ghost buttons with 16px glyphs.</summary>
    public double ToolSize { get; set; }

    public double ToolGlyphSize { get; set; }

    /// <summary>The card: its padding, widest size, the space between its rows and its ring.</summary>
    public Thickness CardPadding { get; set; }

    public double CardMaxWidth { get; set; }

    public double CardGap { get; set; }

    public double CardRingSize { get; set; }

    public double CardRingThickness { get; set; }

    public double CardDaysFontSize { get; set; }

    public double CardGlyphSize { get; set; }

    public Thickness CardHeadTextMargin { get; set; }

    /// <summary>The card's feature rows: 13/18 with a 14px mark, 8px apart.</summary>
    public double FeatureFontSize { get; set; }

    public double FeatureLineHeight { get; set; }

    public double FeatureIconSize { get; set; }

    public double FeatureGap { get; set; }

    public double FeatureIconGap { get; set; }

    /// <summary>The card's note line (simulated notes and checkout errors): 12/16, its height reserved.</summary>
    public double NoteFontSize { get; set; }

    public double NoteLineHeight { get; set; }

    /// <summary>The pill: its height, padding, ring, the glyph in it, the gap to the label and the label's size.</summary>
    public double PillHeight { get; set; }

    public Thickness PillPadding { get; set; }

    public double PillRingSize { get; set; }

    public double PillRingThickness { get; set; }

    public double PillGlyphSize { get; set; }

    public double PillGap { get; set; }

    public double PillFontSize { get; set; }

    /// <summary>The card's width inside the pill's popover.</summary>
    public double PopoverCardWidth { get; set; }

    /// <summary>The pointer-over fill of the pill: SurfaceMuted at this opacity.</summary>
    public double HoverOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BannerPadding = new Thickness(12, 10, 10, 10);
        BannerMinHeight = 56;
        CompactBelow = 600;
        BannerGap = 12;
        BannerRowGap = 10;
        BannerRingSize = 32;
        BannerRingThickness = 3;
        BannerDaysFontSize = 12;
        BannerGlyphSize = 14;
        TitleFontSize = 14;
        TitleLineHeight = 20;
        DetailFontSize = 13;
        DetailLineHeight = 18;
        DetailCompactHeight = 2 * DetailLineHeight;
        ActionHeight = 30;
        ActionPadding = new Thickness(12, 0);
        ActionGap = 8;
        ActionFontSize = 13;
        ToolSize = 28;
        ToolGlyphSize = 16;
        CardPadding = new Thickness(16);
        CardMaxWidth = 340;
        CardGap = 14;
        CardRingSize = 44;
        CardRingThickness = 4;
        CardDaysFontSize = 15;
        CardGlyphSize = 18;
        CardHeadTextMargin = new Thickness(12, 0, 4, 0);
        FeatureFontSize = 13;
        FeatureLineHeight = 18;
        FeatureIconSize = 14;
        FeatureGap = 8;
        FeatureIconGap = 8;
        NoteFontSize = 12;
        NoteLineHeight = 16;
        PillHeight = 30;
        PillPadding = new Thickness(6, 0, 12, 0);
        PillRingSize = 18;
        PillRingThickness = 2.5;
        PillGlyphSize = 10;
        PillGap = 7;
        PillFontSize = 12;
        PopoverCardWidth = 300;
        HoverOpacity = 0.6;
    }
}
