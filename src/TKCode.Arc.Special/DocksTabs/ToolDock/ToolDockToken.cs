using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and accent-independent colors of <see cref="ToolDock"/> (Arc Dock, design reference), as measured: 40 px
/// slots 4 px apart inside a 6 px padding, the 36 px tray members, the tray kept 30 px from the bar's ends, the 8 px
/// fillet where the tray grows out of the bar, the floating label and the badge. Surfaces, text, accent, radii, shadows
/// and type come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ToolDockToken : AbstractControlDesignToken
{
    /// <summary>The side of a slot in the bar (40).</summary>
    public double SlotSize { get; set; }

    /// <summary>The space between two slots (4).</summary>
    public double SlotSpacing { get; set; }

    /// <summary>The space between the bar's edge and its slots (6).</summary>
    public Thickness BarPadding { get; set; }

    /// <summary>The side of a slot's or a member's glyph (20).</summary>
    public double GlyphSize { get; set; }

    /// <summary>The side of a member in a tray (36).</summary>
    public double MemberSize { get; set; }

    /// <summary>The space between a tray's edge and its members (6).</summary>
    public Thickness TrayPadding { get; set; }

    /// <summary>The space between two members of a tray (4).</summary>
    public double TraySpacing { get; set; }

    /// <summary>How close a tray may come to either end of the bar (30).</summary>
    public double TrayInset { get; set; }

    /// <summary>The radius of the inward curve where a tray meets the bar (8).</summary>
    public double FilletRadius { get; set; }

    /// <summary>The width of the hairline around the bar and the tray (0.5).</summary>
    public double OutlineThickness { get; set; }

    /// <summary>The hairline around the bar and the tray: the text color at 13% (white at 12% in the dark theme).</summary>
    public Color OutlineColor { get; set; }

    /// <summary>The fill under a slot or a member while the mouse is over it: the text color at 6%.</summary>
    public Color HoverFillColor { get; set; }

    /// <summary>The height of the floating label (28).</summary>
    public double LabelHeight { get; set; }

    /// <summary>The gap between the floating label and the bar or the tray under it (10).</summary>
    public double LabelGap { get; set; }

    /// <summary>The space at the ends of the floating label (10).</summary>
    public Thickness LabelPadding { get; set; }

    /// <summary>The side of the key hint in the floating label (16).</summary>
    public double ShortcutSize { get; set; }

    /// <summary>The type size of the key hint (10).</summary>
    public double ShortcutFontSize { get; set; }

    /// <summary>The space between the label's text and its key hint (8).</summary>
    public double ShortcutSpacing { get; set; }

    /// <summary>The key hint's fill: the label's text color (the page background) at 18%.</summary>
    public Color ShortcutFillColor { get; set; }

    /// <summary>The height of a badge (16).</summary>
    public double BadgeHeight { get; set; }

    /// <summary>The space at the ends of a badge (5).</summary>
    public Thickness BadgePadding { get; set; }

    /// <summary>The type size of a badge's count (11).</summary>
    public double BadgeFontSize { get; set; }

    /// <summary>Where a badge sits in its slot: 21 from the left, 3 from the top.</summary>
    public Thickness BadgeMargin { get; set; }

    /// <summary>The ring of bar fill around a badge (2).</summary>
    public double BadgeRingThickness { get; set; }

    /// <summary>The count's color on a badge (white in both themes).</summary>
    public Color BadgeForegroundColor { get; set; }

    /// <summary>The side of the corner mark that tells a group from a plain slot (5).</summary>
    public double MarkSize { get; set; }

    /// <summary>Where the corner mark sits in its slot: 28 from the left and the top.</summary>
    public Thickness MarkMargin { get; set; }

    /// <summary>The corner mark's opacity (0.55).</summary>
    public double MarkOpacity { get; set; }

    /// <summary>The keyboard focus ring around a slot or a member: 2 px outside it.</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SlotSize = 40;
        SlotSpacing = 4;
        BarPadding = new Thickness(6);
        GlyphSize = 20;
        MemberSize = 36;
        TrayPadding = new Thickness(6);
        TraySpacing = 4;
        TrayInset = 30;
        FilletRadius = 8;
        OutlineThickness = 0.5;
        LabelHeight = 28;
        LabelGap = 10;
        LabelPadding = new Thickness(10, 0);
        ShortcutSize = 16;
        ShortcutFontSize = 10;
        ShortcutSpacing = 8;
        BadgeHeight = 16;
        BadgePadding = new Thickness(5, 0);
        BadgeFontSize = 11;
        BadgeMargin = new Thickness(21, 3, 0, 0);
        BadgeRingThickness = 2;
        BadgeForegroundColor = Colors.White;
        MarkSize = 5;
        MarkMargin = new Thickness(28, 28, 0, 0);
        MarkOpacity = 0.55;
        FocusRingMargin = new Thickness(-4);
        var n = ArcPalette.Neutrals(isDarkMode);
        OutlineColor = isDarkMode ? Oklch.WithAlpha(Colors.White, 0.12) : Oklch.WithAlpha(n.Foreground, 0.13);
        HoverFillColor = Oklch.WithAlpha(n.Foreground, 0.06);
        ShortcutFillColor = Oklch.WithAlpha(n.Background, 0.18);
    }
}
