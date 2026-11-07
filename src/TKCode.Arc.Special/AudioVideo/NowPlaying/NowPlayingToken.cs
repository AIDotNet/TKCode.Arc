using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and accent-independent colors of <see cref="NowPlaying"/> (Arc Now playing, design reference), as measured:
/// the mini bar's and full player's radii and button sizes, the dark glass of the collapse button (the same in both
/// themes), the waveform's unplayed bars (the text color at 16%) and the play button's pressed fill. Surfaces, text,
/// shadow and type come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class NowPlayingToken : AbstractControlDesignToken
{
    /// <summary>The widest the shell gets (336; narrower containers make it 100% wide).</summary>
    public double ShellMaxWidth { get; set; }

    /// <summary>The shell's radius in the mini bar (22) and the full player (30); the morph interpolates between them.</summary>
    public CornerRadius MiniShellCornerRadius { get; set; }

    public CornerRadius FullShellCornerRadius { get; set; }

    /// <summary>The clip inside the shell's 1px border: the shell's radius minus 1 (21 and 29).</summary>
    public CornerRadius MiniClipCornerRadius { get; set; }

    public CornerRadius FullClipCornerRadius { get; set; }

    /// <summary>The artwork's radius: the shell's minus its 10 inset (12 in the mini bar, 20 in the full player).</summary>
    public CornerRadius MiniArtCornerRadius { get; set; }

    public CornerRadius FullArtCornerRadius { get; set; }

    /// <summary>The mini play and next buttons (40) and their glyphs (18).</summary>
    public double MiniButtonSize { get; set; }

    public double MiniGlyphSize { get; set; }

    /// <summary>The full play button (52, glyph 24) and the previous / next buttons (44, glyph 20).</summary>
    public double PlayButtonSize { get; set; }

    public double PlayGlyphSize { get; set; }

    public double SkipButtonSize { get; set; }

    public double SkipGlyphSize { get; set; }

    /// <summary>The collapse button on the artwork (34, chevron 18).</summary>
    public double CollapseButtonSize { get; set; }

    /// <summary>The collapse button's glass: near black at 42%, 56% on hover, with a near-white chevron (both themes).</summary>
    public Color CollapseFillColor { get; set; }

    public Color CollapseHoverFillColor { get; set; }

    public Color CollapseGlyphColor { get; set; }

    /// <summary>The waveform's unplayed bars: the text color at 16%.</summary>
    public Color WaveUnplayedColor { get; set; }

    /// <summary>The full play button while pressed: the text color lifted toward the surface (L 0.15 to about 0.27).</summary>
    public Color PlayPressedColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ShellMaxWidth = 336;
        MiniShellCornerRadius = new CornerRadius(22);
        FullShellCornerRadius = new CornerRadius(30);
        MiniClipCornerRadius = new CornerRadius(21);
        FullClipCornerRadius = new CornerRadius(29);
        MiniArtCornerRadius = new CornerRadius(12);
        FullArtCornerRadius = new CornerRadius(20);
        MiniButtonSize = 40;
        MiniGlyphSize = 18;
        PlayButtonSize = 52;
        PlayGlyphSize = 24;
        SkipButtonSize = 44;
        SkipGlyphSize = 20;
        CollapseButtonSize = 34;
        CollapseFillColor = Oklch.ToColor(0.15, 0, 0, 0.42);
        CollapseHoverFillColor = Oklch.ToColor(0.15, 0, 0, 0.56);
        CollapseGlyphColor = Oklch.ToColor(0.98, 0, 0, 1);
        var n = ArcPalette.Neutrals(isDarkMode);
        WaveUnplayedColor = Oklch.WithAlpha(n.Foreground, 0.16);
        PlayPressedColor = Oklch.MixOklab(n.Foreground, n.Background, 0.14);
    }
}
