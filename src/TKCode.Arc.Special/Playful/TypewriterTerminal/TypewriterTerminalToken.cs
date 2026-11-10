using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="TypewriterTerminal"/>. The window's surfaces, its text and line colors, the shadow and the
/// focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class TypewriterTerminalToken : AbstractControlDesignToken
{
    /// <summary>Height of the title bar.</summary>
    public double TitleBarHeight { get; set; }

    /// <summary>Space around the title bar's content.</summary>
    public Thickness TitleBarPadding { get; set; }

    /// <summary>Diameter of the three window dots.</summary>
    public double DotSize { get; set; }

    /// <summary>Space around the text area.</summary>
    public Thickness ScreenPadding { get; set; }

    /// <summary>Size of the terminal text.</summary>
    public double TextSize { get; set; }

    /// <summary>Line height as a multiple of <see cref="TextSize"/>.</summary>
    public double LineSpacing { get; set; }

    /// <summary>The keyboard focus ring sits 4px outside the window.</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TitleBarHeight = 40;
        TitleBarPadding = new Thickness(14, 0, 8, 0);
        DotSize = 11;
        ScreenPadding = new Thickness(18, 14, 18, 18);
        TextSize = 13;
        LineSpacing = 1.65;
        FocusRingMargin = new Thickness(-4);
    }
}
