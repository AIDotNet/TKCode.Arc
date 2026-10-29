
namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ChipGroupToken : AbstractControlDesignToken
{
    /// <summary>Space before and after a chip label (14px).</summary>
    public double ChipInlinePadding { get; set; }

    /// <summary>Gap between chips and between lines (space-2).</summary>
    public double ChipGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ChipInlinePadding = 14;
        ChipGap = 8;
    }
}
