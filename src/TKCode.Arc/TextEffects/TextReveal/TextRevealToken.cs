
namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class TextRevealToken : AbstractControlDesignToken
{
    /// <summary>Line length limit in "0" widths (Arc: max-width 24ch).</summary>
    public double LineLengthChars { get; set; }

    /// <summary>Line height of headings (leading-display) and paragraphs (leading-body).</summary>
    public double HeadingLeading { get; set; }

    public double ParagraphLeading { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LineLengthChars = 24;
        HeadingLeading = 1.1;
        ParagraphLeading = 1.4;
    }
}
