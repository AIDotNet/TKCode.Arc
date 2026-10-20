using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace TKCode.Arc;

/// <summary>
/// Arc's small, dependency free converters for the subset the rich text editor writes: Markdown to the editor's HTML
/// (three heading levels, nested lists, quotes, fenced code, rules and inline marks), the editor's HTML to Markdown,
/// HTML sanitizing, and link normalizing.
/// </summary>
public static partial class RichTextEditorMarkdown
{
    /// <summary>Converts Markdown to the editor's HTML: headings (h4–h6 become h3), lists, quotes, fenced code, rules, and inline marks.</summary>
    public static string ToHtml(string? markdown)
    {
        var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new StringBuilder();
        var paragraph = new List<string>();

        void Flush()
        {
            if (paragraph.Count > 0)
            {
                output.Append("<p>").Append(Inline(string.Join(" ", paragraph))).Append("</p>");
                paragraph.Clear();
            }
        }

        static bool IsItem(string line) => ItemRegex().IsMatch(line);

        for (var index = 0; index < lines.Length;)
        {
            var line = lines[index];
            if (FenceRegex().IsMatch(line))
            {
                Flush();
                var code = new List<string>();
                index++;
                while (index < lines.Length && !FenceRegex().IsMatch(lines[index]))
                {
                    code.Add(lines[index++]);
                }
                index++;
                var body = RichTextEditorHtml.Escape(string.Join("\n", code));
                output.Append("<pre>").Append(body.Length == 0 ? "<br>" : body).Append("</pre>");
                continue;
            }
            if (line.Trim().Length == 0)
            {
                Flush();
                index++;
                continue;
            }
            var heading = HeadingRegex().Match(line);
            if (heading.Success)
            {
                Flush();
                var level = Math.Min(3, heading.Groups[1].Length);
                output.Append("<h").Append(level).Append('>').Append(Inline(heading.Groups[2].Value)).Append("</h").Append(level).Append('>');
                index++;
                continue;
            }
            if (RuleRegex().IsMatch(line))
            {
                Flush();
                output.Append("<hr>");
                index++;
                continue;
            }
            if (QuoteRegex().IsMatch(line))
            {
                Flush();
                var quote = new List<string>();
                while (index < lines.Length && QuoteRegex().IsMatch(lines[index]))
                {
                    quote.Add(QuotePrefixRegex().Replace(lines[index++], string.Empty, 1));
                }
                output.Append("<blockquote>").Append(string.Join("<br>", quote.Select(Inline))).Append("</blockquote>");
                continue;
            }
            if (IsItem(line))
            {
                Flush();
                var items = new List<string>();
                while (index < lines.Length && IsItem(lines[index]))
                {
                    items.Add(lines[index++]);
                }
                output.Append(List(items));
                continue;
            }
            paragraph.Add(line.Trim());
            index++;
        }
        Flush();
        return output.Length == 0 ? "<p><br></p>" : output.ToString();
    }

    /// <summary>Converts editor HTML to Markdown (the HTML is sanitized first, so any HTML is accepted).</summary>
    public static string FromHtml(string? html) => Write(RichTextEditorHtml.ToDocument(html));

    /// <summary>Keeps only the tags the editor understands and drops every attribute except safe link targets. Bold and italic spans from other editors become marks.</summary>
    public static string SanitizeHtml(string? html) => RichTextEditorHtml.Write(RichTextEditorHtml.ToDocument(html));

    /// <summary>Adds a scheme to bare addresses: <c>example.com/docs</c> becomes <c>https://example.com/docs</c>, an email address becomes <c>mailto:</c>.</summary>
    public static string NormalizeUrl(string? input)
    {
        var url = (input ?? string.Empty).Trim();
        if (url.Length == 0)
        {
            return string.Empty;
        }
        if (SchemeRegex().IsMatch(url) || url.StartsWith('/') || url.StartsWith('#'))
        {
            return url;
        }
        if (EmailRegex().IsMatch(url))
        {
            return "mailto:" + url;
        }
        return "https://" + url;
    }

    /// <summary>Converts Markdown straight into editor blocks.</summary>
    internal static ImmutableList<RichTextEditorBlock> ToDocument(string? markdown) => RichTextEditorHtml.ToDocument(ToHtml(markdown));

    /// <summary>True when pasted plain text looks like Markdown (Arc's paste heuristic).</summary>
    internal static bool LooksLikeMarkdown(string text) => MarkdownishRegex().IsMatch(text);

    /* ----------------------------------------------------------------------------------------------------------
     * Markdown in
     * -------------------------------------------------------------------------------------------------------- */

