using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class FieldIconButtonToken : AbstractControlDesignToken
{
    /// <summary>Default square size (PasswordField reveal: 30px).</summary>
    public double ButtonSize { get; set; }

    /// <summary>S7 ring: 2px wide, 2px outside the button.</summary>
    public Thickness FocusRingMargin { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ButtonSize = 30;
        FocusRingMargin = new Thickness(-4);
        FocusRingRadius = new CornerRadius(22);
    }
}
