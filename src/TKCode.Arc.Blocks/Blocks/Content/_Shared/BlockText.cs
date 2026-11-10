using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Text built in code for the Content and Pricing blocks' rows and cards: its size and color bound to ArcDesign tokens, with
/// Arc's body metrics (line height 1.4, tracking -0.01em) unless it passes its own.
/// </summary>
internal static class BlockText
{
    public static TextBlock Create(string? text, ArcDesignTokenKind size, double pixels, ArcDesignTokenKind? color = null, bool medium = false,
        TextWrapping wrapping = TextWrapping.Wrap, double? lineHeight = null, double? tracking = null, int maxLines = 0)
    {
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = wrapping,
            LineHeight = lineHeight ?? Math.Round(pixels * 1.4, 2),
            LetterSpacing = tracking ?? pixels * -0.01,
        };
        if (maxLines > 0)
        {
            block.MaxLines = maxLines;
            block.TextTrimming = TextTrimming.CharacterEllipsis;
        }
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, size);
        if (color is { } kind)
        {
            ArcTokens.Bind(block, TextBlock.ForegroundProperty, kind);
        }
        if (medium)
        {
            block.FontWeight = FontWeight.Medium;
        }
        return block;
    }
}
