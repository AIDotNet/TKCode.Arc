using System.Collections.Immutable;

namespace StreamMark.Core.Model;

/// <summary>源码区间（相对整篇 Markdown 源码的字符偏移，左闭右开）。</summary>
public readonly record struct SourceRange(int Start, int Length)
{
    public int End => Start + Length;
}

/// <summary>
/// 不可变的块模型。顶层块的 <see cref="Key"/> 为其在源码中的起始偏移：源码只追加，
/// 所以同一个块在多次重解析之间 key 保持不变，渲染层据此复用控件。
/// </summary>
public abstract record MarkdownBlock
{
    /// <summary>块在源码中的起始偏移，同时作为差异比对和界面状态的 key。</summary>
    public int Key => Range.Start;

    public SourceRange Range { get; init; }

    /// <summary>渲染内容指纹（不含 <see cref="Range"/>）：相同指纹的两个块渲染结果相同。每次调用重新计算。</summary>
    public ulong ComputeFingerprint()
    {
        var fp = Model.Fingerprint.Create();
        fp.Add(GetType().Name);
        AddContent(ref fp);
        return fp.Value;
    }

    internal abstract void AddContent(ref Fingerprint fp);

    internal static void AddChildren(ref Fingerprint fp, ImmutableArray<MarkdownBlock> children)
    {
        fp.Add(children.Length);
        foreach (var child in children)
        {
            fp.Add(child.GetType().Name);
            child.AddContent(ref fp);
        }
    }
}

public sealed record ParagraphBlock(InlineContent Content) : MarkdownBlock
{
    internal override void AddContent(ref Fingerprint fp) => Content.AddTo(ref fp);
}

public sealed record HeadingBlock(int Level, InlineContent Content, string? Id) : MarkdownBlock
{
    internal override void AddContent(ref Fingerprint fp)
    {
        fp.Add(Level);
        fp.Add(Id);
        Content.AddTo(ref fp);
    }
}

/// <summary>代码块。<see cref="IsOpen"/> 表示流式中尚未出现闭合围栏。</summary>
public sealed record CodeBlock(string? Language, string? Info, string Code, bool IsFenced, bool IsOpen) : MarkdownBlock
{
    internal override void AddContent(ref Fingerprint fp)
    {
        fp.Add(Info);
        fp.Add(Code);
        fp.Add(IsFenced ? 1 : 0);
        fp.Add(IsOpen ? 1 : 0);
    }
}

/// <summary>块级公式（<c>$$…$$</c> 或 <c>\[…\]</c>）。</summary>
public sealed record MathBlock(string Latex, bool IsOpen) : MarkdownBlock
{
    internal override void AddContent(ref Fingerprint fp)
    {
        fp.Add(Latex);
        fp.Add(IsOpen ? 1 : 0);
    }
}

public sealed record QuoteBlock(ImmutableArray<MarkdownBlock> Children) : MarkdownBlock
{
    internal override void AddContent(ref Fingerprint fp) => AddChildren(ref fp, Children);
}

/// <summary>GitHub 提示块（<c>&gt; [!NOTE]</c> 等），<see cref="Kind"/> 为大写类型名。</summary>
public sealed record AlertBlock(string Kind, ImmutableArray<MarkdownBlock> Children) : MarkdownBlock
{
    internal override void AddContent(ref Fingerprint fp)
    {
        fp.Add(Kind);
        AddChildren(ref fp, Children);
    }
}

public sealed record ListBlock(bool IsOrdered, int StartNumber, char Bullet, bool IsLoose, ImmutableArray<ListItemBlock> Items) : MarkdownBlock
{
    internal override void AddContent(ref Fingerprint fp)
    {
        fp.Add(IsOrdered ? 1 : 0);
        fp.Add(StartNumber);
        fp.Add(Bullet);
        fp.Add(IsLoose ? 1 : 0);
        fp.Add(Items.Length);
        foreach (var item in Items)
        {
            item.AddContent(ref fp);
        }
    }
}

/// <summary>列表项。<see cref="Task"/> 为 null 表示不是任务项。</summary>
public sealed record ListItemBlock(bool? Task, ImmutableArray<MarkdownBlock> Children) : MarkdownBlock
{
    /// <summary>
    /// 任务标记 <c>[ ]</c> / <c>[x]</c> 在整篇源码中的区间（共 3 个字符，偏移单位与 <see cref="MarkdownBlock.Range"/> 相同）；
    /// 不是任务项时长度为 0。
    /// </summary>
    public SourceRange TaskMarker { get; init; }

    internal override void AddContent(ref Fingerprint fp)
    {
        fp.Add(Task is null ? -1 : Task.Value ? 1 : 0);
        // 记相对列表项起点的位置：整体平移不改变指纹，标记在项内的位置变了（例如项目符号后多了空格）则视为内容变化
        fp.Add(TaskMarker.Length == 0 ? -1 : TaskMarker.Start - Range.Start);
        fp.Add(TaskMarker.Length);
        AddChildren(ref fp, Children);
    }
}

