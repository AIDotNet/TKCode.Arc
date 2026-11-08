using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry and the accent-independent notice colors of <see cref="SeatManager"/> (Arc Seat manager, design reference).</summary>
[ControlDesignToken]
internal sealed class SeatManagerToken : AbstractControlDesignToken
{
    /// <summary>Widest the card grows.</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>Space inside each section.</summary>
    public Thickness SectionPadding { get; set; }

    /// <summary>Space inside each section below <see cref="NarrowWidth"/>.</summary>
    public Thickness NarrowSectionPadding { get; set; }

    /// <summary>Side padding of the footer.</summary>
    public Thickness FooterPadding { get; set; }

    /// <summary>Side padding of the footer below <see cref="NarrowWidth"/>.</summary>
    public Thickness NarrowFooterPadding { get; set; }

    /// <summary>Card width below which the paddings tighten, the quote notes and the legend's change entry hide.</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Height of the footer.</summary>
    public double FooterHeight { get; set; }

    /// <summary>Title font size.</summary>
    public double TitleSize { get; set; }

    /// <summary>Title letter spacing.</summary>
    public double TitleLetterSpacing { get; set; }

    /// <summary>Letter spacing of body copy.</summary>
    public double CopyLetterSpacing { get; set; }

    /// <summary>Line height of 14px copy.</summary>
    public double CopyLineHeight { get; set; }

    /// <summary>Font size of the small copy (subtitle, legend, notes).</summary>
    public double SmallSize { get; set; }

    /// <summary>Height of the plan badge.</summary>
    public double BadgeHeight { get; set; }

    /// <summary>Side padding of the plan badge.</summary>
    public Thickness BadgePadding { get; set; }

    /// <summary>Gap between the title row and the subtitle.</summary>
    public double HeaderGap { get; set; }

    /// <summary>Gap between the header and the overview rows.</summary>
    public double HeaderBottom { get; set; }

    /// <summary>Gap between the overview rows.</summary>
    public double RowGap { get; set; }

    /// <summary>Avatar diameter.</summary>
    public double AvatarDiameter { get; set; }

    /// <summary>How far each avatar overlaps the previous one.</summary>
    public double AvatarOverlap { get; set; }

    /// <summary>Width of the surface ring around each avatar.</summary>
    public double AvatarRing { get; set; }

    /// <summary>Height of a seat cell.</summary>
    public double CellHeight { get; set; }

    /// <summary>Corner radius of a seat cell.</summary>
    public double CellRadius { get; set; }

    /// <summary>Gap between neighbouring seat cells.</summary>
    public double CellGap { get; set; }

    /// <summary>Gap between legend entries.</summary>
    public double LegendGap { get; set; }

    /// <summary>Size of a legend dot.</summary>
    public double LegendDot { get; set; }

    /// <summary>Gap between the notice icon, text and action.</summary>
    public double NoticeGap { get; set; }

    /// <summary>Size of the 16px glyphs.</summary>
    public double GlyphSize { get; set; }

    /// <summary>Stroke of the 16px glyphs.</summary>
    public double GlyphStroke { get; set; }

    /// <summary>Gap between the stepper row and the quote.</summary>
    public double QuoteGap { get; set; }

    /// <summary>Gap between the two quote rows.</summary>
    public double QuoteLineGap { get; set; }

    /// <summary>Width of the stepper.</summary>
    public double StepperWidth { get; set; }

    /// <summary>Height of the stepper.</summary>
    public double StepperHeight { get; set; }

    /// <summary>Size of the stepper's buttons.</summary>
    public double StepButtonSize { get; set; }

    /// <summary>Width of the seat count in the stepper.</summary>
    public double SpinWidth { get; set; }

    /// <summary>Height of an invite row.</summary>
    public double InviteRowHeight { get; set; }

    /// <summary>Margin of the invite list (pulls it toward the section's edges).</summary>
    public Thickness InviteListMargin { get; set; }

    /// <summary>Margin of the invite actions (their text lines up with the section's right edge).</summary>
    public Thickness InviteActionsMargin { get; set; }

    /// <summary>Success glyph color (circle-check, Sent): a calm green, lower in chroma than the Success token.</summary>
    public Color SuccessInk { get; set; }

    /// <summary>Warning glyph color of the full-team notice: a calm amber.</summary>
    public Color WarningInk { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardMaxWidth = 560;
        SectionPadding = new Thickness(24);
        NarrowSectionPadding = new Thickness(20);
        FooterPadding = new Thickness(24, 0);
        NarrowFooterPadding = new Thickness(20, 0);
        NarrowWidth = 460;
        FooterHeight = 64;
        TitleSize = 16;
        TitleLetterSpacing = -0.48;
        CopyLetterSpacing = -0.14;
        CopyLineHeight = 20;
        SmallSize = 13;
        BadgeHeight = 20;
        BadgePadding = new Thickness(8, 0);
        HeaderGap = 4;
        HeaderBottom = 16;
        RowGap = 12;
        AvatarDiameter = 28;
        AvatarOverlap = 6;
        AvatarRing = 2;
        CellHeight = 24;
        CellRadius = 5;
        CellGap = 3;
        LegendGap = 16;
        LegendDot = 8;
        NoticeGap = 8;
        GlyphSize = 16;
        GlyphStroke = 1.75;
        QuoteGap = 16;
        QuoteLineGap = 8;
        StepperWidth = 120;
        StepperHeight = 40;
        StepButtonSize = 32;
        SpinWidth = 44;
        InviteRowHeight = 56;
        InviteListMargin = new Thickness(0, -8, 0, -16);
        InviteActionsMargin = new Thickness(0, 0, -12, 0);
        SuccessInk = isDarkMode ? Oklch.ToColor(0.79, 0.14, 162) : Oklch.ToColor(0.50, 0.12, 162);
        WarningInk = isDarkMode ? Oklch.ToColor(0.83, 0.12, 78) : Oklch.ToColor(0.55, 0.12, 68);
    }
}
