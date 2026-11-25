// The Markdown of hovers and parameter hints. Upstream renders IMarkdownString with marked
// (src/vs/base/browser/markdownRenderer.ts) into sanitized HTML; there is no browser here, so this is a small parser of
// the subset hovers use, written for this port: paragraphs (soft and hard line breaks), ATX and setext headings,
// fenced and indented code blocks (the language of the fence colours it, with the editor's tokenizer, in the
// controls), block quotes, bullet and ordered lists, thematic breaks, and inline bold, italic, strikethrough, code
// spans, links, autolinks, backslash escapes and character references. HTML is not interpreted (a <br> is a line
// break; other tags stay text). Also ported (src/vs/base/common/htmlContent.ts, commit b57d5c9e, MIT license):
// escapeMarkdownSyntaxTokens, isEmptyMarkdownString and MarkdownString.appendText's escaping.

using System.Globalization;
using System.Text;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Hover;

/// <summary>A block of a Markdown document.</summary>
internal abstract record MarkdownBlock;

/// <summary>A paragraph.</summary>
internal sealed record MarkdownParagraph(IReadOnlyList<MarkdownRun> Runs) : MarkdownBlock;

/// <summary>A heading (level 1 to 6).</summary>
internal sealed record MarkdownHeading(int Level, IReadOnlyList<MarkdownRun> Runs) : MarkdownBlock;

/// <summary>A code block; <see cref="Language"/> is the first word of a fence's info string, or null.</summary>
internal sealed record MarkdownCodeBlock(string? Language, string Code) : MarkdownBlock;

/// <summary>A block quote.</summary>
internal sealed record MarkdownQuote(IReadOnlyList<MarkdownBlock> Blocks) : MarkdownBlock;

/// <summary>A list; each item is a list of blocks.</summary>
internal sealed record MarkdownList(bool Ordered, int Start, IReadOnlyList<IReadOnlyList<MarkdownBlock>> Items) : MarkdownBlock;

/// <summary>A thematic break (<c>---</c>).</summary>
internal sealed record MarkdownRule : MarkdownBlock;

/// <summary>The style of a text run.</summary>
[Flags]
internal enum MarkdownRunStyle
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Strikethrough = 4,
}

/// <summary>What a run is.</summary>
internal enum MarkdownRunKind
{
    /// <summary>Text.</summary>
    Text,

    /// <summary>A code span.</summary>
    Code,

    /// <summary>A hard line break.</summary>
    LineBreak,
}

/// <summary>A piece of inline text with one style; <see cref="Href"/> is the target when it is (part of) a link.</summary>
internal sealed record MarkdownRun(string Text, MarkdownRunKind Kind = MarkdownRunKind.Text, MarkdownRunStyle Style = MarkdownRunStyle.None)
{
    public string? Href { get; init; }

    public string? Title { get; init; }
}

/// <summary>Parses the Markdown of hovers into blocks and runs, and the helpers around IMarkdownString.</summary>
internal static partial class HoverMarkdown
{
    /// <summary>Upstream's escapeMarkdownSyntaxTokens.</summary>
    public static string EscapeMarkdownSyntaxTokens(string text)
    {
        var sb = new StringBuilder(text.Length + 8);
        var lineStart = true;
        foreach (var ch in text)
        {
            if (ch is '\\' or '`' or '*' or '_' or '{' or '}' or '[' or ']' or '(' or ')' or '#' or '+' or '!' or '~')
            {
                sb.Append('\\');
            }
            else if (ch == '-' && lineStart)
            {
                sb.Append('\\');
            }

            sb.Append(ch);
            if (ch is '\n' or '\r')
            {
                lineStart = true;
            }
            else if (ch is not (' ' or '\t'))
            {
                lineStart = false;
            }
        }

        return sb.ToString();
    }

