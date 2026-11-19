using System.Collections.Immutable;

namespace StreamMark.Core.Model;

/// <summary>行内样式标志，可叠加。</summary>
[Flags]
public enum InlineStyle : ushort
{
    None = 0,
    Bold = 1 << 0,
    Italic = 1 << 1,
    Strikethrough = 1 << 2,
    Code = 1 << 3,
    Link = 1 << 4,
    Superscript = 1 << 5,
    Subscript = 1 << 6,
    Keyboard = 1 << 7,
    FootnoteReference = 1 << 8,
    /// <summary>未闭合（流式中由 remend 补全）的链接，渲染为占位样式且不可点击。</summary>
    IncompleteLink = 1 << 9,
    /// <summary>高亮（<c>&lt;mark&gt;</c>）。</summary>
    Mark = 1 << 10,
    Underline = 1 << 11,
}

/// <summary>
/// 扁平文本中的一段样式区间。<see cref="LinkIndex"/> 指向 <see cref="InlineContent.Links"/>，
/// <see cref="ComponentIndex"/> 指向 <see cref="InlineContent.Components"/>（成对的行内组件，如 <c>&lt;cite&gt;</c>），
/// <see cref="TextStyleIndex"/> 指向 <see cref="InlineContent.TextStyles"/>（HTML 的 color / font-size 等），-1 表示没有。
/// </summary>
public readonly record struct InlineSpan(int Start, int Length, InlineStyle Style, int LinkIndex = -1, int ComponentIndex = -1, int TextStyleIndex = -1)
{
    public int End => Start + Length;
}

public sealed record LinkTarget(string Url, string? Title, bool IsAutoLink);

public enum InlineObjectKind : byte
{
    Math,
    Image,
    TaskCheckbox,
    /// <summary>自闭合的行内组件（如 <c>&lt;Citation id="1" /&gt;</c>），<see cref="InlineObject.Value"/> 为组件名。</summary>
    Component,
}

/// <summary>组件属性（保持源码顺序，名称不区分大小写）。</summary>
public sealed record ComponentAttributes(ImmutableArray<KeyValuePair<string, string>> Items)
{
    public static readonly ComponentAttributes Empty = new(ImmutableArray<KeyValuePair<string, string>>.Empty);

    public string? this[string name]
    {
        get
        {
            foreach (var item in Items)
            {
                if (string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return item.Value;
                }
            }
            return null;
        }
    }

    internal void AddTo(ref Fingerprint fp)
    {
        fp.Add(Items.Length);
        foreach (var item in Items)
        {
            fp.Add(item.Key);
            fp.Add(item.Value);
        }
    }

    public bool Equals(ComponentAttributes? other) => other is not null && Items.SequenceEqual(other.Items);

    public override int GetHashCode() => Items.Length;
}

/// <summary>成对的行内组件（如 <c>&lt;cite href="…"&gt;文字&lt;/cite&gt;</c>）的名称与属性。</summary>
public sealed record InlineComponent(string Name, ComponentAttributes Attributes);

/// <summary>
/// 嵌在文本中的对象（行内公式、图片、任务复选框）。在 <see cref="InlineContent.Text"/> 中以
/// U+FFFC 占位，<see cref="Offset"/> 为占位字符的位置。
/// </summary>
public sealed record InlineObject(int Offset, InlineObjectKind Kind, string Value, string? Alt = null, string? Title = null, bool Flag = false)
{
    /// <summary>组件属性（<see cref="InlineObjectKind.Component"/> 与带属性的图片）。</summary>
    public ComponentAttributes Attributes { get; init; } = ComponentAttributes.Empty;

    /// <summary>对象占位字符。</summary>
    public const char Placeholder = '￼';
}

/// <summary>
/// 一个段落级文本单元：渲染层可以直接据此构造一个 TextLayout。
/// 文本是扁平字符串，样式以不重叠、按起点排序的区间给出；未被任何区间覆盖的文本为普通样式。
/// </summary>
public sealed record InlineContent(
    string Text,
    ImmutableArray<InlineSpan> Spans,
    ImmutableArray<LinkTarget> Links,
    ImmutableArray<InlineObject> Objects)
{
    public static readonly InlineContent Empty = new("", [], [], []);

    /// <summary>成对行内组件表，由 <see cref="InlineSpan.ComponentIndex"/> 引用。</summary>
    public ImmutableArray<InlineComponent> Components { get; init; } = [];

    /// <summary>HTML 行内文字样式表，由 <see cref="InlineSpan.TextStyleIndex"/> 引用。</summary>
    public ImmutableArray<HtmlTextStyle> TextStyles { get; init; } = [];

    public bool IsEmpty => Text.Length == 0;

    internal void AddTo(ref Fingerprint fp)
    {
        fp.Add(Text);
        fp.Add(Spans.Length);
        foreach (var span in Spans)
        {
            fp.Add(span.Start);
            fp.Add(span.Length);
            fp.Add((int)span.Style);
            fp.Add(span.LinkIndex);
            fp.Add(span.ComponentIndex);
            fp.Add(span.TextStyleIndex);
        }
        fp.Add(TextStyles.Length);
        foreach (var textStyle in TextStyles)
        {
            fp.Add(textStyle.ToString());
        }
        fp.Add(Components.Length);
        foreach (var component in Components)
        {
            fp.Add(component.Name);
            component.Attributes.AddTo(ref fp);
        }
        foreach (var link in Links)
        {
            fp.Add(link.Url);
            fp.Add(link.Title);
        }
        foreach (var obj in Objects)
        {
            fp.Add(obj.Offset);
            fp.Add((int)obj.Kind);
            fp.Add(obj.Value);
            fp.Add(obj.Alt);
            fp.Add(obj.Flag ? 1 : 0);
            obj.Attributes.AddTo(ref fp);
        }
    }
}
