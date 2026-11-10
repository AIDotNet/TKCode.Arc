using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="SandText"/>. The text color, its font and the focus ring come from the ArcDesign tokens in the
/// theme.
/// </summary>
[ControlDesignToken]
internal sealed class SandTextToken : AbstractControlDesignToken
{
    /// <summary>The keyboard focus ring sits 6px beside and 4px above and below the text.</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>Size of the default text: large letters give the grains room to read as letters.</summary>
    public double DefaultFontSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FocusRingMargin = new Thickness(-6, -4);
        DefaultFontSize = 52;
    }
}