    /// <summary>Upstream's MarkdownString.appendText: plain text as Markdown that renders as the text (newlines start paragraphs).</summary>
    public static string AppendText(string text)
    {
        var escaped = EscapeMarkdownSyntaxTokens(text);
        var sb = new StringBuilder(escaped.Length);
        foreach (var ch in escaped)
        {
            switch (ch)
            {
                case ' ':
                case '\t':
                    sb.Append("&nbsp;");
                    break;
                case '>':
                    sb.Append("\\>");
                    break;
                case '\n':
                    sb.Append("\n\n");
                    break;
                default:
                    sb.Append(ch);
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>Upstream's isEmptyMarkdownString for one string.</summary>
    public static bool IsEmptyMarkdownString(MarkdownString? markdown) => markdown is null || string.IsNullOrEmpty(markdown.Value);

    /// <summary>Upstream's isEmptyMarkdownString for an array: every one is empty.</summary>
    public static bool IsEmptyMarkdownString(IReadOnlyList<MarkdownString>? markdown) =>
        markdown is null || markdown.All(IsEmptyMarkdownString);

    /// <summary>
    /// The text a reader sees (upstream: the rendered element's innerText): blocks on their own lines, no-break spaces
    /// (appendText's <c>&amp;nbsp;</c>) as spaces.
    /// </summary>
    public static string ToPlainText(string markdown) => ToPlainText(Parse(markdown)).Replace('\u00A0', ' ');

    /// <summary>The text of parsed blocks.</summary>
    public static string ToPlainText(IReadOnlyList<MarkdownBlock> blocks)
    {
        var sb = new StringBuilder();
        AppendPlainText(sb, blocks);
        return sb.ToString().TrimEnd('\n');
    }

    /// <summary>The text of runs.</summary>
    public static string ToPlainText(IReadOnlyList<MarkdownRun> runs)
    {
        var sb = new StringBuilder();
        foreach (var run in runs)
        {
            sb.Append(run.Kind == MarkdownRunKind.LineBreak ? "\n" : run.Text);
        }

        return sb.ToString();
    }

    private static void AppendPlainText(StringBuilder sb, IReadOnlyList<MarkdownBlock> blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case MarkdownParagraph p:
                    sb.Append(ToPlainText(p.Runs)).Append('\n');
                    break;
                case MarkdownHeading h:
                    sb.Append(ToPlainText(h.Runs)).Append('\n');
                    break;
                case MarkdownCodeBlock c:
                    sb.Append(c.Code).Append('\n');
                    break;
                case MarkdownQuote q:
                    AppendPlainText(sb, q.Blocks);
                    break;
                case MarkdownList l:
                    for (var i = 0; i < l.Items.Count; i++)
                    {
                        sb.Append(l.Ordered ? (l.Start + i).ToString(CultureInfo.InvariantCulture) + ". " : "• ");
                        AppendPlainText(sb, l.Items[i]);
                    }

                    break;
                case MarkdownRule:
                    break;
            }
        }
    }

    /// <summary>Parses Markdown into blocks.</summary>
    public static IReadOnlyList<MarkdownBlock> Parse(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return ParseBlocks(lines);
    }

    private static List<MarkdownBlock> ParseBlocks(IReadOnlyList<string> lines)
    {
        var blocks = new List<MarkdownBlock>();
        var paragraph = new List<string>();

        void FlushParagraph()
        {
            if (paragraph.Count > 0)
            {
                blocks.Add(new MarkdownParagraph(ParseInlines(string.Join("\n", paragraph).Trim())));
                paragraph.Clear();
            }
        }

        var i = 0;
        while (i < lines.Count)
        {
            var line = ExpandTabs(lines[i]);
            var indent = CountIndent(line);
            var content = line.TrimStart(' ');

            if (content.Length == 0)
            {
                FlushParagraph();
                i++;
                continue;
            }

            // indented code (not inside a paragraph)
            if (indent >= 4 && paragraph.Count == 0)
            {
                var code = new List<string>();
                while (i < lines.Count)
                {
                    var l = ExpandTabs(lines[i]);
                    if (l.Trim().Length == 0)
                    {
                        code.Add(string.Empty);
                    }
                    else if (CountIndent(l) >= 4)
                    {
                        code.Add(l[4..]);
                    }
                    else
                    {
                        break;
                    }

                    i++;
                }

                while (code.Count > 0 && code[^1].Length == 0)
                {
                    code.RemoveAt(code.Count - 1);
                }

                blocks.Add(new MarkdownCodeBlock(null, string.Join("\n", code)));
                continue;
            }

            if (indent < 4 && TryFence(content, out var fenceChar, out var fenceLength, out var info))
            {
                FlushParagraph();
                var code = new List<string>();
                i++;
                while (i < lines.Count)
                {
                    var l = lines[i];
                    var lc = l.TrimStart(' ');
                    if (CountIndent(l) < 4 && TryFence(lc, out var closeChar, out var closeLength, out var closeInfo)
                        && closeChar == fenceChar && closeLength >= fenceLength && closeInfo.Length == 0)
                    {
                        i++;
                        break;
                    }

                    // the fence's indentation is removed from the content lines
                    code.Add(RemoveIndent(l, indent));
                    i++;
                }

                var language = info.Length == 0 ? null : info.Split(' ', '\t')[0];
                blocks.Add(new MarkdownCodeBlock(language, string.Join("\n", code)));
                continue;
            }

            if (indent < 4 && IsThematicBreak(content))
            {
                // a --- line under a paragraph is a setext heading
                if (paragraph.Count > 0 && content.Trim().All(c => c == '-'))
                {
                    blocks.Add(new MarkdownHeading(2, ParseInlines(string.Join("\n", paragraph).Trim())));
                    paragraph.Clear();
                }
                else
                {
                    FlushParagraph();
                    blocks.Add(new MarkdownRule());
                }

                i++;
                continue;
            }

            if (indent < 4 && paragraph.Count > 0 && content.TrimEnd().Length > 0 && content.TrimEnd().All(c => c == '='))
            {
                blocks.Add(new MarkdownHeading(1, ParseInlines(string.Join("\n", paragraph).Trim())));
                paragraph.Clear();
                i++;
                continue;
            }

            if (indent < 4 && TryAtxHeading(content, out var level, out var headingText))
            {
                FlushParagraph();
                blocks.Add(new MarkdownHeading(level, ParseInlines(headingText)));
                i++;
                continue;
            }

            if (indent < 4 && content.StartsWith('>'))
            {
                FlushParagraph();
                var quoted = new List<string>();
                while (i < lines.Count)
                {
                    var lc = ExpandTabs(lines[i]).TrimStart(' ');
                    if (lc.StartsWith('>'))
                    {
                        lc = lc[1..];
                        quoted.Add(lc.StartsWith(' ') ? lc[1..] : lc);
                    }
                    else if (lc.Length > 0 && quoted.Count > 0 && quoted[^1].Trim().Length > 0)
                    {
                        // lazy continuation of the quoted paragraph
                        quoted.Add(lc);
                    }
                    else
                    {
                        break;
                    }

                    i++;
                }

                blocks.Add(new MarkdownQuote(ParseBlocks(quoted)));
                continue;
            }

            if (indent < 4 && TryListMarker(content, out var ordered, out var start, out _, out var bullet)
                && (paragraph.Count == 0 || !ordered || start == 1))
            {
                FlushParagraph();
                i = ParseList(lines, i, ordered, start, bullet, blocks);
                continue;
            }

            paragraph.Add(line);
            i++;
        }

        FlushParagraph();
        return blocks;
    }

    // a list: items start with the same kind of marker; an item's other lines are indented to its content, or are lazy
    // continuations of its paragraph
    private static int ParseList(IReadOnlyList<string> lines, int i, bool ordered, int start, char bullet, List<MarkdownBlock> blocks)
    {
        var items = new List<IReadOnlyList<MarkdownBlock>>();
        while (i < lines.Count)
        {
            var line = ExpandTabs(lines[i]);
            var indent = CountIndent(line);
            var content = line.TrimStart(' ');
            if (indent >= 4 || !TryListMarker(content, out var itemOrdered, out _, out var markerWidth, out var itemBullet)
                || itemOrdered != ordered || (!ordered && itemBullet != bullet))
            {
                break;
            }

            var contentIndent = indent + markerWidth;
            var itemLines = new List<string> { content[markerWidth..] };
            i++;
            var lastBlank = false;
            while (i < lines.Count)
            {
                var l = ExpandTabs(lines[i]);
                if (l.Trim().Length == 0)
                {
                    itemLines.Add(string.Empty);
                    lastBlank = true;
                    i++;
                    continue;
                }

                var li = CountIndent(l);
                if (li >= contentIndent)
                {
                    itemLines.Add(l[contentIndent..]);
                    lastBlank = false;
                    i++;
                    continue;
                }

                var lc = l.TrimStart(' ');
                if (!lastBlank && li < 4 && !TryListMarker(lc, out _, out _, out _, out _) && !lc.StartsWith('>')
                    && !TryFence(lc, out _, out _, out _) && !IsThematicBreak(lc) && !TryAtxHeading(lc, out _, out _))
                {
                    // lazy continuation
                    itemLines.Add(lc);
                    i++;
                    continue;
                }

                break;
            }

            while (itemLines.Count > 0 && itemLines[^1].Length == 0)
            {
                itemLines.RemoveAt(itemLines.Count - 1);
            }

            items.Add(ParseBlocks(itemLines));

            // a blank line followed by something that is not a list item ends the list
            if (lastBlank && i < lines.Count && !TryListMarker(ExpandTabs(lines[i]).TrimStart(' '), out _, out _, out _, out _))
            {
                break;
            }
        }

        blocks.Add(new MarkdownList(ordered, start, items));
        return i;
    }

    private static bool TryFence(string content, out char fenceChar, out int length, out string info)
    {
        fenceChar = '\0';
        length = 0;
        info = string.Empty;
        if (content.Length < 3 || (content[0] != '`' && content[0] != '~'))
        {
            return false;
        }

        var ch = content[0];
        var n = 0;
        while (n < content.Length && content[n] == ch)
        {
            n++;
        }

        if (n < 3)
        {
            return false;
        }

        var rest = content[n..].Trim();
        if (ch == '`' && rest.Contains('`'))
        {
            return false;
        }

        fenceChar = ch;
        length = n;
        info = rest;
        return true;
    }

    private static bool IsThematicBreak(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.Length < 3)
        {
            return false;
        }

        var ch = trimmed[0];
        if (ch is not ('-' or '*' or '_'))
        {
            return false;
        }

        var count = 0;
        foreach (var c in trimmed)
        {
            if (c == ch)
            {
                count++;
            }
            else if (c is not (' ' or '\t'))
            {
                return false;
            }
        }

        return count >= 3;
    }

