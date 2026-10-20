using AtomUI.Theme.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// Markdown document geometry. Arc has no Markdown component; the values follow its rich text editor's content styles
/// (base text with 1.65 leading). Colors and fonts come from ArcDesign tokens through StreamMark's <c>Md.*</c>
/// resources (see <see cref="Markdown"/>).
/// </summary>
[ControlDesignToken]
internal sealed class MarkdownToken : AbstractControlDesignToken
{
    /// <summary>Body line height as a multiple of the font size.</summary>
    public double BodyLeading { get; set; }

    /// <summary>Vertical space between blocks.</summary>
    public double BlockGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        // Arc rich text: font-size var(--text-base); line-height 1.65; blocks .6em apart (StreamMark spaces every block
        // the same, so code blocks and tables get a little more room: 12px at 16px text).
        BodyLeading = 1.65;
        BlockGap = 12;
    }
}
