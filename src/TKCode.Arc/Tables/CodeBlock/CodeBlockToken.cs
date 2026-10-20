using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// CodeBlock geometry and the syntax palette. Arc gives each syntax role an oklch color per theme; they do not
/// follow the accent, so they live here (comments and punctuation use the neutral text roles).
/// </summary>
[ControlDesignToken]
internal sealed class CodeBlockToken : AbstractControlDesignToken
{
    public double HeaderMinHeight { get; set; }
    public Thickness HeaderPadding { get; set; }
    public Thickness HeaderPaddingCompact { get; set; }
    public CornerRadius HeaderCornerRadius { get; set; }
    public CornerRadius ExpandCornerRadius { get; set; }
    public double HeaderGap { get; set; }
    public Thickness CodePadding { get; set; }
    public Thickness CodePaddingCompact { get; set; }
    public Thickness LineNumbersPadding { get; set; }
    public Thickness LineNumbersPaddingCompact { get; set; }
    public double CodeLineHeight { get; set; }
    public double CodeLineHeightCompact { get; set; }
    public double ExpandMinHeight { get; set; }
    public Thickness ExpandPadding { get; set; }

    /// <summary>Below this width the block uses Arc's narrow layout (smaller text, icon-only copy).</summary>
    public double CompactWidth { get; set; }

    public Color SyntaxComment { get; set; }
    public Color SyntaxString { get; set; }
    public Color SyntaxNumber { get; set; }
    public Color SyntaxKeyword { get; set; }
    public Color SyntaxType { get; set; }
    public Color SyntaxFunction { get; set; }
    public Color SyntaxProperty { get; set; }
    public Color SyntaxTag { get; set; }
    public Color SyntaxPunctuation { get; set; }
    public Color LineNumberForeground { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        HeaderMinHeight = 52;
        HeaderPadding = new Thickness(16, 8, 12, 8);
        HeaderPaddingCompact = new Thickness(12, 8, 12, 8);
        HeaderCornerRadius = new CornerRadius(25, 25, 0, 0);
        ExpandCornerRadius = new CornerRadius(0, 0, 25, 25);
        HeaderGap = 12;
        CodePadding = new Thickness(20);
        CodePaddingCompact = new Thickness(16);
        LineNumbersPadding = new Thickness(20, 20, 0, 20);
        LineNumbersPaddingCompact = new Thickness(16, 16, 0, 16);
        // Arc: font-size var(--text-sm) / var(--text-xs) with line-height 1.7.
        CodeLineHeight = 14 * 1.7;
        CodeLineHeightCompact = 12 * 1.7;
        ExpandMinHeight = 44;
        ExpandPadding = new Thickness(16, 0);
        CompactWidth = 420;

        var n = ArcPalette.Neutrals(isDarkMode);
        SyntaxComment = n.TextMuted;
        SyntaxPunctuation = n.TextSecondary;
        LineNumberForeground = Oklch.MixOklab(n.TextMuted, n.Surface, 0.25);
        if (isDarkMode)
        {
            SyntaxString = Oklch.ToColor(0.77, 0.15, 150);
            SyntaxNumber = Oklch.ToColor(0.80, 0.14, 75);
            SyntaxKeyword = Oklch.ToColor(0.78, 0.16, 285);
            SyntaxType = Oklch.ToColor(0.79, 0.13, 230);
            SyntaxFunction = Oklch.ToColor(0.80, 0.13, 25);
            SyntaxProperty = Oklch.ToColor(0.79, 0.12, 85);
            SyntaxTag = Oklch.ToColor(0.79, 0.13, 230);
        }
        else
        {
            SyntaxString = Oklch.ToColor(0.46, 0.13, 150);
            SyntaxNumber = Oklch.ToColor(0.50, 0.14, 55);
            SyntaxKeyword = Oklch.ToColor(0.48, 0.15, 285);
            SyntaxType = Oklch.ToColor(0.46, 0.13, 230);
            SyntaxFunction = Oklch.ToColor(0.43, 0.12, 25);
            SyntaxProperty = Oklch.ToColor(0.44, 0.12, 85);
            SyntaxTag = Oklch.ToColor(0.46, 0.13, 230);
        }
    }
}