    private static bool TryAtxHeading(string content, out int level, out string text)
    {
        level = 0;
        text = string.Empty;
        while (level < content.Length && content[level] == '#')
        {
            level++;
        }

        if (level is 0 or > 6 || (level < content.Length && content[level] is not (' ' or '\t')))
        {
            level = 0;
            return false;
        }

        text = content[level..].Trim();

        // a closing sequence of #s preceded by a space goes
        var end = text.Length;
        while (end > 0 && text[end - 1] == '#')
        {
            end--;
        }

        if (end == 0)
        {
            text = string.Empty;
        }
        else if (end < text.Length && text[end - 1] == ' ')
        {
            text = text[..end].TrimEnd();
        }

        return true;
    }

    private static bool TryListMarker(string content, out bool ordered, out int start, out int width, out char bullet)
    {
        ordered = false;
        start = 1;
        width = 0;
        bullet = '\0';
        if (content.Length >= 1 && content[0] is '-' or '*' or '+')
        {
            if (content.Length == 1 || content[1] is ' ' or '\t')
            {
                bullet = content[0];
                width = content.Length == 1 ? 1 : 2;
                return true;
            }

            return false;
        }

        var n = 0;
        while (n < content.Length && n < 9 && char.IsAsciiDigit(content[n]))
        {
            n++;
        }

        if (n > 0 && n < content.Length && content[n] is '.' or ')' && (n + 1 == content.Length || content[n + 1] == ' '))
        {
            ordered = true;
            start = int.Parse(content.AsSpan(0, n), CultureInfo.InvariantCulture);
            bullet = content[n];
            width = n + 1 == content.Length ? n + 1 : n + 2;
            return true;
        }

        return false;
    }

    private static int CountIndent(string line)
    {
        var n = 0;
        while (n < line.Length && line[n] == ' ')
        {
            n++;
        }

        return n;
    }

    private static string RemoveIndent(string line, int indent)
    {
        var n = 0;
        while (n < indent && n < line.Length && line[n] == ' ')
        {
            n++;
        }

        return line[n..];
    }

    private static string ExpandTabs(string line)
    {
        if (!line.Contains('\t'))
        {
            return line;
        }

        var sb = new StringBuilder(line.Length + 8);
        foreach (var ch in line)
        {
            if (ch == '\t')
            {
                var spaces = 4 - (sb.Length % 4);
                sb.Append(' ', spaces);
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }
}
