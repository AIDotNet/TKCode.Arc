using System.Collections.Immutable;
using System.Text;
using StreamMark.Core.Model;

namespace StreamMark.Core.Html;

/// <summary>
/// 把 <see cref="HtmlTreeBuilder"/> 构建的树转换为 StreamMark 块模型：标题、段落、列表、表格、引用、代码、
/// 分隔线、折叠块直接复用已有的块；div / p / center 等成为 <see cref="HtmlBoxBlock"/>，携带盒子与文字样式。
/// </summary>
public static class HtmlMapper
{
    /// <summary>把根节点的子节点转换为块序列。</summary>
    public static ImmutableArray<MarkdownBlock> MapChildren(HtmlElement root) => [.. Blocks(root.Children)];

    private static List<MarkdownBlock> Blocks(List<HtmlNode> nodes)
    {
        var blocks = new List<MarkdownBlock>();
        HtmlInlineBuilder? inline = null;
        void Flush()
        {
            if (inline is not null && inline.Build() is { IsEmpty: false } content)
            {
                blocks.Add(new ParagraphBlock(content));
            }
            inline = null;
        }
        foreach (var node in nodes)
        {
            switch (node)
            {
                case HtmlMarkdown markdown:
                    Flush();
                    blocks.Add(markdown.Block);
                    break;
                case HtmlElement element when HtmlElements.Block.Contains(element.Name) || ContainsMarkdown(element):
                    Flush();
                    if (Block(element) is { } block)
                    {
                        blocks.Add(block);
                    }
                    break;
                default:
                    inline ??= new HtmlInlineBuilder();
                    inline.Add(node);
                    break;
            }
        }
        Flush();
        return blocks;
    }

    private static bool ContainsMarkdown(HtmlElement element) =>
        element.Children.Any(c => c is HtmlMarkdown || c is HtmlElement child && ContainsMarkdown(child));

    private static bool IsHidden(HtmlElement element) =>
        element.Attributes["hidden"] is not null
        || element.Attributes["style"] is { } style && style.Replace(" ", "", StringComparison.Ordinal).Contains("display:none", StringComparison.OrdinalIgnoreCase);

    private static MarkdownBlock? Block(HtmlElement element)
    {
        if (IsHidden(element))
        {
            return null;
        }
        var (box, text) = CssStyle.FromAttributes(element.Name, element.Attributes);
        // 背景属于盒子；行内背景只用于行内区间
        text = text with { Background = null };
        MarkdownBlock? inner = element.Name switch
        {
            "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => new HeadingBlock(element.Name[1] - '0', Inline(element.Children), element.Attributes["id"]),
            "ul" or "ol" => List(element),
            "blockquote" => new QuoteBlock([.. Blocks(element.Children)]),
            "pre" => Code(element),
            "hr" => new ThematicBreakBlock(),
            "table" => Table(element),
            "details" => Details(element),
            "dl" => Definitions(element),
            _ => null,
        };
        if (inner is not null)
        {
            return box.IsDefault && text.IsDefault ? inner : new HtmlBoxBlock(element.Name, box, text, [inner]);
        }
        // div、p、center、section、figure 等，以及包含 Markdown 的行内元素：盒子
        return new HtmlBoxBlock(element.Name, box, text, [.. Blocks(element.Children)]);
    }

    private static ListBlock List(HtmlElement element)
    {
        var ordered = element.Name == "ol";
        var start = ordered && int.TryParse(element.Attributes["start"], out var s) ? s : 1;
        var items = ImmutableArray.CreateBuilder<ListItemBlock>();
        List<HtmlNode>? stray = null;
        foreach (var child in element.Children)
        {
            if (child is HtmlElement { Name: "li" } li)
            {
                items.Add(new ListItemBlock(null, [.. Blocks(li.Children)]));
            }
            else if (child is not HtmlText { Text: var t } || !string.IsNullOrWhiteSpace(t))
            {
                // 列表中直接出现的其它内容：并入一个列表项
                stray ??= [];
                stray.Add(child);
            }
        }
        if (stray is not null)
        {
            items.Add(new ListItemBlock(null, [.. Blocks(stray)]));
        }
        return new ListBlock(ordered, start, ordered ? '.' : '-', false, items.ToImmutable());
    }

