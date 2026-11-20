using System.Collections.Immutable;
using StreamMark.Core.Html;
using StreamMark.Core.Model;

namespace StreamMark.Core.Parsing;

/// <summary>叶块中保留的一行切片元信息（直接引用 <see cref="SourceBuffer"/> 的绝对偏移，零拷贝）。</summary>
internal readonly record struct BlockLineSlice(int Start, int Length, int LineIndex);

/// <summary>内部可变块节点基类：闭合后缓存不可变 <see cref="MarkdownBlock"/>，实现容器内增量复用。</summary>
internal abstract class BlockNode
{
    public ContainerNode? Parent;
    public int StartOffset;
    public int EndOffset;
    public int StartLine;
    public int EndLine;
    public bool IsOpen = true;
    public bool Dirty = true;
    public MarkdownBlock? CachedModel;

    public SourceRange Range => new(StartOffset, Math.Max(0, EndOffset - StartOffset));

    public void MarkDirtyUpwards()
    {
        var cur = this;
        while (cur is not null)
        {
            cur.Dirty = true;
            cur.CachedModel = null;
            cur = cur.Parent;
        }
    }
}

internal abstract class ContainerNode : BlockNode
{
    public readonly List<BlockNode> Children = [];
    public bool LastChildEndedWithBlankLine;

    public void AddChild(BlockNode child)
    {
        child.Parent = this;
        Children.Add(child);
        Dirty = true;
        CachedModel = null;
    }

    public BlockNode? LastChild => Children.Count > 0 ? Children[^1] : null;
}

internal sealed class DocumentNode : ContainerNode
{
    public DocumentNode()
    {
        StartOffset = 0;
        EndOffset = 0;
    }
}

internal sealed class ParagraphNode : BlockNode
{
    public readonly List<BlockLineSlice> Lines = [];
    public bool MaybeDefinition;
    public bool LeadingDefsChecked;

    /// <summary>该段落在列表项首段时剥离的任务复选框状态（不通过占位对象往返）。</summary>
    public bool? TaskChecked;
    public SourceRange TaskMarker;
    public int TaskPrefixSkip;
}

internal sealed class HeadingNode : BlockNode
{
    public int Level;
    public bool IsSetext;
    public readonly List<BlockLineSlice> Lines = [];
    public string? AllocatedId;
}

internal sealed class FencedCodeNode : BlockNode
{
    public char FenceChar;
    public int FenceLength;
    public int Indent;
    public string? Info;
    public string? Language;
    public bool SawClose;
    public readonly OpenBlockBuffer Buffer = new();
}

internal sealed class IndentedCodeNode : BlockNode
{
    public readonly OpenBlockBuffer Buffer = new();
}

internal sealed class MathBlockNode : BlockNode
{
    /// <summary>true 表示 <c>\[...\]</c>，false 表示 <c>$$...$$</c>。</summary>
    public bool IsBracket;
    public bool SawClose;
    public readonly OpenBlockBuffer Buffer = new();
}

internal sealed class ThematicBreakNode : BlockNode;

internal sealed class QuoteNode : ContainerNode
{
    /// <summary>非 null 时表示 GitHub 提示块（<c>&gt; [!NOTE]</c> 等大写种类名）。</summary>
    public string? AlertKind;
    public bool AlertChecked;
}

internal sealed class ListNode : ContainerNode
{
    public bool IsOrdered;
    public int StartNumber = 1;
    public char Bullet;
    public bool IsLoose;
    public bool SawBlankLineBetweenItems;

    public bool Matches(bool isOrdered, char bullet) => IsOrdered == isOrdered && Bullet == bullet;
}

internal sealed class ListItemNode : ContainerNode
{
    /// <summary>子块续行所需的内容缩进列数（CommonMark §5.2）。</summary>
    public int ContentColumn;
    /// <summary>列表项是否以空行开局（空列表项只允许一行空行）。</summary>
    public bool StartedEmpty;
    public bool SawBlankLine;
    public bool HadContentAfterMarker;
    public ListItemBlock? CachedItem;
}

/// <summary>GFM 管道表格的已缓存行节点：行闭合后永久缓存其不可变 <see cref="TableRow"/>，流式追加新行时前面行 O(1) 引用复用。</summary>
internal sealed class TableRowNode
{
    public bool IsHeader;
    public BlockLineSlice Line;
    public bool IsClosed;
    public TableRow? CachedRow;
}

internal sealed class TableNode : BlockNode
{
    public ImmutableArray<TableAlignment> Columns;
    public readonly List<TableRowNode> Rows = [];
}

