namespace TKCode.Arc;

/// <summary>
/// Tokens of a <see cref="RadioCard"/> of its own. The card's radius, insets, indicator, icon box and focus ring stay in
/// <c>RadioCardsToken</c>, where theme overrides already address them.
/// </summary>
[ControlDesignToken]
internal sealed class RadioCardToken : AbstractControlDesignToken
{
    /// <summary>Size of the stroke icon a card draws for a <see cref="RadioCardOption.Icon"/> geometry.</summary>
    public double GlyphSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        GlyphSize = 18;
    }
}
