using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="SandText"/>. The grain color (the accent) and the font come from the ArcDesign tokens in the
/// theme.
/// </summary>
[ControlDesignToken]
internal sealed class SandTextToken : AbstractControlDesignToken
{
    /// <summary>
    /// Kept for theme overrides written against earlier versions: Sand text is no longer focusable and draws no focus
    /// ring.
    /// </summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>Size of the letters when <see cref="SandText.FontSizeRatio"/> is not set.</summary>
    public double DefaultFontSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FocusRingMargin = new Thickness(-6, -4);
        DefaultFontSize = 52;
    }
}