internal sealed class CustomContainerNode : ContainerNode
{
    public int FenceLength;
    public string? Info;
    public string? Arguments;
    public bool SawClose;
}

internal sealed class DefinitionItemNode
{
    public readonly List<BlockLineSlice> TermLines = [];
    public readonly List<BlockNode> Definitions = [];
    public bool Dirty = true;
    public DefinitionItem? CachedItem;
}

internal sealed class DefinitionListNode : ContainerNode
{
    public readonly List<DefinitionItemNode> Items = [];
    public int ActiveItemIndex => Items.Count - 1;
}

internal sealed class FootnoteDefNode : ContainerNode
{
    public required string RawLabel;
    public required string NormalizedLabel;
    public int ContentColumn = 4;
    public bool SawBlankLine;
}

internal enum CommonMarkHtmlBlockKind : byte
{
    Type1RawTag = 1,      // <pre>, <script>, <style>, <textarea>
    Type2Comment = 2,     // <!-- ... -->
    Type3Processing = 3,  // <? ... ?>
    Type4Declaration = 4, // <!X ... >
    Type5CData = 5,       // <![CDATA[ ... ]]>
    Type6BlockTag = 6,    // <div>, <table>, ... ends at blank line
    Type7CompleteTag = 7, // single complete open/close tag on line, ends at blank line
}

internal sealed class HtmlBlockNode : BlockNode
{
    public CommonMarkHtmlBlockKind HtmlKind;
    public string? RawTagName;
    public readonly OpenBlockBuffer Buffer = new();
}

internal sealed class ComponentNode : ContainerNode
{
    public required string Name;
    public ComponentAttributes Attributes = ComponentAttributes.Empty;
    public bool IsRaw;
    public bool CloseAfterLine;
    public bool SawClose;
    public readonly OpenBlockBuffer RawBuffer = new();
}

internal sealed class CustomRuleBlockNode : ContainerNode
{
    public required IBlockSyntaxRule Rule;
    public BlockRuleState State;
    public readonly OpenBlockBuffer RawBuffer = new();
}

/// <summary>
/// 兄弟块中的原始 HTML 与交错 Markdown 拼装器（复用现有 <see cref="HtmlTreeBuilder"/> 与 <see cref="HtmlMapper"/>，
/// 与 <c>HtmlTests</c> 保持 100% 行为一致）。
/// </summary>
internal sealed class HtmlSiblingStitcher
{
    private readonly List<MarkdownBlock> _fallback = [];
    private SourceRange _range;
    private bool _hasRange;

    public HtmlTreeBuilder Tree { get; } = new();

    public void AddHtml(string source, SourceRange range)
    {
        Extend(range);
        Tree.Feed(source);
        _fallback.Add(new HtmlBlock(source) { Range = range });
    }

    public void AddMarkdown(MarkdownBlock? block, SourceRange range)
    {
        Extend(range);
        if (block is null)
        {
            return;
        }
        Tree.AddMarkdown(block);
        _fallback.Add(block);
    }

    private void Extend(SourceRange range)
    {
        if (!_hasRange)
        {
            _range = range;
            _hasRange = true;
            return;
        }
        var start = Math.Min(_range.Start, range.Start);
        _range = new SourceRange(start, Math.Max(_range.End, range.End) - start);
    }

    public void Finish(ImmutableArray<MarkdownBlock>.Builder output)
    {
        if (Tree.HasUnknownElements)
        {
            output.AddRange(_fallback);
            return;
        }
        var blocks = HtmlMapper.MapChildren(Tree.Root);
        if (!Tree.HasElements && blocks.All(b => b is ParagraphBlock))
        {
            foreach (var fb in _fallback)
            {
                if (fb is not HtmlBlock h || !IsCommentOnly(h.Html))
                {
                    output.Add(fb);
                }
            }
            return;
        }
        switch (blocks.Length)
        {
            case 0:
                return;
            case 1:
                output.Add(blocks[0] with { Range = _range });
                return;
            default:
                output.Add(new HtmlBoxBlock("#fragment", HtmlBoxStyle.Default, HtmlTextStyle.Default, blocks) { Range = _range });
                return;
        }
    }

    private static bool IsCommentOnly(string html)
    {
        var text = html.AsSpan().Trim();
        while (text.StartsWith("<!--", StringComparison.Ordinal))
        {
            var end = text.IndexOf("-->", StringComparison.Ordinal);
            if (end < 0)
            {
                return true;
            }
            text = text[(end + 3)..].TrimStart();
        }
        return text.IsEmpty;
    }
}