public enum TableAlignment : byte
{
    None,
    Left,
    Center,
    Right,
}

public sealed record TableRow(bool IsHeader, ImmutableArray<InlineContent> Cells);

public sealed record TableBlock(ImmutableArray<TableAlignment> Columns, ImmutableArray<TableRow> Rows) : MarkdownBlock
{
    internal override void AddContent(ref Fingerprint fp)
    {
        fp.Add(Columns.Length);
        foreach (var column in Columns)
        {
            fp.Add((int)column);
        }
        fp.Add(Rows.Length);
        foreach (var row in Rows)
        {
            fp.Add(row.IsHeader ? 1 : 0);
            fp.Add(row.Cells.Length);
            foreach (var cell in row.Cells)
            {
                cell.AddTo(ref fp);
            }
        }
    }
}

public sealed record ThematicBreakBlock : MarkdownBlock
{
    internal override void AddContent(ref Fingerprint fp)
    {
    }
}

/// <summary>原始 HTML 块。渲染层只解释白名单标签，其余按纯文本显示。</summary>
public sealed record HtmlBlock(string Html) : MarkdownBlock
{
    internal override void AddContent(ref Fingerprint fp) => fp.Add(Html);
}

/// <summary>
/// 由原始 HTML 转换来的盒子（div、p、center、section 等，或一段 HTML 中的多个顶层元素）：
/// 盒子样式（外边距、内边距、背景、边框、宽度）作用于自身，文字样式由其中所有块继承。
/// </summary>
public sealed record HtmlBoxBlock(string Tag, HtmlBoxStyle Box, HtmlTextStyle Text, ImmutableArray<MarkdownBlock> Children) : MarkdownBlock
{
    internal override void AddContent(ref Fingerprint fp)
    {
        fp.Add(Tag);
        fp.Add(Box.ToString());
        fp.Add(Text.ToString());
        AddChildren(ref fp, Children);
    }
}

public sealed record FootnoteDefinition(string Label, int Order, ImmutableArray<MarkdownBlock> Children);

/// <summary>文末脚注组。</summary>
public sealed record FootnotesBlock(ImmutableArray<FootnoteDefinition> Footnotes) : MarkdownBlock
{
    internal override void AddContent(ref Fingerprint fp)
    {
        fp.Add(Footnotes.Length);
        foreach (var footnote in Footnotes)
        {
            fp.Add(footnote.Label);
            fp.Add(footnote.Order);
            AddChildren(ref fp, footnote.Children);
        }
    }
}

public sealed record DefinitionItem(ImmutableArray<InlineContent> Terms, ImmutableArray<MarkdownBlock> Definitions);

public sealed record DefinitionListBlock(ImmutableArray<DefinitionItem> Items) : MarkdownBlock
{
    internal override void AddContent(ref Fingerprint fp)
    {
        fp.Add(Items.Length);
        foreach (var item in Items)
        {
            fp.Add(item.Terms.Length);
            foreach (var term in item.Terms)
            {
                term.AddTo(ref fp);
            }
            AddChildren(ref fp, item.Definitions);
        }
    }
}

/// <summary>
/// 块级 HTML 组件（白名单标签，如 <c>&lt;details&gt;</c>、<c>&lt;think&gt;</c> 或插件注册的标签）。
/// 内部内容按 Markdown 解析时在 <see cref="Children"/>；组件声明为 <c>ComponentContent.Raw</c> 时原样放在 <see cref="Raw"/>，
/// <see cref="Children"/> 为空。插件也可以自己构造它，作为自定义块的载体。
/// </summary>
public sealed record ComponentBlock(string Name, ComponentAttributes Attributes, ImmutableArray<MarkdownBlock> Children) : MarkdownBlock
{
    /// <summary>原样保留的内部文本（行之间用 <c>\n</c>）；内容按 Markdown 解析的组件为 null。</summary>
    public string? Raw { get; init; }

    /// <summary>流式中尚未出现结束标签（与 <see cref="CodeBlock.IsOpen"/> 同义）。没有结束标签就随外层块结束的组件也是 true。</summary>
    public bool IsOpen { get; init; }

    internal override void AddContent(ref Fingerprint fp)
    {
        fp.Add(Name);
        Attributes.AddTo(ref fp);
        fp.Add(Raw);
        fp.Add(IsOpen ? 1 : 0);
        AddChildren(ref fp, Children);
    }
}

/// <summary><c>:::</c> 自定义容器，<see cref="Info"/> 为开头围栏后的信息串（如 <c>warning</c>）。</summary>
public sealed record CustomContainerBlock(string? Info, string? Arguments, ImmutableArray<MarkdownBlock> Children) : MarkdownBlock
{
    internal override void AddContent(ref Fingerprint fp)
    {
        fp.Add(Info);
        fp.Add(Arguments);
        AddChildren(ref fp, Children);
    }
}
