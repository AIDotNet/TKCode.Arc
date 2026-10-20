using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class FloatingButtonGroupSeparatorToken : AbstractControlDesignToken
{
    /// <summary>Rule color: foreground at 12%.</summary>
    public Color RuleColor { get; set; }

    /// <summary>Rule length: half the button height.</summary>
    public double RuleLengthMD { get; set; }
    public double RuleLengthSM { get; set; }

    public Thickness RuleMargin { get; set; }
    public Thickness VerticalRuleMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RuleColor = Oklch.WithAlpha(ArcPalette.Neutrals(isDarkMode).Foreground, 0.12);
        RuleLengthMD = 18;
        RuleLengthSM = 15;
        RuleMargin = new Thickness(2, 0);
        VerticalRuleMargin = new Thickness(0, 2);
    }
}
