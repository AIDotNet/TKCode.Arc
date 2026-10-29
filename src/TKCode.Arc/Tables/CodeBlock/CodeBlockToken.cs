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

    public CornerRadius FocusRingRadius { get; set; }
    public CornerRadius ExpandFocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FocusRingRadius = new CornerRadius(12);
        ExpandFocusRingRadius = new CornerRadius(12);
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
        LineNumberForeground = Oklch.MixOklab(n.TextMuted, n.Surface, 0.25);
        SyntaxComment = CodeBlockSyntaxColors.Get(CodeBlockSyntaxKind.Comment, isDarkMode);
        SyntaxString = CodeBlockSyntaxColors.Get(CodeBlockSyntaxKind.String, isDarkMode);
        SyntaxNumber = CodeBlockSyntaxColors.Get(CodeBlockSyntaxKind.Number, isDarkMode);
        SyntaxKeyword = CodeBlockSyntaxColors.Get(CodeBlockSyntaxKind.Keyword, isDarkMode);
        SyntaxType = CodeBlockSyntaxColors.Get(CodeBlockSyntaxKind.Type, isDarkMode);
        SyntaxFunction = CodeBlockSyntaxColors.Get(CodeBlockSyntaxKind.Function, isDarkMode);
        SyntaxProperty = CodeBlockSyntaxColors.Get(CodeBlockSyntaxKind.Property, isDarkMode);
        SyntaxTag = CodeBlockSyntaxColors.Get(CodeBlockSyntaxKind.Tag, isDarkMode);
        SyntaxPunctuation = CodeBlockSyntaxColors.Get(CodeBlockSyntaxKind.Punctuation, isDarkMode);
    }
}

/// <summary>
/// Arc's syntax role colors (code-block.module.css): comments and punctuation use the neutral text roles, the rest an
/// oklch color per appearance. Shared by <see cref="CodeBlockToken"/> and the Markdown control's fenced code.
/// </summary>
internal static class CodeBlockSyntaxColors
{
    /// <summary>The color of <paramref name="kind"/>; <see cref="CodeBlockSyntaxKind.None"/> is the foreground.</summary>
    public static Color Get(CodeBlockSyntaxKind kind, bool isDarkMode)
    {
        var n = ArcPalette.Neutrals(isDarkMode);
        return kind switch
        {
            CodeBlockSyntaxKind.Comment => n.TextMuted,
            CodeBlockSyntaxKind.Punctuation => n.TextSecondary,
            CodeBlockSyntaxKind.String => isDarkMode ? Oklch.ToColor(0.77, 0.15, 150) : Oklch.ToColor(0.46, 0.13, 150),
            CodeBlockSyntaxKind.Number => isDarkMode ? Oklch.ToColor(0.80, 0.14, 75) : Oklch.ToColor(0.50, 0.14, 55),
            CodeBlockSyntaxKind.Keyword => isDarkMode ? Oklch.ToColor(0.78, 0.16, 285) : Oklch.ToColor(0.48, 0.15, 285),
            CodeBlockSyntaxKind.Type => isDarkMode ? Oklch.ToColor(0.79, 0.13, 230) : Oklch.ToColor(0.46, 0.13, 230),
            CodeBlockSyntaxKind.Function => isDarkMode ? Oklch.ToColor(0.80, 0.13, 25) : Oklch.ToColor(0.43, 0.12, 25),
            CodeBlockSyntaxKind.Property => isDarkMode ? Oklch.ToColor(0.79, 0.12, 85) : Oklch.ToColor(0.44, 0.12, 85),
            CodeBlockSyntaxKind.Tag => isDarkMode ? Oklch.ToColor(0.79, 0.13, 230) : Oklch.ToColor(0.46, 0.13, 230),
            _ => n.Foreground
        };
    }
}
