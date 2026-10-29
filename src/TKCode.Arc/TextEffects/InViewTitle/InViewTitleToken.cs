
namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class InViewTitleToken : AbstractControlDesignToken
{
    /// <summary>Line height of the title (leading-display).</summary>
    public double TitleLeading { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TitleLeading = 1.1;
    }
}
