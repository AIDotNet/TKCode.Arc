using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class PasswordFieldToken : AbstractControlDesignToken
{
    /// <summary>The 30px toggle sits with an even inset on top, bottom and right: <c>(44 - 2 - 30) / 2</c>.</summary>
    public Thickness FramePaddingSM { get; set; }
    public Thickness FramePaddingMD { get; set; }
    public Thickness FramePaddingLG { get; set; }

    public double RevealButtonSize { get; set; }
    public double RevealButtonSizeSM { get; set; }
    public double EyeSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FramePaddingSM = new Thickness(10, 0, 4, 0);
        FramePaddingMD = new Thickness(12, 0, 6, 0);
        FramePaddingLG = new Thickness(14, 0, 9, 0);
        RevealButtonSize = 30;
        RevealButtonSizeSM = 26;
        EyeSize = 18;
    }
}
