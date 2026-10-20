using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ExpandingButtonGroupItemToken : AbstractControlDesignToken
{
    /// <summary>Action height and collapsed width (28 / 36px).</summary>
    public double ActionExtentMD { get; set; }
    public double ActionExtentSM { get; set; }

    /// <summary>Icon size (14 / 16px).</summary>
    public double GlyphExtentMD { get; set; }
    public double GlyphExtentSM { get; set; }

    /// <summary>Left inset of the icon, the same collapsed and expanded (10 / 7px).</summary>
    public Thickness GlyphInsetMD { get; set; }
    public Thickness GlyphInsetSM { get; set; }

    /// <summary>Gap between icon and label (7 / 5px).</summary>
    public double GlyphGapMD { get; set; }
    public double GlyphGapSM { get; set; }

    /// <summary>Inset after the label (14 / 11px).</summary>
    public double LabelEndMD { get; set; }
    public double LabelEndSM { get; set; }

    /// <summary>Danger tone expanded background: danger mixed 10% (14% dark) into the surface.</summary>
    public Color DangerExpandedColor { get; set; }

    /// <summary>Confirmation background: success mixed 10% (14% dark) into the surface.</summary>
    public Color DoneColor { get; set; }

    public double DisabledOpacity { get; set; }

    /// <summary>Opacity of the icon while busy under reduced motion (no pulse).</summary>
    public double BusyGlyphOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ActionExtentMD = 36;
        ActionExtentSM = 28;
        GlyphExtentMD = 16;
        GlyphExtentSM = 14;
        GlyphInsetMD = new Thickness(10, 0, 0, 0);
        GlyphInsetSM = new Thickness(7, 0, 0, 0);
        GlyphGapMD = 7;
        GlyphGapSM = 5;
        LabelEndMD = 14;
        LabelEndSM = 11;
        DangerExpandedColor = Oklch.MixOklab(n.Surface, n.Danger, isDarkMode ? 0.14 : 0.10);
        DoneColor = Oklch.MixOklab(n.Surface, n.Success, isDarkMode ? 0.14 : 0.10);
        DisabledOpacity = 0.5;
        BusyGlyphOpacity = 0.6;
    }
}
