// The documentation of a completion as blocks of text for the suggest widget's details pane. Upstream renders the
// Markdown with its markdown renderer (suggestWidgetDetails.ts, commit b57d5c9e, MIT license); the details pane here
// shows text: paragraphs, and fenced or indented code blocks in the editor's font, with headings, emphasis, inline code
// marks, links (their text stays) and Markdown escapes reduced to their text.

using System.Text;
using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Languages;

namespace TKCode.Arc.Editor.Contrib.Suggest;

/// <summary>A paragraph or a code block of documentation.</summary>
internal sealed record SuggestDocumentationBlock(string Text, bool IsCode);

/// <summary>Turns a completion's documentation into blocks of text.</summary>
internal static partial class SuggestDocumentation
{
    public static IReadOnlyList<SuggestDocumentationBlock> ToBlocks(StringOrMarkdown documentation)
    {
        if (!documentation.IsMarkdown)
        {
            var text = (documentation.Text ?? string.Empty).Trim();
            return text.Length == 0 ? [] : [new SuggestDocumentationBlock(text, false)];
        }

        var blocks = new List<SuggestDocumentationBlock>();
        var paragraph = new StringBuilder();
        var lines = documentation.Value.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        void FlushParagraph()
        {
            var text = paragraph.ToString().Trim();
            paragraph.Clear();
            if (text.Length > 0)
            {
                blocks.Add(new SuggestDocumentationBlock(Inline(text), false));
            }
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                FlushParagraph();
                var fence = trimmed[..3];
                var code = new StringBuilder();
                for (i++; i < lines.Length && !lines[i].TrimStart().StartsWith(fence, StringComparison.Ordinal); i++)
                {
                    code.Append(lines[i]).Append('\n');
                }
                var text = code.ToString().TrimEnd('\n');
                if (text.Length > 0)
                {
                    blocks.Add(new SuggestDocumentationBlock(text, true));
                }
                continue;
            }

            if (line.Length == 0 || trimmed.Length == 0)
            {
                FlushParagraph();
                continue;
            }

            if (line.StartsWith("    ", StringComparison.Ordinal) && paragraph.Length == 0)
            {
                // an indented code block
                var code = new StringBuilder();
                for (; i < lines.Length && (lines[i].StartsWith("    ", StringComparison.Ordinal) || lines[i].Trim().Length == 0); i++)
                {
                    code.Append(lines[i].Length >= 4 ? lines[i][4..] : string.Empty).Append('\n');
                }
                i--;
                blocks.Add(new SuggestDocumentationBlock(code.ToString().TrimEnd('\n'), true));
                continue;
            }

            if (HeadingRegex().Match(trimmed) is { Success: true } heading)
            {
                FlushParagraph();
                blocks.Add(new SuggestDocumentationBlock(Inline(heading.Groups[1].Value), false));
                continue;
            }

            if (ListItemRegex().Match(trimmed) is { Success: true } listItem)
            {
                FlushParagraph();
                paragraph.Append("• ").Append(listItem.Groups[1].Value);
                continue;
            }

            if (paragraph.Length > 0)
            {
                paragraph.Append(line.EndsWith("  ", StringComparison.Ordinal) ? '\n' : ' ');
            }
            paragraph.Append(trimmed.TrimEnd());
        }

        FlushParagraph();
        return blocks;
    }

    // inline marks: links keep their text, code spans and emphasis lose their marks, escapes their backslash
    private static string Inline(string text)
    {
        text = LinkRegex().Replace(text, "$1");
        text = CodeSpanRegex().Replace(text, "$1");
        text = StrongRegex().Replace(text, "$2");
        text = EmphasisRegex().Replace(text, "$2");
        text = EscapeRegex().Replace(text, "$1");
        return text;
    }

    [GeneratedRegex(@"^#{1,6}\s+(.*?)\s*#*$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^(?:[-*+]|\d+[.)])\s+(.*)$")]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(@"!?\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"`+([^`]+)`+")]
    private static partial Regex CodeSpanRegex();

    [GeneratedRegex(@"(\*\*|__)(.+?)\1")]
    private static partial Regex StrongRegex();

    [GeneratedRegex(@"(?<![\w*])(\*|_)(?!\s)(.+?)(?<!\s)\1(?![\w*])")]
    private static partial Regex EmphasisRegex();

    [GeneratedRegex(@"\\([\\`*_{}\[\]()#+\-.!|<>])")]
    private static partial Regex EscapeRegex();
}
