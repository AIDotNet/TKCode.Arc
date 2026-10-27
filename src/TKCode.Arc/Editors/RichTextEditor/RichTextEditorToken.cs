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

    /// <summary>The text color named gray in the Docs profile palette.</summary>
    public Color TextGray { get; set; }

    /// <summary>The text color named red in the Docs profile palette.</summary>
    public Color TextRed { get; set; }

    /// <summary>The text color named orange in the Docs profile palette.</summary>
    public Color TextOrange { get; set; }

    /// <summary>The text color named yellow in the Docs profile palette.</summary>
    public Color TextYellow { get; set; }

    /// <summary>The text color named green in the Docs profile palette.</summary>
    public Color TextGreen { get; set; }

    /// <summary>The text color named blue in the Docs profile palette.</summary>
    public Color TextBlue { get; set; }

    /// <summary>The text color named purple in the Docs profile palette.</summary>
    public Color TextPurple { get; set; }

    /// <summary>The highlight named gray in the Docs profile palette.</summary>
    public Color FillGray { get; set; }

    /// <summary>The highlight named red in the Docs profile palette.</summary>
    public Color FillRed { get; set; }

    /// <summary>The highlight named orange in the Docs profile palette.</summary>
    public Color FillOrange { get; set; }

    /// <summary>The highlight named yellow in the Docs profile palette.</summary>
    public Color FillYellow { get; set; }

    /// <summary>The highlight named green in the Docs profile palette.</summary>
    public Color FillGreen { get; set; }

    /// <summary>The highlight named blue in the Docs profile palette.</summary>
    public Color FillBlue { get; set; }

    /// <summary>The highlight named purple in the Docs profile palette.</summary>
    public Color FillPurple { get; set; }

    /// <summary>The Docs profile's left gutter for the block handle (two 22px buttons and their gaps).</summary>
    public Thickness GutterMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        SurfaceMinHeight = 128;
        GutterMargin = new Thickness(56, 0, 0, 0);
        ToolButtonSize = 32;
        ToolButtonRadius = new CornerRadius(11);
        ToolbarRadius = 16;
        SlashMenuRadius = 18;
        SlashRowHeight = 34;
        SlashRowRadius = new CornerRadius(12);
        SlashHighlightColor = Oklch.WithAlpha(neutrals.Foreground, 0.065);
        var hues = new[] { 260.0, 25.0, 55.0, 90.0, 150.0, 255.0, 305.0 };
        Color Text(int i) => Oklch.ToColor(isDarkMode ? 0.76 : 0.55, i == 0 ? 0.01 : isDarkMode ? 0.14 : 0.19, hues[i]);
        Color Fill(int i) => Oklch.ToColor(isDarkMode ? 0.38 : (i == 3 ? 0.95 : 0.93), i == 0 ? 0.01 : i == 3 ? 0.10 : 0.07, hues[i]);
        TextGray = Text(0);
        FillGray = Fill(0);
        TextRed = Text(1);
        FillRed = Fill(1);
        TextOrange = Text(2);
        FillOrange = Fill(2);
        TextYellow = Text(3);
        FillYellow = Fill(3);
        TextGreen = Text(4);
        FillGreen = Fill(4);
        TextBlue = Text(5);
        FillBlue = Fill(5);
        TextPurple = Text(6);
        FillPurple = Fill(6);
    }
}
