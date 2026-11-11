namespace TKCode.Arc;

/// <summary>
/// Tokens of a <see cref="RadioGroupItem"/> of its own. The row's ring, dot, insets and focus ring stay in
/// <c>RadioGroupToken</c>, where theme overrides already address them.
/// </summary>
[ControlDesignToken]
internal sealed class RadioGroupItemToken : AbstractControlDesignToken
{
    /// <summary>The smallest height of a row (0: as tall as its label and description), for touch layouts or rows of one height.</summary>
    public double MinRowHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MinRowHeight = 0;
    }
}
