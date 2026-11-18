using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry and theme-dependent colors of <see cref="ModelViewer"/>.</summary>
[ControlDesignToken]
public sealed class ModelViewerToken : AbstractControlDesignToken
{
    /// <summary>The backdrop behind the model: a quiet neutral slightly away from the page background (self-designed).</summary>
    public Color StageColor { get; set; }

    /// <summary>Smallest height of the viewer (self-designed).</summary>
    public double MinHeight { get; set; }

    /// <summary>Distance of the toolbar from the viewer's edges (self-designed).</summary>
    public Thickness ToolbarMargin { get; set; }

    /// <summary>Gap between toolbar buttons (self-designed).</summary>
    public double ToolbarSpacing { get; set; }

    /// <summary>Size of the toolbar glyphs (self-designed).</summary>
    public double GlyphSize { get; set; }

    /// <summary>Diameter of the loading ring (self-designed).</summary>
    public double SpinnerSize { get; set; }

    /// <summary>Widest line of the error message (self-designed).</summary>
    public double MessageMaxWidth { get; set; }

    /// <inheritdoc />
    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        StageColor = Oklch.MixOklab(n.Background, n.Foreground, isDarkMode ? 0.06 : 0.04);
        MinHeight = 160;
        ToolbarMargin = new Thickness(8);
        ToolbarSpacing = 4;
        GlyphSize = 16;
        SpinnerSize = 24;
        MessageMaxWidth = 320;
    }
}