    private static string Inline(string text)
    {
        var codes = new List<string>();
        var output = InlineCodeRegex().Replace(RichTextEditorHtml.Escape(text), match =>
        {
            codes.Add(match.Groups[1].Value);
            return "\u0000" + (codes.Count - 1) + "\u0000";
        });
        output = LinkRegex().Replace(output, match =>
        {
            var label = match.Groups[1].Value;
            var url = match.Groups[2].Value;
            return RichTextEditorHtml.IsSafeUrl(RichTextEditorHtml.DecodeEntities(url)) ? $"<a href=\"{url}\">{label}</a>" : label;
        });
        output = StrongRegex().Replace(output, "<strong>$2</strong>");
        output = StrikeRegex().Replace(output, "<s>$1</s>");
        output = EmStarRegex().Replace(output, "$1<em>$2</em>");
        output = EmUnderscoreRegex().Replace(output, "$1<em>$2</em>");
        return CodeSlotRegex().Replace(output, match => "<code>" + codes[int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)] + "</code>");
    }

    private static string List(List<string> lines)
    {
        var html = new StringBuilder();
        var stack = new List<(int Indent, string Tag)>();
        foreach (var line in lines)
        {
            var match = ItemPartsRegex().Match(line);
            var indent = match.Groups[1].Value.Replace("\t", "  ").Length;
            var tag = char.IsAsciiDigit(match.Groups[2].Value[0]) ? "ol" : "ul";
            var text = match.Groups[3].Value;
            while (stack.Count > 1 && indent < stack[^1].Indent)
            {
                html.Append("</li></").Append(stack[^1].Tag).Append('>');
                stack.RemoveAt(stack.Count - 1);
            }
            if (stack.Count == 0 || indent > stack[^1].Indent)
            {
                html.Append('<').Append(tag).Append("><li>");
                stack.Add((indent, tag));
            }
            else if (stack[^1].Tag != tag)
            {
                html.Append("</li></").Append(stack[^1].Tag).Append("><").Append(tag).Append("><li>");
                stack[^1] = (stack[^1].Indent, tag);
            }
            else
            {
                html.Append("</li><li>");
            }
            html.Append(Inline(text));
        }
        while (stack.Count > 0)
        {
            html.Append("</li></").Append(stack[^1].Tag).Append('>');
            stack.RemoveAt(stack.Count - 1);
        }
        return html.ToString();
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Markdown out
     * -------------------------------------------------------------------------------------------------------- */

    /// <summary>Markdown for <paramref name="blocks"/>: blocks separated by a blank line, a list as one block.</summary>
    internal static string Write(IReadOnlyList<RichTextEditorBlock> blocks)
    {
        var count = RichTextEditorHtml.OutputCount(blocks);
        var parts = new List<string>();
        var i = 0;
        while (i < count)
        {
            var block = blocks[i];
            if (block.IsList)
            {
                var end = RichTextEditorHtml.ListGroupEnd(blocks, i, count);
                parts.Add(WriteList(blocks, i, end));
                i = end;
                continue;
            }
            var part = block.Type switch
            {
                RichTextBlockType.Heading1 => "# " + InlineMarkdown(block).Trim(),
                RichTextBlockType.Heading2 => "## " + InlineMarkdown(block).Trim(),
                RichTextBlockType.Heading3 => "### " + InlineMarkdown(block).Trim(),
                RichTextBlockType.Divider => "---",
                RichTextBlockType.CodeBlock => "```\n" + block.Text + "\n```",
                RichTextBlockType.Quote => Quote(InlineMarkdown(block).Trim()),
                _ => InlineMarkdown(block).Trim()
            };
            if (part.Length > 0)
            {
                parts.Add(part);
            }
            i++;
        }
        return string.Join("\n\n", parts);
    }

    private static string Quote(string body) =>
        string.Join("\n", body.Split('\n').Select(line => line.Length > 0 ? "> " + line : ">"));

    private static string WriteList(IReadOnlyList<RichTextEditorBlock> blocks, int from, int to)
    {
        var lines = new List<string>();
        var stack = new List<ListFrame>();
        for (var i = from; i < to; i++)
        {
            var item = blocks[i];
            while (stack.Count > 0 && stack[^1].Level > item.Level)
            {
                stack.RemoveAt(stack.Count - 1);
            }
            if (stack.Count > 0 && stack[^1].Level == item.Level && stack[^1].Type != item.Type)
            {
                var replaced = stack[^1];
                stack[^1] = new ListFrame(item.Level, item.Type, replaced.Indent);
            }
            if (stack.Count == 0 || stack[^1].Level < item.Level)
            {
                var indent = stack.Count == 0 ? string.Empty : stack[^1].Indent + new string(' ', stack[^1].MarkerLength);
                stack.Add(new ListFrame(item.Level, item.Type, indent));
            }
            var frame = stack[^1];
            var marker = item.Type == RichTextBlockType.NumberedList ? $"{frame.Next++}. " : "- ";
            frame.MarkerLength = marker.Length;
            lines.Add(frame.Indent + marker + InlineMarkdown(item).Trim());
        }
        return string.Join("\n", lines);
    }

    private sealed class ListFrame(int level, RichTextBlockType type, string indent)
    {
        public int Level { get; } = level;
        public RichTextBlockType Type { get; } = type;
        public string Indent { get; } = indent;
        public int Next { get; set; } = 1;
        public int MarkerLength { get; set; } = 2;
    }

    internal static string InlineMarkdown(RichTextEditorBlock block)
    {
        if (block.IsCode)
        {
            return block.Text;
        }
        var sb = new StringBuilder();
        InlineMarkdown(block, 0, block.SpanCount, 0, sb);
        return sb.ToString();
    }

    private static void InlineMarkdown(RichTextEditorBlock block, int from, int to, int level, StringBuilder sb)
    {
        var spans = block.Spans;
        if (level == 5)
        {
            for (var i = from; i < to; i++)
            {
                sb.Append(block.Text, spans[i].Start, spans[i].Length);
            }
            return;
        }
        var index = from;
        while (index < to)
        {
            var key = RichTextEditorHtml.GroupKey(spans[index], level);
            var end = index + 1;
            while (end < to && Equals(RichTextEditorHtml.GroupKey(spans[end], level), key))
            {
                end++;
            }
            if (key is null)
            {
                InlineMarkdown(block, index, end, level + 1, sb);
            }
            else if (level == 4)
            {
                sb.Append('`');
                for (var i = index; i < end; i++)
                {
                    sb.Append(block.Text, spans[i].Start, spans[i].Length);
                }
                sb.Append('`');
            }
            else
            {
                var inner = new StringBuilder();
                InlineMarkdown(block, index, end, level + 1, inner);
                switch (level)
                {
                    case 0:
                        sb.Append('[').Append(inner).Append("](").Append((string)key).Append(')');
                        break;
                    case 1:
                        sb.Append(WrapMark(inner.ToString(), "**"));
                        break;
                    case 2:
                        sb.Append(WrapMark(inner.ToString(), "_"));
                        break;
                    default:
                        sb.Append(WrapMark(inner.ToString(), "~~"));
                        break;
                }
            }
            index = end;
        }
    }

    /// <summary>Wraps the text in a mark, keeping surrounding whitespace outside it (<c>**bold** </c>, not <c>**bold **</c>).</summary>
    private static string WrapMark(string inner, string mark)
    {
        var start = 0;
        while (start < inner.Length && char.IsWhiteSpace(inner[start]))
        {
            start++;
        }
        var end = inner.Length;
        while (end > start && char.IsWhiteSpace(inner[end - 1]))
        {
            end--;
        }
        if (end == start)
        {
            return inner;
        }
        return inner[..start] + mark + inner[start..end] + mark + inner[end..];
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Patterns (Arc's regular expressions; \w and \d spelled out with JavaScript's ASCII meaning)
     * -------------------------------------------------------------------------------------------------------- */

    [GeneratedRegex(@"^\s*```")]
    private static partial Regex FenceRegex();

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^ {0,3}([-*_])( *\1){2,} *$")]
    private static partial Regex RuleRegex();

    [GeneratedRegex(@"^\s*>")]
    private static partial Regex QuoteRegex();

    [GeneratedRegex(@"^\s*>\s?")]
    private static partial Regex QuotePrefixRegex();

    [GeneratedRegex(@"^\s*([-*+]|[0-9]+[.)])\s+")]
    private static partial Regex ItemRegex();

    [GeneratedRegex(@"^(\s*)([-*+]|[0-9]+[.)])\s+(.*)$")]
    private static partial Regex ItemPartsRegex();

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)\s]+)\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"(\*\*|__)(?=\S)([\s\S]*?\S)\1")]
    private static partial Regex StrongRegex();

    [GeneratedRegex(@"~~(?=\S)([\s\S]*?\S)~~")]
    private static partial Regex StrikeRegex();

    [GeneratedRegex(@"(^|[^A-Za-z0-9_*])\*(?=\S)([^*]*?\S)\*(?!\*)")]
    private static partial Regex EmStarRegex();

    [GeneratedRegex(@"(^|[^A-Za-z0-9_])_(?=\S)([^_]*?\S)_(?![A-Za-z0-9_])")]
    private static partial Regex EmUnderscoreRegex();

    [GeneratedRegex("\u0000([0-9]+)\u0000")]
    private static partial Regex CodeSlotRegex();

    [GeneratedRegex(@"^[a-z][a-z0-9+.-]*:", RegexOptions.IgnoreCase)]
    private static partial Regex SchemeRegex();

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(^|\n)\s*(#{1,3}\s|[-*+]\s|[0-9]+[.)]\s|>|```)|\*\*|`[^`]+`|\[[^\]]+\]\(")]
    private static partial Regex MarkdownishRegex();
}
