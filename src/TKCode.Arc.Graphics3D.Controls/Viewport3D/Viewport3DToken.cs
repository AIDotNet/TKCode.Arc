using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Values of <see cref="Viewport3D"/> that follow the light or dark theme but not the accent.</summary>
[ControlDesignToken]
public sealed class Viewport3DToken : AbstractControlDesignToken
{
    /// <summary>The backdrop behind a scene when the viewport asks for an opaque stage: a quiet neutral, slightly away
    /// from the page background so that models read against it (self-designed).</summary>
    public Color StageColor { get; set; }

    /// <summary>Samples per pixel of multisample anti-aliasing when a viewport does not set its own (self-designed).</summary>
    public int Samples { get; set; }

    /// <inheritdoc />
    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        StageColor = Oklch.MixOklab(n.Background, n.Foreground, isDarkMode ? 0.06 : 0.04);
        Samples = 4;
    }
}
