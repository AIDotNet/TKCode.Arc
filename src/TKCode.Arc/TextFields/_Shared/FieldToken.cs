using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class FieldToken : AbstractControlDesignToken
{
    /// <summary>Label to editor gap (<c>margin-bottom: var(--space-2)</c>).</summary>
    public Thickness LabelMargin { get; set; }

    /// <summary>14px label on a 1.4 line (12px for Small).</summary>
    public double LabelLineHeight { get; set; }
    public double LabelLineHeightSM { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LabelMargin = new Thickness(0, 0, 0, 8);
        LabelLineHeight = 14 * 1.4;
        LabelLineHeightSM = 12 * 1.4;
    }
}
