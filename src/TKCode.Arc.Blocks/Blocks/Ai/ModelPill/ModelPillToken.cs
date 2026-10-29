using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ModelPill"/>: the standalone look measured from Arc's model picker and the toolbar look measured
/// from its AI composer. Colors, radii and fonts come from ArcDesign tokens in the theme; the toolbar's two tints are
/// derived from the neutral text color (accent-independent).
/// </summary>
[ControlDesignToken]
internal sealed class ModelPillToken : AbstractControlDesignToken
{
    /// <summary>The pill is 40px high.</summary>
    public double PillHeight { get; set; }

    /// <summary>0 12 0 13: one more pixel on the left so the mark looks centered in the rounded end.</summary>
    public Thickness PillPadding { get; set; }

    /// <summary>The model mark (16px, no tile) and the 8px after it.</summary>
    public double MarkSize { get; set; }

    public Thickness MarkMargin { get; set; }

    /// <summary>Space between the model name and the effort (7px).</summary>
    public double EffortGap { get; set; }

    /// <summary>ChevronDown: 15px at stroke 1.75, 8px after the text.</summary>
    public double ChevronSize { get; set; }

    public double ChevronStroke { get; set; }

    public Thickness ChevronMargin { get; set; }

    /// <summary>Name and effort: 14px text on a 19.6px line, tracked −0.01em.</summary>
    public double LabelLineHeight { get; set; }

    public double LabelLetterSpacing { get; set; }

    /// <summary>The keyboard ring: 2px wide, 2px outside the border.</summary>
    public Thickness FocusRingMargin { get; set; }

    public double DisabledOpacity { get; set; }

    /// <summary>Without its text (<c>ShowText</c> false) the mark sits 4px before the chevron.</summary>
    public Thickness CompactMarkMargin { get; set; }

    /// <summary>The toolbar chip (Arc's AI composer) is 36px high, without a border.</summary>
    public double ToolbarHeight { get; set; }

    /// <summary>0 8 0 10 around mark, text and chevron; 0 6 0 10 when only the mark and the chevron remain.</summary>
    public Thickness ToolbarPadding { get; set; }

    public Thickness ToolbarCompactPadding { get; set; }

    /// <summary>6px between the mark, the text and the chevron, and between the name and the effort.</summary>
    public Thickness ToolbarMarkMargin { get; set; }

    public Thickness ToolbarChevronMargin { get; set; }

    public double ToolbarEffortGap { get; set; }

    /// <summary>ChevronDown 14 at stroke 2.</summary>
    public double ToolbarChevronSize { get; set; }

    public double ToolbarChevronStroke { get; set; }

    /// <summary>The chip is clear at rest: the text color at 6% under the pointer, 10% while its menu is open.</summary>
    public Color ToolbarHoverBackground { get; set; }

    public Color ToolbarOpenBackground { get; set; }

    /// <summary>The chat header's pill (<see cref="ModelPillAppearance.Header"/>): 34px high, otherwise the toolbar chip's spacing.</summary>
    public double HeaderHeight { get; set; }

    /// <summary>Its mark: 15.</summary>
    public double HeaderMarkSize { get; set; }

    /// <summary>Its chevron's stroke: 1.75 (ChevronDown 14).</summary>
    public double HeaderChevronStroke { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        PillHeight = 40;
        PillPadding = new Thickness(13, 0, 12, 0);
        MarkSize = 16;
        MarkMargin = new Thickness(0, 0, 8, 0);
        EffortGap = 7;
        ChevronSize = 15;
        ChevronStroke = 1.75;
        ChevronMargin = new Thickness(8, 0, 0, 0);
        LabelLineHeight = 14 * 1.4;
        LabelLetterSpacing = 14 * -0.01;
        FocusRingMargin = new Thickness(-4);
        DisabledOpacity = 0.5;
        CompactMarkMargin = new Thickness(0, 0, 4, 0);
        ToolbarHeight = 36;
        ToolbarPadding = new Thickness(10, 0, 8, 0);
        ToolbarCompactPadding = new Thickness(10, 0, 6, 0);
        ToolbarMarkMargin = new Thickness(0, 0, 6, 0);
        ToolbarChevronMargin = new Thickness(6, 0, 0, 0);
        ToolbarEffortGap = 6;
        ToolbarChevronSize = 14;
        HeaderHeight = 34;
        HeaderMarkSize = 15;
        HeaderChevronStroke = 1.75;
        ToolbarChevronStroke = 2;
        ToolbarHoverBackground = Oklch.WithAlpha(n.Foreground, 0.06);
        ToolbarOpenBackground = Oklch.WithAlpha(n.Foreground, 0.10);
    }
}