    private static CodeBlock Code(HtmlElement element)
    {
        string? language = null;
        var source = element;
        if (element.Children.Count(c => c is not HtmlText { Text: var t } || !string.IsNullOrWhiteSpace(t)) == 1
            && element.Children.OfType<HtmlElement>().FirstOrDefault() is { Name: "code" } code)
        {
            source = code;
            language = LanguageOf(code.Attributes["class"]) ?? LanguageOf(element.Attributes["class"]);
        }
        language ??= LanguageOf(element.Attributes["class"]) ?? element.Attributes["lang"];
        var sb = new StringBuilder();
        AppendRawText(source, sb);
        var text = sb.ToString();
        if (text.StartsWith('\n'))
        {
            text = text[1..];
        }
        return new CodeBlock(language, language, text.TrimEnd('\n'), IsFenced: true, IsOpen: false);
    }

    private static string? LanguageOf(string? classes)
    {
        foreach (var name in (classes ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (name.StartsWith("language-", StringComparison.OrdinalIgnoreCase))
            {
                return name[9..];
            }
            if (name.StartsWith("lang-", StringComparison.OrdinalIgnoreCase))
            {
                return name[5..];
            }
        }
        return null;
    }

    private static void AppendRawText(HtmlElement element, StringBuilder sb)
    {
        foreach (var child in element.Children)
        {
            switch (child)
            {
                case HtmlText text:
                    sb.Append(text.Text);
                    break;
                case HtmlElement { Name: "br" }:
                    sb.Append('\n');
                    break;
                case HtmlElement nested:
                    AppendRawText(nested, sb);
                    break;
            }
        }
    }

    private static TableBlock Table(HtmlElement table)
    {
        var rows = new List<(bool Header, List<HtmlElement> Cells)>();
        void CollectRows(HtmlElement container, bool header)
        {
            foreach (var child in container.Children.OfType<HtmlElement>())
            {
                switch (child.Name)
                {
                    case "thead":
                        CollectRows(child, true);
                        break;
                    case "tbody" or "tfoot":
                        CollectRows(child, false);
                        break;
                    case "tr":
                        {
                            var cells = child.Children.OfType<HtmlElement>().Where(c => c.Name is "td" or "th").ToList();
                            rows.Add((header || cells.Count > 0 && cells.All(c => c.Name == "th"), cells));
                            break;
                        }
                    case "td" or "th":
                        // 缺少 <tr> 的单元格：自成一行
                        rows.Add((false, [child]));
                        break;
                }
            }
        }
        CollectRows(table, false);
        var columns = rows.Count == 0 ? 0 : rows.Max(r => r.Cells.Count);
        var alignments = new TableAlignment[columns];
        foreach (var (_, cells) in rows)
        {
            for (var c = 0; c < cells.Count; c++)
            {
                if (alignments[c] == TableAlignment.None)
                {
                    alignments[c] = CssStyle.FromAttributes(cells[c].Name, cells[c].Attributes).Text.Align switch
                    {
                        TextAlign.Left => TableAlignment.Left,
                        TextAlign.Center => TableAlignment.Center,
                        TextAlign.Right => TableAlignment.Right,
                        _ => TableAlignment.None,
                    };
                }
            }
        }
        var mapped = rows.Select(r => new TableRow(r.Header, [.. r.Cells.Select(c => Inline(c.Children))])).ToImmutableArray();
        return new TableBlock([.. alignments], mapped);
    }

    private static ComponentBlock Details(HtmlElement element)
    {
        var children = ImmutableArray.CreateBuilder<MarkdownBlock>();
        var body = new List<HtmlNode>();
        foreach (var child in element.Children)
        {
            if (child is HtmlElement { Name: "summary" } summary)
            {
                children.Add(new ComponentBlock("summary", summary.Attributes, [new ParagraphBlock(Inline(summary.Children))]));
            }
            else
            {
                body.Add(child);
            }
        }
        children.AddRange(Blocks(body));
        return new ComponentBlock("details", element.Attributes, children.ToImmutable());
    }

    private static DefinitionListBlock Definitions(HtmlElement element)
    {
        var items = ImmutableArray.CreateBuilder<DefinitionItem>();
        var terms = ImmutableArray.CreateBuilder<InlineContent>();
        var definitions = ImmutableArray.CreateBuilder<MarkdownBlock>();
        void FlushItem()
        {
            if (terms.Count > 0 || definitions.Count > 0)
            {
                items.Add(new DefinitionItem(terms.ToImmutable(), definitions.ToImmutable()));
            }
            terms.Clear();
            definitions.Clear();
        }
        foreach (var child in element.Children.OfType<HtmlElement>())
        {
            if (child.Name == "dt")
            {
                if (definitions.Count > 0)
                {
                    FlushItem();
                }
                terms.Add(Inline(child.Children));
            }
            else if (child.Name == "dd")
            {
                definitions.AddRange(Blocks(child.Children));
            }
        }
        FlushItem();
        return new DefinitionListBlock(items.ToImmutable());
    }

    private static InlineContent Inline(List<HtmlNode> nodes)
    {
        var builder = new HtmlInlineBuilder();
        foreach (var node in nodes)
        {
            builder.Add(node);
        }
        return builder.Build();
    }
}

/// <summary>把 HTML 行内节点转换为扁平的 <see cref="InlineContent"/>（空白按 HTML 规则折叠）。</summary>
internal sealed class HtmlInlineBuilder
{
    private readonly StringBuilder _text = new();
    private readonly List<InlineSpan> _spans = [];
    private readonly List<LinkTarget> _links = [];
    private readonly List<InlineObject> _objects = [];
    private readonly List<HtmlTextStyle> _textStyles = [];
    // 待输出的折叠空白（遇到下一个可见内容时才写出，行首 / 换行后丢弃）
    private bool _pendingSpace;
    private bool _pendingSpaceHadNewline;

