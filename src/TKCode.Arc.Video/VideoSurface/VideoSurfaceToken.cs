using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Colors of <see cref="VideoSurface"/> that do not follow the theme.</summary>
[ControlDesignToken]
public sealed class VideoSurfaceToken : AbstractControlDesignToken
{
    /// <summary>The bars around a picture that does not fill the surface: black in light and dark themes (video convention; self-designed).</summary>
    public Color LetterboxColor { get; set; }

    /// <inheritdoc />
    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LetterboxColor = Colors.Black;
    }
}
