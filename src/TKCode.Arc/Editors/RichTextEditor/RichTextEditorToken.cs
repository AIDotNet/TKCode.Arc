using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class RichTextEditorToken : AbstractControlDesignToken
{
    /// <summary>Arc's <c>min-height: 8rem</c> for the writing surface.</summary>
    public double SurfaceMinHeight { get; set; }

    /// <summary>Toolbar and link field buttons (32px).</summary>
    public double ToolButtonSize { get; set; }

    public CornerRadius ToolButtonRadius { get; set; }

    public double ToolbarRadius { get; set; }

    public double SlashMenuRadius { get; set; }

    /// <summary>Slash row height (34px) and its highlight radius (12px).</summary>
    public double SlashRowHeight { get; set; }

    public CornerRadius SlashRowRadius { get; set; }

    /// <summary>The slash highlight: foreground at 6.5%.</summary>
    public Color SlashHighlightColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        SurfaceMinHeight = 128;
        ToolButtonSize = 32;
        ToolButtonRadius = new CornerRadius(11);
        ToolbarRadius = 16;
        SlashMenuRadius = 18;
        SlashRowHeight = 34;
        SlashRowRadius = new CornerRadius(12);
        SlashHighlightColor = Oklch.WithAlpha(neutrals.Foreground, 0.065);
    }
}
