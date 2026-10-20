using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class AvatarGroupToken : AbstractControlDesignToken
{
    public double DiameterSM { get; set; }
    public double DiameterMD { get; set; }
    public double DiameterLG { get; set; }

    /// <summary>How far each avatar tucks under the next one.</summary>
    public double StackOverlap { get; set; }

    public double ChipFontSizeSM { get; set; }

    /// <summary>Padding of the name pill shown above a hovered person.</summary>
    public Thickness NamePadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        DiameterSM = 28;
        DiameterMD = 36;
        DiameterLG = 48;
        StackOverlap = 6;
        ChipFontSizeSM = 10;
        NamePadding = new Thickness(8, 4);
    }
}
