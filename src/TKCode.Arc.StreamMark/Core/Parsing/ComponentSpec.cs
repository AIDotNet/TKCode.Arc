using System.Collections.Immutable;
using StreamMark.Core.Model;

namespace StreamMark.Core.Parsing;

public enum ComponentKind : byte
{
    /// <summary>块级容器：开始标签位于行首，内部按 Markdown 解析，直到对应的结束标签（可跨空行、可嵌套）。</summary>
    Block,
    /// <summary>成对的行内组件：<c>&lt;cite&gt;文字&lt;/cite&gt;</c>，内部按行内 Markdown 解析。</summary>
    InlinePair,
    /// <summary>自闭合的行内组件：<c>&lt;Citation id="1" /&gt;</c>（也接受不带斜杠的空元素写法）。</summary>
    SelfClosing,
}

/// <summary>块级组件内部内容的处理方式。</summary>
public enum ComponentContent : byte
{
    /// <summary>内部按 Markdown 解析，结果在 <see cref="ComponentBlock.Children"/>。</summary>
    Markdown,

    /// <summary>
    /// 内部原样保留（数据而非 Markdown，例如 <c>&lt;tool-call&gt;{json}&lt;/tool-call&gt;</c>），结果在 <see cref="ComponentBlock.Raw"/>。
    /// 遇到第一个结束标签即结束（不支持同名嵌套）；结束标签须在行尾（独占一行，或跟在内容后面）。
    /// </summary>
    Raw,
}

/// <summary>白名单 HTML 组件的声明。标签名不区分大小写。</summary>
public sealed record ComponentSpec(string Name, ComponentKind Kind)
{
    /// <summary>块级组件内部内容的处理方式（只对 <see cref="ComponentKind.Block"/> 有效）。</summary>
    public ComponentContent Content { get; init; } = ComponentContent.Markdown;

    /// <summary>内置组件：折叠块、推理块、按键 / 上下标 / 高亮 / 下划线、引用、图片。</summary>
    public static ImmutableArray<ComponentSpec> BuiltIn { get; } =
    [
        new("details", ComponentKind.Block),
        new("summary", ComponentKind.Block),
        new("think", ComponentKind.Block),
        new("kbd", ComponentKind.InlinePair),
        new("sub", ComponentKind.InlinePair),
        new("sup", ComponentKind.InlinePair),
        new("mark", ComponentKind.InlinePair),
        new("u", ComponentKind.InlinePair),
        new("cite", ComponentKind.InlinePair),
        new("citation", ComponentKind.SelfClosing),
        new("img", ComponentKind.SelfClosing),
    ];

    /// <summary>映射为行内样式（而不是组件）的内置标签。</summary>
    internal static InlineStyle? StyleFor(string name) => name.ToLowerInvariant() switch
    {
        "kbd" => InlineStyle.Keyboard,
        "sub" => InlineStyle.Subscript,
        "sup" => InlineStyle.Superscript,
        "mark" => InlineStyle.Mark,
        "u" => InlineStyle.Underline,
        _ => null,
    };
}

/// <summary>白名单组件表（按小写名称查找）。</summary>
internal sealed class ComponentTable
{
    private readonly Dictionary<string, ComponentSpec> _specs = new(StringComparer.OrdinalIgnoreCase);

    public ComponentTable(IEnumerable<ComponentSpec> specs)
    {
        foreach (var spec in specs)
        {
            _specs[spec.Name] = spec;
        }
        BlockNames = [.. _specs.Values.Where(s => s.Kind == ComponentKind.Block && s.Content == ComponentContent.Markdown).Select(s => s.Name.ToLowerInvariant())];
        RawBlockNames = [.. _specs.Values.Where(s => s.Kind == ComponentKind.Block && s.Content == ComponentContent.Raw).Select(s => s.Name.ToLowerInvariant())];
    }

    /// <summary>内部按 Markdown 解析的块级组件名（小写）。</summary>
    public ImmutableArray<string> BlockNames { get; }

    /// <summary>内部原样保留的块级组件名（小写）。</summary>
    public ImmutableArray<string> RawBlockNames { get; }

    public bool TryGet(ReadOnlySpan<char> name, out ComponentSpec spec)
    {
#if NET9_0_OR_GREATER
        var lookup = _specs.GetAlternateLookup<ReadOnlySpan<char>>();
        return lookup.TryGetValue(name, out spec!);
#else
        return _specs.TryGetValue(name.ToString(), out spec!);
#endif
    }
}

/// <summary>HTML 标签的极简解析：名称、属性、是否自闭合 / 结束标签。只处理白名单组件用到的写法。</summary>
internal static class HtmlTagReader
{
    public readonly record struct Tag(string Name, bool IsClosing, bool IsSelfClosing, ComponentAttributes Attributes, int Length);

    /// <summary>从 <paramref name="text"/> 开头（必须是 '&lt;'）读取一个完整标签。</summary>
    public static bool TryRead(ReadOnlySpan<char> text, out Tag tag)
    {
        tag = default;
        if (text.Length < 3 || text[0] != '<')
        {
            return false;
        }
        var i = 1;
        var closing = false;
        if (text[i] == '/')
        {
            closing = true;
            i++;
        }
        var nameStart = i;
        while (i < text.Length && (char.IsAsciiLetterOrDigit(text[i]) || text[i] is '-' or '_' or ':'))
        {
            i++;
        }
        if (i == nameStart || !char.IsAsciiLetter(text[nameStart]))
        {
            return false;
        }
        var name = text[nameStart..i].ToString();
        var attributes = ImmutableArray.CreateBuilder<KeyValuePair<string, string>>();
        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            if (i >= text.Length)
            {
                return false;
            }
            if (text[i] == '>')
            {
                tag = new Tag(name, closing, false, new ComponentAttributes(attributes.ToImmutable()), i + 1);
                return true;
            }
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '>')
            {
                tag = new Tag(name, closing, true, new ComponentAttributes(attributes.ToImmutable()), i + 2);
                return true;
            }
            if (closing)
            {
                return false;
            }
            var attrStart = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('=' or '>' or '/'))
            {
                i++;
            }
            if (i == attrStart)
            {
                return false;
            }
            var attrName = text[attrStart..i].ToString();
            var value = "";
            if (i < text.Length && text[i] == '=')
            {
                i++;
                if (i < text.Length && text[i] is '"' or '\'')
                {
                    var quote = text[i++];
                    var valueStart = i;
                    while (i < text.Length && text[i] != quote)
                    {
                        i++;
                    }
                    if (i >= text.Length)
                    {
                        return false;
                    }
                    value = text[valueStart..i].ToString();
                    i++;
                }
                else
                {
                    var valueStart = i;
                    while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '>')
                    {
                        i++;
                    }
                    value = text[valueStart..i].ToString();
                }
            }
            attributes.Add(new KeyValuePair<string, string>(attrName, System.Net.WebUtility.HtmlDecode(value)));
        }
        return false;
    }
}
