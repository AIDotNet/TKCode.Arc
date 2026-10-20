using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>A node of the small HTML tree the editor parses pasted or assigned HTML into.</summary>
internal sealed class RichTextEditorHtmlNode
{
    public RichTextEditorHtmlNode(string? tag, string text = "")
    {
        Tag = tag;
        Text = text;
    }

    /// <summary>Lower-case tag name, or null for a text node.</summary>
    public string? Tag { get; }

    public string Text { get; }

    public string? Href { get; set; }

    public string? Style { get; set; }

    public RichTextEditorHtmlNode? Parent { get; set; }

    public List<RichTextEditorHtmlNode> Children { get; } = new();

    public void Add(RichTextEditorHtmlNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }
}

/// <summary>
/// HTML in and out for the rich text editor. Input goes through a tolerant tokenizer and Arc's sanitizing rules
/// (only the tags the editor understands, safe link targets, bold and italic spans from other editors become marks);
/// output is Arc's clean HTML: canonical tags, no attributes but <c>href</c>, no trailing empty lines.
/// </summary>
internal static class RichTextEditorHtml
{
    private const char NewlineMark = '\u0001';

    private static readonly HashSet<string> s_void = new(StringComparer.Ordinal)
    {
        "br", "hr", "img", "input", "meta", "link", "area", "base", "col", "embed", "source", "track", "wbr", "param"
    };

    private static readonly HashSet<string> s_rawText = new(StringComparer.Ordinal) { "script", "style", "textarea", "title", "xmp" };

    private static readonly HashSet<string> s_closesParagraph = new(StringComparer.Ordinal)
    {
        "address", "article", "aside", "blockquote", "div", "dl", "fieldset", "figure", "footer", "form", "h1", "h2", "h3",
        "h4", "h5", "h6", "header", "hr", "li", "main", "nav", "ol", "p", "pre", "section", "table", "ul"
    };

    // Arc's DROP set, plus noscript.
    private static readonly HashSet<string> s_drop = new(StringComparer.Ordinal)
    {
        "script", "style", "meta", "link", "title", "template", "iframe", "object", "embed", "svg", "canvas", "img", "video",
        "audio", "input", "button", "select", "textarea", "head", "noscript"
    };

    // Containers Arc unwraps; here they also end the inline run so pasted lines do not run together.
    private static readonly HashSet<string> s_boundary = new(StringComparer.Ordinal)
    {
        "div", "section", "article", "header", "footer", "main", "aside", "nav", "figure", "figcaption", "address", "center",
        "details", "summary", "dl", "dt", "dd", "table", "thead", "tbody", "tfoot", "tr", "caption", "form", "fieldset"
    };

    private static readonly Dictionary<string, string> s_entities = new(StringComparer.Ordinal)
    {
        ["amp"] = "&", ["lt"] = "<", ["gt"] = ">", ["quot"] = "\"", ["apos"] = "'", ["nbsp"] = " ", ["copy"] = "©",
        ["reg"] = "®", ["trade"] = "™", ["hellip"] = "…", ["mdash"] = "—", ["ndash"] = "–", ["lsquo"] = "‘", ["rsquo"] = "’",
        ["ldquo"] = "“", ["rdquo"] = "”", ["bull"] = "•", ["middot"] = "·", ["times"] = "×", ["laquo"] = "«", ["raquo"] = "»",
        ["euro"] = "€", ["pound"] = "£", ["yen"] = "¥", ["cent"] = "¢", ["sect"] = "§", ["deg"] = "°", ["plusmn"] = "±",
        ["zwsp"] = "​", ["shy"] = "­"
    };

    /* ----------------------------------------------------------------------------------------------------------
     * Parsing
     * -------------------------------------------------------------------------------------------------------- */

