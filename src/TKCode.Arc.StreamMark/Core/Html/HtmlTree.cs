using System.Collections.Immutable;
using System.Net;
using StreamMark.Core.Model;
using StreamMark.Core.Parsing;

namespace StreamMark.Core.Html;

/// <summary>支持的 HTML 元素（单一来源）。不在表中的元素使所在的 HTML 片段按源码显示。</summary>
public static class HtmlElements
{
    /// <summary>块级元素。</summary>
    public static ImmutableHashSet<string> Block { get; } = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase,
        "div", "p", "center", "section", "article", "header", "footer", "main", "nav", "aside", "figure", "figcaption", "address", "hgroup",
        "h1", "h2", "h3", "h4", "h5", "h6",
        "table", "caption", "thead", "tbody", "tfoot", "tr", "th", "td", "colgroup", "col",
        "ul", "ol", "li", "dl", "dt", "dd", "blockquote", "pre", "hr", "details", "summary");

    /// <summary>行内元素。</summary>
    public static ImmutableHashSet<string> Inline { get; } = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase,
        "a", "img", "picture", "source", "br", "wbr", "strong", "b", "em", "i", "u", "ins", "s", "del", "strike",
        "code", "kbd", "samp", "tt", "var", "sub", "sup", "mark", "small", "big", "span", "font", "abbr", "acronym",
        "cite", "dfn", "q", "time", "label", "bdi", "bdo", "data");

    /// <summary>空元素（没有内容、没有结束标签）。</summary>
    public static ImmutableHashSet<string> Void { get; } = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase,
        "br", "wbr", "img", "hr", "source", "col", "input", "meta", "link", "area", "base", "embed", "param", "track");

    /// <summary>可以跨空行包住 Markdown 的容器元素：流式切分时未闭合就不冻结。</summary>
    public static ImmutableArray<string> Containers { get; } =
        ["div", "center", "section", "article", "header", "footer", "main", "aside", "figure", "table", "blockquote", "ul", "ol", "dl"];

    public static bool IsKnown(string name) => Block.Contains(name) || Inline.Contains(name);
}

/// <summary>HTML 树的节点。</summary>
public abstract class HtmlNode;

public sealed class HtmlText(string text) : HtmlNode
{
    public string Text { get; set; } = text;
}

public sealed class HtmlElement(string name, ComponentAttributes attributes) : HtmlNode
{
    public string Name { get; } = name;

    public ComponentAttributes Attributes { get; } = attributes;

    public List<HtmlNode> Children { get; } = [];

    public override string ToString() => "<" + Name + ">";
}

/// <summary>夹在 HTML 元素中间的 Markdown 块（GFM：HTML 块之间空行隔开的 Markdown 照常解析）。</summary>
public sealed class HtmlMarkdown(MarkdownBlock block) : HtmlNode
{
    public MarkdownBlock Block { get; } = block;
}

/// <summary>
/// 容错的 HTML 树构建器（只覆盖支持子集需要的规则）。可以分多次 <see cref="Feed"/>：
/// 一段 HTML 中未闭合的元素保持打开，之后的 Markdown 块（<see cref="AddMarkdown"/>）和 HTML 继续加入其中。
/// </summary>
public sealed class HtmlTreeBuilder
{
    private readonly List<HtmlElement> _open = [];

    public HtmlTreeBuilder()
    {
        Root = new HtmlElement("#root", ComponentAttributes.Empty);
        _open.Add(Root);
    }

    public HtmlElement Root { get; }

    /// <summary>是否还有未闭合的元素。</summary>
    public bool HasOpenElements => _open.Count > 1;

    /// <summary>是否遇到了不支持的元素。</summary>
    public bool HasUnknownElements { get; private set; }

    /// <summary>是否包含至少一个元素（只有注释 / 文字时为 false）。</summary>
    public bool HasElements { get; private set; }

    private HtmlElement Current => _open[^1];

    public void AddMarkdown(MarkdownBlock block) => Current.Children.Add(new HtmlMarkdown(block));

