using Avalonia;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using Avalonia.Media.Immutable;
using TKCode.Arc.Primitives;
using Avalonia.Automation.Provider;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="CodeReview"/> (Arc Code review, design reference, ADR 0053) and the colors it derives from the
/// neutrals: the diff tints are the success and danger colors mixed into the surface, the active-line and approved fills
/// the foreground mixed into it. Colors that follow the accent, the radii and the fonts come from ArcDesign tokens.
/// Paddings are Avalonia thicknesses (left, top, right, bottom).
/// </summary>
[ControlDesignToken]
internal sealed class CodeReviewToken : AbstractControlDesignToken
{
    /// <summary>The widest the card grows (760).</summary>
    public double MaxWidth { get; set; }

    /// <summary>Below this card width the header stacks and one line-number column remains (560).</summary>
    public double CompactWidth { get; set; }

    /// <summary>The card's text size (13) and the small text of meta lines, pills and the diff (12).</summary>
    public double BodyFontSize { get; set; }

    public double SmallFontSize { get; set; }

    /// <summary>The title's size (17).</summary>
    public double TitleFontSize { get; set; }

    /// <summary>Height of a diff line (22) and of a hunk header (32).</summary>
    public double LineHeight { get; set; }

    public double HunkHeight { get; set; }

    /// <summary>Width of one line-number column (32.5) and the padding around the numbers (12 before, 6 after).</summary>
    public double NumberWidth { get; set; }

    public Thickness GutterPadding { get; set; }

    /// <summary>The compact gutter's padding (4 before, 6 after).</summary>
    public Thickness CompactGutterPadding { get; set; }

    /// <summary>Width of the sign column (+ / −), its 4 left padding included (21.7), and that padding (4).</summary>
    public double SignWidth { get; set; }

    public double SignInset { get; set; }

    /// <summary>The add-comment mark's size at rest (18) and its smallest scale (0.6).</summary>
    public double MarkSize { get; set; }

    public double MarkRestScale { get; set; }

    /// <summary>Header padding (24, 22, 24, 18) and its compact form (16, 18, 16, 16).</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness CompactHeaderPadding { get; set; }

    /// <summary>The side padding of the toolbar, the file header and the hunk headers (24) and its compact form (16).</summary>
    public double SidePadding { get; set; }

    public double CompactSidePadding { get; set; }

    /// <summary>A thread band's padding (28, 8, 24, 8) and its compact form (16, 8, 16, 8).</summary>
    public Thickness ThreadPadding { get; set; }

    public Thickness CompactThreadPadding { get; set; }

    /// <summary>The widest a thread's content grows (640).</summary>
    public double ThreadMaxWidth { get; set; }

    /// <summary>Width of the progress track (56) and the most it stretches in the compact layout (160).</summary>
    public double ProgressWidth { get; set; }

    public double CompactProgressWidth { get; set; }

    /// <summary>The diff's added line and gutter tints.</summary>
    public Color AddedLine { get; set; }

    public Color AddedGutter { get; set; }

    /// <summary>The diff's removed line and gutter tints.</summary>
    public Color DeletedLine { get; set; }

    public Color DeletedGutter { get; set; }

    /// <summary>A line whose suggestion was just applied starts with these stronger tints.</summary>
    public Color FreshLine { get; set; }

    public Color FreshGutter { get; set; }

    /// <summary>The line and gutter under an open comment box.</summary>
    public Color ActiveLine { get; set; }

    public Color ActiveGutter { get; set; }

    /// <summary>The approve button once approved, and the status line's fill.</summary>
    public Color ApprovedFill { get; set; }

    public Color ApprovedText { get; set; }

    public Color StatusFill { get; set; }

    /// <summary>The keyboard focus ring drawn inside the gutter and the tabs (the foreground at 72%).</summary>
    public Color FocusInset { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        MaxWidth = 760;
        CompactWidth = 560;
        BodyFontSize = 13;
        SmallFontSize = 12;
        TitleFontSize = 17;
        LineHeight = 22;
        HunkHeight = 32;
        NumberWidth = 32.5;
        GutterPadding = new Thickness(12, 0, 6, 0);
        CompactGutterPadding = new Thickness(4, 0, 6, 0);
        SignWidth = 21.7;
        SignInset = 4;
        MarkSize = 18;
        MarkRestScale = 0.6;
        HeaderPadding = new Thickness(24, 22, 24, 18);
        CompactHeaderPadding = new Thickness(16, 18, 16, 16);
        SidePadding = 24;
        CompactSidePadding = 16;
        ThreadPadding = new Thickness(28, 8, 24, 8);
        CompactThreadPadding = new Thickness(16, 8, 16, 8);
        ThreadMaxWidth = 640;
        ProgressWidth = 56;
        CompactProgressWidth = 160;
        // Arc mixes the diff colors into the surface: 10% / 16% in the dark, 8% / 13% in the light (removed 15% / 11%).
        AddedLine = Oklch.MixOklab(n.Surface, n.Success, isDarkMode ? 0.10 : 0.08);
        AddedGutter = Oklch.MixOklab(n.Surface, n.Success, isDarkMode ? 0.16 : 0.13);
        DeletedLine = Oklch.MixOklab(n.Surface, n.Danger, isDarkMode ? 0.10 : 0.07);
        DeletedGutter = Oklch.MixOklab(n.Surface, n.Danger, isDarkMode ? 0.15 : 0.11);
        FreshLine = Oklch.MixOklab(n.Surface, n.Success, isDarkMode ? 0.26 : 0.24);
        FreshGutter = Oklch.MixOklab(n.Surface, n.Success, isDarkMode ? 0.33 : 0.30);
        ActiveLine = Oklch.MixOklab(n.Surface, n.Foreground, isDarkMode ? 0.11 : 0.07);
        ActiveGutter = Oklch.MixOklab(n.Surface, n.Foreground, isDarkMode ? 0.17 : 0.126);
        ApprovedFill = Oklch.MixOklab(n.Surface, n.Foreground, 0.095);
        ApprovedText = Oklch.MixOklab(n.Surface, n.Foreground, 0.845);
        StatusFill = Oklch.MixOklab(n.Surface, n.Foreground, 0.055);
        FocusInset = Oklch.WithAlpha(n.Foreground, 0.72);
    }
}