    public static RichTextEditorHtmlNode Parse(string html)
    {
        var root = new RichTextEditorHtmlNode("#root");
        var stack = new List<RichTextEditorHtmlNode> { root };
        var i = 0;
        var n = html.Length;
        var text = new StringBuilder();

        void FlushText()
        {
            if (text.Length > 0)
            {
                stack[^1].Add(new RichTextEditorHtmlNode(null, DecodeEntities(text.ToString())));
                text.Clear();
            }
        }

        while (i < n)
        {
            var c = html[i];
            if (c != '<' || i + 1 >= n)
            {
                text.Append(c);
                i++;
                continue;
            }
            var next = html[i + 1];
            if (next == '!' && string.CompareOrdinal(html, i, "<!--", 0, 4) == 0)
            {
                FlushText();
                var close = html.IndexOf("-->", i + 4, StringComparison.Ordinal);
                i = close < 0 ? n : close + 3;
                continue;
            }
            if (next is '!' or '?')
            {
                FlushText();
                var close = html.IndexOf('>', i + 2);
                i = close < 0 ? n : close + 1;
                continue;
            }
            if (next == '/')
            {
                var nameStart = i + 2;
                var nameEnd = nameStart;
                while (nameEnd < n && IsNameChar(html[nameEnd]))
                {
                    nameEnd++;
                }
                if (nameEnd == nameStart)
                {
                    text.Append(c);
                    i++;
                    continue;
                }
                FlushText();
                var name = html.Substring(nameStart, nameEnd - nameStart).ToLowerInvariant();
                var close = html.IndexOf('>', nameEnd);
                i = close < 0 ? n : close + 1;
                CloseElement(stack, name);
                continue;
            }
            if (!char.IsAsciiLetter(next))
            {
                text.Append(c);
                i++;
                continue;
            }
            FlushText();
            var start = i + 1;
            var end = start;
            while (end < n && IsNameChar(html[end]))
            {
                end++;
            }
            var tag = html.Substring(start, end - start).ToLowerInvariant();
            i = ParseAttributes(html, end, out var href, out var style, out var selfClosing);
            OpenElement(stack, tag, href, style, selfClosing);
            if (s_rawText.Contains(tag) && !selfClosing)
            {
                var closeAt = html.IndexOf("</" + tag, i, StringComparison.OrdinalIgnoreCase);
                var raw = closeAt < 0 ? html[i..] : html[i..closeAt];
                if (raw.Length > 0)
                {
                    stack[^1].Add(new RichTextEditorHtmlNode(null, DecodeEntities(raw)));
                }
                i = closeAt < 0 ? n : closeAt;
            }
        }
        FlushText();
        return root;
    }

    private static bool IsNameChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or ':' or '_';

    private static int ParseAttributes(string html, int i, out string? href, out string? style, out bool selfClosing)
    {
        href = null;
        style = null;
        selfClosing = false;
        var n = html.Length;
        while (i < n)
        {
            var c = html[i];
            if (c == '>')
            {
                return i + 1;
            }
            if (c == '/' && i + 1 < n && html[i + 1] == '>')
            {
                selfClosing = true;
                return i + 2;
            }
            if (char.IsWhiteSpace(c) || c == '/')
            {
                i++;
                continue;
            }
            var nameStart = i;
            while (i < n && !char.IsWhiteSpace(html[i]) && html[i] is not ('=' or '>' or '/'))
            {
                i++;
            }
            var name = html.Substring(nameStart, i - nameStart).ToLowerInvariant();
            while (i < n && char.IsWhiteSpace(html[i]))
            {
                i++;
            }
            string? value = null;
            if (i < n && html[i] == '=')
            {
                i++;
                while (i < n && char.IsWhiteSpace(html[i]))
                {
                    i++;
                }
                if (i < n && html[i] is '"' or '\'')
                {
                    var quote = html[i];
                    var close = html.IndexOf(quote, i + 1);
                    value = close < 0 ? html[(i + 1)..] : html.Substring(i + 1, close - i - 1);
                    i = close < 0 ? n : close + 1;
                }
                else
                {
                    var valueStart = i;
                    while (i < n && !char.IsWhiteSpace(html[i]) && html[i] != '>')
                    {
                        i++;
                    }
                    value = html.Substring(valueStart, i - valueStart);
                }
                value = DecodeEntities(value);
            }
            if (name == "href")
            {
                href = value ?? string.Empty;
            }
            else if (name == "style")
            {
                style = value;
            }
        }
        return n;
    }

