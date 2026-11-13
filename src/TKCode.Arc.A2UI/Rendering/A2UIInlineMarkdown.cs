using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc.A2UI;

/// <summary>
/// The "simple Markdown" of the basic catalog's Text: a leading <c>#</c> to <c>###</c> makes a heading; <c>**bold**</c>,
/// <c>*italic*</c> and <c>`code`</c> inline. Everything else is plain text; no links, no HTML.
/// </summary>
internal static class A2UIInlineMarkdown
{
    public static void Apply(TextBlock block, string text)
    {
        var level = 0;
        while (level < text.Length && level < 3 && text[level] == '#')
        {
            level++;
        }

        if (level > 0 && level < text.Length && text[level] == ' ')
        {
            text = text[(level + 1)..];
            block.FontWeight = FontWeight.SemiBold;
            ArcTokens.Bind(block, TextBlock.FontSizeProperty, level switch
            {
                1 => ArcDesignTokenKind.TypeXL,
                2 => ArcDesignTokenKind.TypeLG,
                _ => ArcDesignTokenKind.TypeBase
            });
        }
        else
        {
            block.FontWeight = FontWeight.Normal;
        }

        if (text.IndexOfAny(['*', '`']) < 0)
        {
            block.Inlines = null;
            block.Text = text;
            return;
        }

        var inlines = new InlineCollection();
        var i = 0;
        var plain = new System.Text.StringBuilder();
        while (i < text.Length)
        {
            if (TryMarker(text, i, "**", out var boldEnd))
            {
                Flush(inlines, plain);
                inlines.Add(new Run(text[(i + 2)..boldEnd]) { FontWeight = FontWeight.SemiBold });
                i = boldEnd + 2;
                continue;
            }

            if (TryMarker(text, i, "*", out var italicEnd))
            {
                Flush(inlines, plain);
                inlines.Add(new Run(text[(i + 1)..italicEnd]) { FontStyle = FontStyle.Italic });
                i = italicEnd + 1;
                continue;
            }

            if (TryMarker(text, i, "`", out var codeEnd))
            {
                Flush(inlines, plain);
                var run = new Run(text[(i + 1)..codeEnd]);
                ArcTokens.Bind(run, TextElement.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
                inlines.Add(run);
                i = codeEnd + 1;
                continue;
            }

            plain.Append(text[i]);
            i++;
        }

        Flush(inlines, plain);
        block.Text = null;
        block.Inlines = inlines;
    }

    private static bool TryMarker(string text, int at, string marker, out int end)
    {
        end = -1;
        if (string.CompareOrdinal(text, at, marker, 0, marker.Length) != 0 || at + marker.Length >= text.Length)
        {
            return false;
        }

        if (marker == "*" && text[at + 1] == '*')
        {
            return false;
        }

        end = text.IndexOf(marker, at + marker.Length, StringComparison.Ordinal);
        return end > at + marker.Length;
    }

    private static void Flush(InlineCollection inlines, System.Text.StringBuilder plain)
    {
        if (plain.Length > 0)
        {
            inlines.Add(new Run(plain.ToString()));
            plain.Clear();
        }
    }
}
