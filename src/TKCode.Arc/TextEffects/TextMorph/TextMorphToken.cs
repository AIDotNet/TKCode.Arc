using AtomUI.Theme.DesignTokens;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class TextMorphToken : AbstractControlDesignToken
{
    /// <summary>How far an entering glyph rises, in em (Arc: 0.14em).</summary>
    public double EnterRiseEm { get; set; }

    /// <summary>How far a leaving glyph lifts, in em (Arc: 0.1em).</summary>
    public double ExitLiftEm { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        EnterRiseEm = 0.14;
        ExitLiftEm = 0.1;
    }
}