    public void Add(HtmlNode node) => Visit(node, InlineStyle.None, -1, -1, HtmlTextStyle.Default);

    private void Visit(HtmlNode node, InlineStyle style, int link, int textStyle, HtmlTextStyle current)
    {
        switch (node)
        {
            case HtmlText text:
                AppendCollapsed(text.Text, style, link, textStyle);
                break;
            case HtmlMarkdown markdown:
                // 行内上下文中的 Markdown 块（少见）：取其文字
                LineBreak();
                if (markdown.Block is ParagraphBlock paragraph)
                {
                    AppendContent(paragraph.Content);
                }
                LineBreak();
                break;
            case HtmlElement element:
                VisitElement(element, style, link, textStyle, current);
                break;
        }
    }

    private void VisitElement(HtmlElement element, InlineStyle style, int link, int textStyle, HtmlTextStyle current)
    {
        if (element.Attributes["hidden"] is not null)
        {
            return;
        }
        switch (element.Name)
        {
            case "br":
                _pendingSpace = false;
                Append("\n", style, link, textStyle);
                return;
            case "wbr" or "source" or "col" or "colgroup":
                return;
            case "img":
                AddImage(element, link);
                return;
            case "picture":
                if (FirstImage(element) is { } img)
                {
                    AddImage(img, link);
                }
                return;
            case "hr":
                LineBreak();
                return;
        }
        style |= StyleFor(element.Name);
        if (element.Name == "a" && element.Attributes["href"] is { Length: > 0 } href)
        {
            _links.Add(new LinkTarget(href.Trim(), element.Attributes["title"], false));
            link = _links.Count - 1;
            style |= InlineStyle.Link;
        }
        var (_, own) = CssStyle.FromAttributes(element.Name, element.Attributes);
        if (element.Name == "small")
        {
            own = own with { FontScale = own.FontScale ?? 0.85 };
        }
        else if (element.Name == "big")
        {
            own = own with { FontScale = own.FontScale ?? 1.2 };
        }
        own = own with { Align = TextAlign.Inherit };
        if (!own.IsDefault)
        {
            current = own.Over(current);
            _textStyles.Add(current);
            textStyle = _textStyles.Count - 1;
        }
        var block = HtmlElements.Block.Contains(element.Name);
        if (block)
        {
            // 行内上下文中的块级元素（表格单元格里的 <p>、<div>）：前后换行
            LineBreak();
        }
        if (element.Name == "q")
        {
            Append("“", style, link, textStyle);
        }
        foreach (var child in element.Children)
        {
            Visit(child, style, link, textStyle, current);
        }
        if (element.Name == "q")
        {
            Append("”", style, link, textStyle);
        }
        if (block)
        {
            LineBreak();
        }
    }

    /// <summary>行内 HTML 元素对应的样式标志。</summary>
    internal static InlineStyle StyleFor(string name) => name switch
    {
        "strong" or "b" => InlineStyle.Bold,
        "em" or "i" or "cite" or "dfn" or "var" => InlineStyle.Italic,
        "u" or "ins" => InlineStyle.Underline,
        "s" or "del" or "strike" => InlineStyle.Strikethrough,
        "code" or "samp" or "tt" => InlineStyle.Code,
        "kbd" => InlineStyle.Keyboard,
        "sub" => InlineStyle.Subscript,
        "sup" => InlineStyle.Superscript,
        "mark" => InlineStyle.Mark,
        _ => InlineStyle.None,
    };

    private static HtmlElement? FirstImage(HtmlElement element)
    {
        foreach (var child in element.Children.OfType<HtmlElement>())
        {
            if (child.Name == "img")
            {
                return child;
            }
            if (FirstImage(child) is { } nested)
            {
                return nested;
            }
        }
        return null;
    }