    private static void OpenElement(List<RichTextEditorHtmlNode> stack, string tag, string? href, string? style, bool selfClosing)
    {
        if (s_closesParagraph.Contains(tag))
        {
            CloseOpen(stack, "p", stopAt: null);
        }
        if (tag == "li")
        {
            CloseOpen(stack, "li", stopAt: new[] { "ul", "ol" });
        }
        if (tag is "h1" or "h2" or "h3" or "h4" or "h5" or "h6" && stack[^1].Tag is "h1" or "h2" or "h3" or "h4" or "h5" or "h6")
        {
            stack.RemoveAt(stack.Count - 1);
        }
        var node = new RichTextEditorHtmlNode(tag) { Href = href, Style = style };
        stack[^1].Add(node);
        if (!selfClosing && !s_void.Contains(tag))
        {
            stack.Add(node);
        }
    }

    private static void CloseOpen(List<RichTextEditorHtmlNode> stack, string tag, string[]? stopAt)
    {
        for (var i = stack.Count - 1; i > 0; i--)
        {
            var open = stack[i].Tag;
            if (open == tag)
            {
                stack.RemoveRange(i, stack.Count - i);
                return;
            }
            if (stopAt is not null && Array.IndexOf(stopAt, open) >= 0)
            {
                return;
            }
        }
    }

    private static void CloseElement(List<RichTextEditorHtmlNode> stack, string tag)
    {
        if (tag == "br")
        {
            stack[^1].Add(new RichTextEditorHtmlNode("br"));
            return;
        }
        for (var i = stack.Count - 1; i > 0; i--)
        {
            if (stack[i].Tag == tag)
            {
                stack.RemoveRange(i, stack.Count - i);
                return;
            }
        }
    }