    public void Feed(string html)
    {
        var text = html.AsSpan();
        var i = 0;
        var textStart = 0;
        while (i < text.Length)
        {
            if (text[i] != '<')
            {
                i++;
                continue;
            }
            var rest = text[i..];
            if (rest.StartsWith("<!--"))
            {
                FlushText(text[textStart..i]);
                var end = rest.IndexOf("-->");
                i = end < 0 ? text.Length : i + end + 3;
                textStart = i;
                continue;
            }
            if (rest.Length > 1 && rest[1] is '!' or '?')
            {
                FlushText(text[textStart..i]);
                var end = rest.IndexOf('>');
                i = end < 0 ? text.Length : i + end + 1;
                textStart = i;
                continue;
            }
            if (rest.Length > 1 && (char.IsAsciiLetter(rest[1]) || rest[1] == '/' && rest.Length > 2 && char.IsAsciiLetter(rest[2])))
            {
                if (HtmlTagReader.TryRead(rest, out var tag))
                {
                    FlushText(text[textStart..i]);
                    i += tag.Length;
                    textStart = i;
                    if (tag.IsClosing)
                    {
                        Close(tag.Name);
                    }
                    else
                    {
                        Open(tag.Name, tag.Attributes, tag.IsSelfClosing);
                        if (IsRawText(tag.Name) && !tag.IsSelfClosing)
                        {
                            // script / style 等的内容是原始文本，直到对应的结束标签
                            var close = rest[tag.Length..].ToString().IndexOf("</" + tag.Name, StringComparison.OrdinalIgnoreCase);
                            var contentEnd = close < 0 ? text.Length : i + close;
                            Current.Children.Add(new HtmlText(text[i..contentEnd].ToString()));
                            i = contentEnd;
                            textStart = i;
                        }
                    }
                    continue;
                }
                if (rest.IndexOf('>') < 0)
                {
                    // 写了一半的标签（流式输出中）：丢弃，等它写完
                    FlushText(text[textStart..i]);
                    textStart = i = text.Length;
                    continue;
                }
            }
            i++;
        }
        FlushText(text[textStart..]);
    }

    private static bool IsRawText(string name) =>
        name.Equals("script", StringComparison.OrdinalIgnoreCase) || name.Equals("style", StringComparison.OrdinalIgnoreCase)
        || name.Equals("textarea", StringComparison.OrdinalIgnoreCase) || name.Equals("title", StringComparison.OrdinalIgnoreCase);

    private void FlushText(ReadOnlySpan<char> raw)
    {
        if (raw.IsEmpty)
        {
            return;
        }
        var decoded = raw.Contains('&') ? WebUtility.HtmlDecode(raw.ToString()) : raw.ToString();
        if (Current.Children.Count > 0 && Current.Children[^1] is HtmlText last)
        {
            last.Text += decoded;
        }
        else
        {
            Current.Children.Add(new HtmlText(decoded));
        }
    }

    private void Open(string name, ComponentAttributes attributes, bool selfClosing)
    {
        name = name.ToLowerInvariant();
        HasElements = true;
        if (!HtmlElements.IsKnown(name))
        {
            HasUnknownElements = true;
        }
        ImplicitClose(name);
        var element = new HtmlElement(name, attributes);
        Current.Children.Add(element);
        if (!selfClosing && !HtmlElements.Void.Contains(name))
        {
            _open.Add(element);
        }
    }

    // HTML 的隐式闭合：块级元素结束打开的 p；同级的 li / dt / dd / tr / td 结束前一个
    private void ImplicitClose(string name)
    {
        switch (name)
        {
            case "li":
                CloseUntil(["li"], ["ul", "ol", "table"]);
                break;
            case "dt" or "dd":
                CloseUntil(["dt", "dd"], ["dl", "table"]);
                break;
            case "tr":
                CloseUntil(["tr", "td", "th"], ["table", "thead", "tbody", "tfoot"]);
                break;
            case "td" or "th":
                CloseUntil(["td", "th"], ["tr", "table"]);
                break;
            case "thead" or "tbody" or "tfoot":
                CloseUntil(["thead", "tbody", "tfoot", "tr", "td", "th"], ["table"]);
                break;
        }
        if (HtmlElements.Block.Contains(name) && name is not ("td" or "th" or "tr" or "li" or "dt" or "dd" or "summary"))
        {
            CloseUntil(["p"], ["td", "th", "li", "div", "blockquote", "table", "details", "dd"]);
        }
    }

    // 从栈顶向下找到边界元素为止，关闭其中最外层的目标元素（新的 <tr> 同时结束打开的 <td> 与 <tr>）
    private void CloseUntil(string[] targets, string[] boundaries)
    {
        var cut = -1;
        for (var i = _open.Count - 1; i > 0; i--)
        {
            var name = _open[i].Name;
            if (Array.IndexOf(boundaries, name) >= 0)
            {
                break;
            }
            if (Array.IndexOf(targets, name) >= 0)
            {
                cut = i;
            }
        }
        if (cut > 0)
        {
            _open.RemoveRange(cut, _open.Count - cut);
        }
    }

    private void Close(string name)
    {
        for (var i = _open.Count - 1; i > 0; i--)
        {
            if (string.Equals(_open[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                _open.RemoveRange(i, _open.Count - i);
                return;
            }
        }
        // 不配对的结束标签忽略
        if (!HtmlElements.IsKnown(name))
        {
            HasUnknownElements = true;
        }
    }
}