    private void AddImage(HtmlElement img, int link)
    {
        var attributes = img.Attributes;
        var (width, height) = CssStyle.ImageSize(attributes);
        // 把 style 中的尺寸规范化为 width / height 属性（渲染层只读这两个）
        var items = attributes.Items.Where(a => !a.Key.Equals("width", StringComparison.OrdinalIgnoreCase) && !a.Key.Equals("height", StringComparison.OrdinalIgnoreCase)).ToList();
        if (width is { } w)
        {
            items.Add(new("width", w.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        if (height is { } h)
        {
            items.Add(new("height", h.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        if (_pendingSpace && !AtLineStart)
        {
            _text.Append(' ');
        }
        _pendingSpace = false;
        var offset = _text.Length;
        _objects.Add(new InlineObject(offset, InlineObjectKind.Image, attributes["src"] ?? "", attributes["alt"], attributes["title"])
        {
            Attributes = new ComponentAttributes([.. items]),
        });
        _text.Append(InlineObject.Placeholder);
        if (link >= 0)
        {
            // 链接中的图片（徽章等）：占位字符也带链接，点击打开链接
            _spans.Add(new InlineSpan(offset, 1, InlineStyle.Link, link));
        }
    }

    private bool AtLineStart => _text.Length == 0 || _text[^1] == '\n';

    private void LineBreak()
    {
        _pendingSpace = false;
        if (!AtLineStart)
        {
            _text.Append('\n');
        }
    }

    private void AppendCollapsed(string text, InlineStyle style, int link, int textStyle)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c) && c != ' ')
            {
                if (sb.Length > 0)
                {
                    Write(sb, style, link, textStyle);
                    sb.Clear();
                }
                _pendingSpaceHadNewline |= c == '\n';
                _pendingSpace = true;
                continue;
            }
            if (_pendingSpace)
            {
                // 换行两侧都是 CJK 字符时不插入空格
                var skip = AtLineStart || _pendingSpaceHadNewline && IsWide(c) && _text.Length > 0 && IsWide(_text[^1]);
                if (!skip)
                {
                    sb.Append(' ');
                }
                _pendingSpace = false;
                _pendingSpaceHadNewline = false;
            }
            sb.Append(c);
        }
        if (sb.Length > 0)
        {
            Write(sb, style, link, textStyle);
        }
    }

    private void Write(StringBuilder sb, InlineStyle style, int link, int textStyle) => Append(sb.ToString(), style, link, textStyle);

    private static bool IsWide(char c) =>
        c is >= '⺀' and <= '鿿' or >= '가' and <= '힯' or >= '豈' and <= '﫿' or >= '＀' and <= '￯';

    private void Append(string text, InlineStyle style, int link, int textStyle)
    {
        if (text.Length == 0)
        {
            return;
        }
        var start = _text.Length;
        _text.Append(text);
        if (style == InlineStyle.None && link < 0 && textStyle < 0)
        {
            return;
        }
        if (_spans.Count > 0 && _spans[^1] is var last && last.End == start && last.Style == style && last.LinkIndex == link && last.TextStyleIndex == textStyle)
        {
            _spans[^1] = last with { Length = last.Length + text.Length };
            return;
        }
        _spans.Add(new InlineSpan(start, text.Length, style, link, -1, textStyle));
    }

    // 嵌入一段已映射的行内内容（偏移、链接序号重新编号）
    private void AppendContent(InlineContent content)
    {
        var offset = _text.Length;
        var linkBase = _links.Count;
        var styleBase = _textStyles.Count;
        _text.Append(content.Text);
        _links.AddRange(content.Links);
        _textStyles.AddRange(content.TextStyles);
        foreach (var span in content.Spans)
        {
            _spans.Add(span with
            {
                Start = span.Start + offset,
                LinkIndex = span.LinkIndex < 0 ? -1 : span.LinkIndex + linkBase,
                ComponentIndex = -1,
                TextStyleIndex = span.TextStyleIndex < 0 ? -1 : span.TextStyleIndex + styleBase,
            });
        }
        foreach (var obj in content.Objects)
        {
            _objects.Add(obj with { Offset = obj.Offset + offset });
        }
    }

    public InlineContent Build()
    {
        // 去掉末尾的换行与空格
        var end = _text.Length;
        while (end > 0 && _text[end - 1] is '\n' or ' ')
        {
            end--;
        }
        var text = _text.ToString(0, end);
        var spans = _spans.Where(s => s.Start < end).Select(s => s.End > end ? s with { Length = end - s.Start } : s).ToImmutableArray();
        return new InlineContent(text, spans, [.. _links], [.. _objects.Where(o => o.Offset < end)]) { TextStyles = [.. _textStyles] };
    }
}