    public static string DecodeEntities(string text)
    {
        if (text.IndexOf('&') < 0)
        {
            return text;
        }
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c != '&')
            {
                sb.Append(c);
                continue;
            }
            var semi = text.IndexOf(';', i + 1);
            if (semi < 0 || semi - i > 12)
            {
                sb.Append(c);
                continue;
            }
            var entity = text.Substring(i + 1, semi - i - 1);
            if (entity.Length > 1 && entity[0] == '#')
            {
                var hex = entity[1] is 'x' or 'X';
                var digits = hex ? entity[2..] : entity[1..];
                if (int.TryParse(digits, hex ? NumberStyles.HexNumber : NumberStyles.None, CultureInfo.InvariantCulture, out var code)
                    && code > 0 && code <= 0x10FFFF && (code < 0xD800 || code > 0xDFFF))
                {
                    sb.Append(char.ConvertFromUtf32(code));
                    i = semi;
                    continue;
                }
            }
            else if (s_entities.TryGetValue(entity.ToLowerInvariant(), out var value))
            {
                sb.Append(value);
                i = semi;
                continue;
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Sanitizing into blocks
     * -------------------------------------------------------------------------------------------------------- */

    /// <summary>Parses and sanitizes <paramref name="html"/> into editor blocks (empty input gives no blocks).</summary>
    public static List<RichTextEditorBlock> ToBlocks(string? html)
    {
        var builder = new BlockBuilder();
        if (!string.IsNullOrEmpty(html))
        {
            builder.Walk(Parse(html), new Context(RichTextEditorMarks.None, null, Container.Top, RichTextBlockType.BulletedList, 0, 0));
        }
        builder.Close();
        return builder.Blocks;
    }

    /// <summary>True when the link target is safe to keep: http(s), mailto, tel, a path or an anchor.</summary>
    public static bool IsSafeUrl(string url)
    {
        var value = url.Trim();
        return value.StartsWith("http:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith('/')
            || value.StartsWith('#');
    }

    private enum Container
    {
        Top,
        Item,
        Quote
    }

    private readonly record struct Context(RichTextEditorMarks Marks, string? Href, Container Container, RichTextBlockType ItemType, int ItemLevel, int ListDepth);

    private sealed class BlockBuilder
    {
        private readonly List<RichTextEditorRun> _runs = new();
        private RichTextBlockType _type;
        private int _level;
        private bool _open;
        private bool _needBreak;
        private readonly Stack<RichTextBlockType> _lists = new();

        public List<RichTextEditorBlock> Blocks { get; } = new();

        public void Walk(RichTextEditorHtmlNode node, Context ctx)
        {
            foreach (var child in node.Children)
            {
                Visit(child, ctx);
            }
        }

        private void Visit(RichTextEditorHtmlNode node, Context ctx)
        {
            if (node.Tag is null)
            {
                AddText(node.Text, ctx);
                return;
            }
            var tag = node.Tag;
            if (s_drop.Contains(tag))
            {
                return;
            }
            switch (tag)
            {
                case "br":
                    Open(ctx);
                    _needBreak = false;
                    _runs.Add(new RichTextEditorRun("\n", ctx.Marks & ~RichTextEditorMarks.Code, ctx.Href));
                    return;
                case "b":
                case "strong":
                    if (tag == "b" && IsNormalWeight(node.Style))
                    {
                        Walk(node, ctx);
                        return;
                    }
                    Walk(node, ctx with { Marks = ctx.Marks | RichTextEditorMarks.Bold });
                    return;
                case "i":
                case "em":
                    Walk(node, ctx with { Marks = ctx.Marks | RichTextEditorMarks.Italic });
                    return;
                case "s":
                case "strike":
                case "del":
                    Walk(node, ctx with { Marks = ctx.Marks | RichTextEditorMarks.Strike });
                    return;
                case "code":
                case "kbd":
                case "samp":
                case "tt":
                    Walk(node, ctx with { Marks = ctx.Marks | RichTextEditorMarks.Code });
                    return;
                case "span":
                case "font":
                    Walk(node, ctx with { Marks = ctx.Marks | StyleMarks(node.Style) });
                    return;
                case "a":
                    var href = node.Href;
                    Walk(node, href is not null && IsSafeUrl(href) ? ctx with { Href = href } : ctx);
                    return;
                case "td":
                case "th":
                    Walk(node, ctx);
                    if (_open && _runs.Count > 0 && !_runs[^1].Text.EndsWith(' '))
                    {
                        _runs.Add(new RichTextEditorRun(" ", ctx.Marks, ctx.Href));
                    }
                    return;
            }
            var inline = ctx with { Marks = RichTextEditorMarks.None, Href = null };
            switch (tag)
            {
                case "p":
                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    if (ctx.Container != Container.Top)
                    {
                        Boundary(ctx);
                        Walk(node, ctx);
                        Boundary(ctx);
                        return;
                    }
                    Close();
                    Begin(tag switch
                    {
                        "h1" => RichTextBlockType.Heading1,
                        "h2" => RichTextBlockType.Heading2,
                        "h3" or "h4" or "h5" or "h6" => RichTextBlockType.Heading3,
                        _ => RichTextBlockType.Paragraph
                    }, 0);
                    Walk(node, inline);
                    Close();
                    return;
                case "ul":
                case "ol":
                    if (ctx.Container == Container.Quote)
                    {
                        Boundary(ctx);
                        Walk(node, ctx);
                        Boundary(ctx);
                        return;
                    }
                    Close();
                    _lists.Push(tag == "ol" ? RichTextBlockType.NumberedList : RichTextBlockType.BulletedList);
                    Walk(node, inline with { ListDepth = ctx.ListDepth + 1 });
                    _lists.Pop();
                    Close();
                    return;
                case "li":
                    if (ctx.Container == Container.Quote)
                    {
                        Boundary(ctx);
                        Walk(node, ctx);
                        Boundary(ctx);
                        return;
                    }
                    Close();
                    var itemType = _lists.Count > 0 ? _lists.Peek() : RichTextBlockType.BulletedList;
                    var itemLevel = Math.Max(0, ctx.ListDepth - 1);
                    var item = inline with { Container = Container.Item, ItemType = itemType, ItemLevel = itemLevel };
                    Begin(itemType, itemLevel);
                    Walk(node, item);
                    Close();
                    return;
                case "blockquote":
                    if (ctx.Container != Container.Top)
                    {
                        Boundary(ctx);
                        Walk(node, ctx);
                        Boundary(ctx);
                        return;
                    }
                    Close();
                    Begin(RichTextBlockType.Quote, 0);
                    Walk(node, inline with { Container = Container.Quote });
                    Close();
                    return;
                case "pre":
                    var code = TextContent(node);
                    if (code.StartsWith('\n'))
                    {
                        code = code[1..];
                    }
                    if (code.EndsWith('\n'))
                    {
                        code = code[..^1];
                    }
                    if (ctx.Container != Container.Top)
                    {
                        Boundary(ctx);
                        Open(ctx);
                        _runs.Add(new RichTextEditorRun(code, ctx.Marks, ctx.Href));
                        Boundary(ctx);
                        return;
                    }
                    Close();
                    Blocks.Add(RichTextEditorBlock.Plain(RichTextBlockType.CodeBlock, code.Replace("​", string.Empty)));
                    return;
                case "hr":
                    if (ctx.Container != Container.Top)
                    {
                        Boundary(ctx);
                        return;
                    }
                    Close();
                    Blocks.Add(RichTextEditorBlock.Divider);
                    return;
            }
            if (s_boundary.Contains(tag))
            {
                Boundary(ctx);
                Walk(node, ctx);
                Boundary(ctx);
                return;
            }
            // Unknown inline elements are unwrapped.
            Walk(node, ctx);
        }

        private void AddText(string raw, Context ctx)
        {
            var text = NormalizeWhitespace(raw);
            if (text.Length == 0)
            {
                return;
            }
            var blank = IsBlank(text);
            if (blank && (!_open || _needBreak))
            {
                return;
            }
            Open(ctx);
            if (_needBreak)
            {
                if (HasContent())
                {
                    _runs.Add(new RichTextEditorRun("\n", RichTextEditorMarks.None, null));
                }
                _needBreak = false;
            }
            _runs.Add(new RichTextEditorRun(text, ctx.Marks, ctx.Href));
        }

        private bool HasContent()
        {
            foreach (var run in _runs)
            {
                if (run.Text.Length > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private void Boundary(Context ctx)
        {
            if (ctx.Container == Container.Top)
            {
                Close();
            }
            else
            {
                _needBreak = true;
            }
        }

        private void Open(Context ctx)
        {
            if (_open)
            {
                return;
            }
            switch (ctx.Container)
            {
                case Container.Item:
                    Begin(ctx.ItemType, ctx.ItemLevel);
                    break;
                case Container.Quote:
                    Begin(RichTextBlockType.Quote, 0);
                    break;
                default:
                    Begin(RichTextBlockType.Paragraph, 0);
                    break;
            }
        }

        private void Begin(RichTextBlockType type, int level)
        {
            Close();
            _type = type;
            _level = level;
            _open = true;
            _needBreak = false;
            _runs.Clear();
        }

        public void Close()
        {
            if (!_open)
            {
                return;
            }
            _open = false;
            _needBreak = false;
            FinishWhitespace(_runs);
            Blocks.Add(RichTextEditorBlock.Create(_type, _level, _runs));
            _runs.Clear();
        }
    }

    private static bool IsBlank(string text)
    {
        foreach (var c in text)
        {
            if (!char.IsWhiteSpace(c) && c != NewlineMark)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsNormalWeight(string? style) =>
        style is not null && System.Text.RegularExpressions.Regex.IsMatch(style, @"font-weight:\s*(normal|[1-4]00)");

    private static RichTextEditorMarks StyleMarks(string? style)
    {
        if (string.IsNullOrEmpty(style))
        {
            return RichTextEditorMarks.None;
        }
        var marks = RichTextEditorMarks.None;
        if (System.Text.RegularExpressions.Regex.IsMatch(style, @"font-weight:\s*(bold|[6-9]00)"))
        {
            marks |= RichTextEditorMarks.Bold;
        }
        if (System.Text.RegularExpressions.Regex.IsMatch(style, @"font-style:\s*italic"))
        {
            marks |= RichTextEditorMarks.Italic;
        }
        if (style.Contains("line-through", StringComparison.Ordinal))
        {
            marks |= RichTextEditorMarks.Strike;
        }
        return marks;
    }

    /// <summary>Source line breaks (with the indentation around them) become a marker; zero width spaces go.</summary>
    private static string NormalizeWhitespace(string text)
    {
        var sb = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c is '\r' or '\n')
            {
                while (sb.Length > 0 && sb[^1] is ' ' or '\t')
                {
                    sb.Length--;
                }
                while (i < text.Length && text[i] is ' ' or '\t' or '\r' or '\n')
                {
                    i++;
                }
                sb.Append(NewlineMark);
                continue;
            }
            if (c == '​')
            {
                i++;
                continue;
            }
            sb.Append(c is ' ' or '\t' ? ' ' : c);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>Drops source line breaks at the block edges, turns the others into spaces, and drops a trailing filler break.</summary>
    private static void FinishWhitespace(List<RichTextEditorRun> runs)
    {
        for (var i = 0; i < runs.Count; i++)
        {
            var text = runs[i].Text.TrimStart(NewlineMark);
            runs[i] = runs[i] with { Text = text };
            if (text.Length > 0)
            {
                break;
            }
        }
        for (var i = runs.Count - 1; i >= 0; i--)
        {
            var text = runs[i].Text.TrimEnd(NewlineMark);
            runs[i] = runs[i] with { Text = text };
            if (text.Length > 0)
            {
                break;
            }
        }
        for (var i = 0; i < runs.Count; i++)
        {
            if (runs[i].Text.IndexOf(NewlineMark) >= 0)
            {
                runs[i] = runs[i] with { Text = runs[i].Text.Replace(NewlineMark, ' ') };
            }
        }
        // A trailing <br> only holds the line open in a browser; it is not a line of its own.
        for (var i = runs.Count - 1; i >= 0; i--)
        {
            if (runs[i].Text.Length == 0)
            {
                continue;
            }
            if (runs[i].Text.EndsWith('\n'))
            {
                runs[i] = runs[i] with { Text = runs[i].Text[..^1] };
            }
            break;
        }
    }

    private static string TextContent(RichTextEditorHtmlNode node)
    {
        var sb = new StringBuilder();
        void Walk(RichTextEditorHtmlNode current)
        {
            foreach (var child in current.Children)
            {
                if (child.Tag is null)
                {
                    sb.Append(child.Text);
                }
                else if (child.Tag == "br")
                {
                    sb.Append('\n');
                }
                else if (!s_drop.Contains(child.Tag))
                {
                    Walk(child);
                }
            }
        }
        Walk(node);
        return sb.ToString().Replace("\r\n", "\n").Replace('\r', '\n');
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Output
     * -------------------------------------------------------------------------------------------------------- */

    public static string Escape(string value)
    {
        if (value.AsSpan().IndexOfAny("&<>\"") < 0)
        {
            return value;
        }
        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }

    /// <summary>Clean HTML for the document; an empty document is <c>&lt;p&gt;&lt;br&gt;&lt;/p&gt;</c>.</summary>
    public static string Write(IReadOnlyList<RichTextEditorBlock> blocks)
    {
        var count = OutputCount(blocks);
        var sb = new StringBuilder();
        var i = 0;
        while (i < count)
        {
            var block = blocks[i];
            if (block.IsList)
            {
                var end = ListGroupEnd(blocks, i, count);
                WriteList(blocks, i, end, sb);
                i = end;
                continue;
            }
            switch (block.Type)
            {
                case RichTextBlockType.Divider:
                    sb.Append("<hr>");
                    break;
                case RichTextBlockType.CodeBlock:
                    sb.Append("<pre>").Append(block.Length == 0 ? "<br>" : Escape(block.Text)).Append("</pre>");
                    break;
                default:
                    var tag = block.Type switch
                    {
                        RichTextBlockType.Heading1 => "h1",
                        RichTextBlockType.Heading2 => "h2",
                        RichTextBlockType.Heading3 => "h3",
                        RichTextBlockType.Quote => "blockquote",
                        _ => "p"
                    };
                    sb.Append('<').Append(tag).Append('>');
                    WriteInlineBlock(block, sb);
                    sb.Append("</").Append(tag).Append('>');
                    break;
            }
            i++;
        }
        return sb.Length == 0 ? "<p><br></p>" : sb.ToString();
    }

    private static void WriteInlineBlock(RichTextEditorBlock block, StringBuilder sb)
    {
        if (block.Length == 0)
        {
            sb.Append("<br>");
            return;
        }
        WriteInline(block, 0, block.SpanCount, 0, sb);
        // A line break at the very end needs a filler break, or browsers would not show the empty line.
        if (block.Text.EndsWith('\n'))
        {
            sb.Append("<br>");
        }
    }

    private static readonly string[] s_tags = { "a", "strong", "em", "s", "code" };

    private static readonly RichTextEditorMarks[] s_levelMarks = { RichTextEditorMarks.None, RichTextEditorMarks.Bold, RichTextEditorMarks.Italic, RichTextEditorMarks.Strike, RichTextEditorMarks.Code };

    /// <summary>The grouping key of a span at a nesting level: links outermost, then strong, em, s and code.</summary>
    internal static object? GroupKey(in RichTextEditorSpan span, int level) => level == 0
        ? span.Href
        : (span.Marks & s_levelMarks[level]) != 0 ? s_levelMarks[level] : null;

    private static void WriteInline(RichTextEditorBlock block, int from, int to, int level, StringBuilder sb)
    {
        var spans = block.Spans;
        if (level == s_tags.Length)
        {
            for (var i = from; i < to; i++)
            {
                var span = spans[i];
                var text = block.Text.AsSpan(span.Start, span.Length);
                foreach (var c in text)
                {
                    switch (c)
                    {
                        case '\n':
                            sb.Append("<br>");
                            break;
                        case '&':
                            sb.Append("&amp;");
                            break;
                        case '<':
                            sb.Append("&lt;");
                            break;
                        case '>':
                            sb.Append("&gt;");
                            break;
                        case '"':
                            sb.Append("&quot;");
                            break;
                        default:
                            sb.Append(c);
                            break;
                    }
                }
            }
            return;
        }
        var index = from;
        while (index < to)
        {
            var key = GroupKey(spans[index], level);
            var end = index + 1;
            while (end < to && Equals(GroupKey(spans[end], level), key))
            {
                end++;
            }
            if (key is null)
            {
                WriteInline(block, index, end, level + 1, sb);
            }
            else
            {
                sb.Append('<').Append(s_tags[level]);
                if (level == 0)
                {
                    sb.Append(" href=\"").Append(Escape((string)key)).Append('"');
                }
                sb.Append('>');
                WriteInline(block, index, end, level + 1, sb);
                sb.Append("</").Append(s_tags[level]).Append('>');
            }
            index = end;
        }
    }

    private static void WriteList(IReadOnlyList<RichTextEditorBlock> blocks, int from, int to, StringBuilder sb)
    {
        var stack = new List<(int Level, RichTextBlockType Type)>();
        for (var i = from; i < to; i++)
        {
            var item = blocks[i];
            while (stack.Count > 0 && stack[^1].Level > item.Level)
            {
                sb.Append("</li></").Append(ListTag(stack[^1].Type)).Append('>');
                stack.RemoveAt(stack.Count - 1);
            }
            if (stack.Count == 0 || stack[^1].Level < item.Level)
            {
                sb.Append('<').Append(ListTag(item.Type)).Append("><li>");
                stack.Add((item.Level, item.Type));
            }
            else if (stack[^1].Type != item.Type)
            {
                sb.Append("</li></").Append(ListTag(stack[^1].Type)).Append("><").Append(ListTag(item.Type)).Append("><li>");
                stack[^1] = (item.Level, item.Type);
            }
            else
            {
                sb.Append("</li><li>");
            }
            WriteInlineBlock(item, sb);
        }
        while (stack.Count > 0)
        {
            sb.Append("</li></").Append(ListTag(stack[^1].Type)).Append('>');
            stack.RemoveAt(stack.Count - 1);
        }
    }

    private static string ListTag(RichTextBlockType type) => type == RichTextBlockType.NumberedList ? "ol" : "ul";

    /// <summary>The end (exclusive) of the list beginning at <paramref name="start"/>: a change of type at the outer level starts a new list.</summary>
    internal static int ListGroupEnd(IReadOnlyList<RichTextEditorBlock> blocks, int start, int count)
    {
        var outer = blocks[start].Type;
        var end = start + 1;
        while (end < count && blocks[end].IsList && (blocks[end].Level > 0 || blocks[end].Type == outer))
        {
            end++;
        }
        return end;
    }

    /// <summary>Blocks that reach the output: trailing elements without visible text are dropped (dividers stay), keeping at least one.</summary>
    internal static int OutputCount(IReadOnlyList<RichTextEditorBlock> blocks)
    {
        // Group starts in order, so trailing empty groups can be dropped whole.
        var starts = new List<int>();
        var i = 0;
        while (i < blocks.Count)
        {
            starts.Add(i);
            i = blocks[i].IsList ? ListGroupEnd(blocks, i, blocks.Count) : i + 1;
        }
        var count = blocks.Count;
        for (var g = starts.Count - 1; g > 0; g--)
        {
            var start = starts[g];
            var visible = false;
            for (var k = start; k < count; k++)
            {
                if (blocks[k].IsDivider || !string.IsNullOrWhiteSpace(blocks[k].Text))
                {
                    visible = true;
                    break;
                }
            }
            if (visible)
            {
                break;
            }
            count = start;
        }
        return count;
    }

    /// <summary>Plain text: one line per block (dividers skipped, empty blocks skipped).</summary>
    public static string PlainText(IReadOnlyList<RichTextEditorBlock> blocks)
    {
        var sb = new StringBuilder();
        foreach (var block in blocks)
        {
            if (block.IsDivider)
            {
                continue;
            }
            var text = block.Text.Trim();
            if (text.Length == 0)
            {
                continue;
            }
            if (sb.Length > 0)
            {
                sb.Append('\n');
            }
            sb.Append(text);
        }
        return sb.ToString();
    }

    public static bool IsDocumentEmpty(IReadOnlyList<RichTextEditorBlock> blocks)
    {
        foreach (var block in blocks)
        {
            if (block.IsDivider || !string.IsNullOrWhiteSpace(block.Text))
            {
                return false;
            }
        }
        return true;
    }

    public static ImmutableList<RichTextEditorBlock> ToDocument(string? html)
    {
        var blocks = ToBlocks(html);
        return RichTextEditorEngine.Normalize(blocks.Count == 0 ? ImmutableList.Create(RichTextEditorBlock.EmptyParagraph) : blocks.ToImmutableList());
    }
}
