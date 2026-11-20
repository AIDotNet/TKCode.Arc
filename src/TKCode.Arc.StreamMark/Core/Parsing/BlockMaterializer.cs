using System.Collections.Immutable;
using StreamMark.Core.Model;
using StreamMark.Core.Remend;
using RemendHealer = StreamMark.Core.Remend.Remend;

namespace StreamMark.Core.Parsing;

/// <summary>
/// 将增量块树物化为不可变 <see cref="MarkdownBlock"/> 模型：
/// 已闭合的子块、表格行、列表项命中节点级缓存 O(1) 引用复用；
/// 仅对流式末尾唯一活跃叶节点执行轻量 <see cref="Remend"/> 行内修补。
/// </summary>
internal sealed class BlockMaterializer
{
    private readonly MarkdownProfile _profile;
    private readonly InlineParser _inlineParser = new();
    private readonly List<string> _tableCellScratch = [];
    private char[] _lineScratch = new char[256];
    private char[] _joinScratch = new char[512];

    public BlockMaterializer(MarkdownProfile profile)
    {
        _profile = profile;
    }

    public ImmutableArray<MarkdownBlock> MaterializeSiblings(
        IReadOnlyList<BlockNode> nodes,
        int startIndex,
        int count,
        SourceBuffer source,
        int sourceLength,
        ReferenceRegistry references,
        BlockParser blockParser,
        RemendOptions remendOptions,
        bool isStreaming,
        BlockNode? activeTailLeaf,
        HashSet<int>? affectedTopKeys,
        int topKeyOverride = -1)
    {
        var builder = ImmutableArray.CreateBuilder<MarkdownBlock>(count);
        HtmlSiblingStitcher? htmlGroup = null;

        for (var i = 0; i < count; i++)
        {
            var node = nodes[startIndex + i];
            var topKey = topKeyOverride >= 0 ? topKeyOverride : node.StartOffset;

            if (node is FootnoteDefNode fn)
            {
                var fnChildren = MaterializeSiblings(fn.Children, 0, fn.Children.Count, source, sourceLength, references, blockParser, remendOptions, isStreaming, activeTailLeaf, affectedTopKeys, topKey);
                references.UpsertFootnote(fn.RawLabel, fn.Range, fnChildren, affectedTopKeys);
                continue;
            }

            if (node is HtmlBlockNode html)
            {
                htmlGroup ??= new HtmlSiblingStitcher();
                htmlGroup.AddHtml(html.Buffer.GetString(), html.Range);
                if (!htmlGroup.Tree.HasOpenElements)
                {
                    htmlGroup.Finish(builder);
                    htmlGroup = null;
                }
                continue;
            }

            var mapped = MaterializeNode(node, source, sourceLength, references, blockParser, remendOptions, isStreaming, activeTailLeaf, affectedTopKeys, topKey);
            if (htmlGroup is not null)
            {
                htmlGroup.AddMarkdown(mapped, node.Range);
            }
            else if (mapped is not null)
            {
                builder.Add(mapped);
            }
        }

        htmlGroup?.Finish(builder);
        return builder.ToImmutable();
    }

    public MarkdownBlock? MaterializeNode(
        BlockNode node,
        SourceBuffer source,
        int sourceLength,
        ReferenceRegistry references,
        BlockParser blockParser,
        RemendOptions remendOptions,
        bool isStreaming,
        BlockNode? activeTailLeaf,
        HashSet<int>? affectedTopKeys,
        int topKey)
    {
        var isActiveLeaf = isStreaming && ReferenceEquals(node, activeTailLeaf);
        if (!node.IsOpen && !node.Dirty && !isActiveLeaf && node.CachedModel is { } cached)
        {
            return cached;
        }

        MarkdownBlock? result = node switch
        {
            ParagraphNode para => MaterializeParagraph(para, source, references, blockParser, remendOptions, isActiveLeaf, affectedTopKeys, topKey, skipFirstLinePrefix: 0, startLineIndex: 0),
            HeadingNode heading => MaterializeHeading(heading, source, references, remendOptions, isActiveLeaf, topKey),
            FencedCodeNode fenced => MaterializeFencedCode(fenced, sourceLength, isStreaming),
            IndentedCodeNode indented => new CodeBlock(null, null, indented.Buffer.GetIndentedCode(), IsFenced: false, IsOpen: false) { Range = indented.Range },
            MathBlockNode math => MaterializeMath(math, sourceLength),
            ThematicBreakNode hr => new ThematicBreakBlock { Range = hr.Range },
            QuoteNode quote => MaterializeQuote(quote, source, sourceLength, references, blockParser, remendOptions, isStreaming, activeTailLeaf, affectedTopKeys, topKey),
            ListNode list => MaterializeList(list, source, sourceLength, references, blockParser, remendOptions, isStreaming, activeTailLeaf, affectedTopKeys, topKey),
            TableNode table => MaterializeTable(table, source, references, remendOptions, isStreaming, isActiveLeaf, topKey),
            CustomContainerNode cc => new CustomContainerBlock(
                cc.Info,
                cc.Arguments,
                MaterializeSiblings(cc.Children, 0, cc.Children.Count, source, sourceLength, references, blockParser, remendOptions, isStreaming, activeTailLeaf, affectedTopKeys, topKey))
            {
                Range = cc.Range,
            },
            DefinitionListNode dl => MaterializeDefinitionList(dl, source, sourceLength, references, blockParser, remendOptions, isStreaming, activeTailLeaf, affectedTopKeys, topKey),
            ComponentNode comp => MaterializeComponent(comp, source, sourceLength, references, blockParser, remendOptions, isStreaming, activeTailLeaf, affectedTopKeys, topKey),
            CustomRuleBlockNode custom => MaterializeCustomRule(custom, source, sourceLength, references, blockParser, remendOptions, isStreaming, activeTailLeaf, affectedTopKeys, topKey),
            HtmlBlockNode html => new HtmlBlock(html.Buffer.GetString()) { Range = html.Range },
            _ => null,
        };

        if (!node.IsOpen && !isActiveLeaf && result is not null)
        {
            node.CachedModel = result;
            node.Dirty = false;
        }
        return result;
    }

    private ParagraphBlock? MaterializeParagraph(
        ParagraphNode para,
        SourceBuffer source,
        ReferenceRegistry references,
        BlockParser blockParser,
        RemendOptions remendOptions,
        bool isActiveLeaf,
        HashSet<int>? affectedTopKeys,
        int topKey,
        int skipFirstLinePrefix,
        int startLineIndex)
    {
        if (para.MaybeDefinition && !para.LeadingDefsChecked)
        {
            blockParser.ConsumeLeadingDefinitions(para, source, references, affectedTopKeys);
        }
        if (para.Lines.Count <= startLineIndex)
        {
            return null;
        }
        var span = JoinLines(source, para.Lines, startLineIndex, skipFirstLinePrefix);
        var content = ParseInline(span, references, remendOptions, isActiveLeaf, topKey);
        var range = startLineIndex == 0
            ? para.Range
            : new SourceRange(para.Lines[startLineIndex].Start, Math.Max(0, para.EndOffset - para.Lines[startLineIndex].Start));
        return new ParagraphBlock(content) { Range = range };
    }

    private HeadingBlock MaterializeHeading(
        HeadingNode heading,
        SourceBuffer source,
        ReferenceRegistry references,
        RemendOptions remendOptions,
        bool isActiveLeaf,
        int topKey)
    {
        var span = JoinLines(source, heading.Lines, startIndex: 0, skipFirstLinePrefix: 0);
        var content = ParseInline(span, references, remendOptions, isActiveLeaf, topKey);
        string? id = null;
        if (_profile.EnableAutoIdentifiers)
        {
            if (!heading.IsOpen && !isActiveLeaf)
            {
                id = heading.AllocatedId ??= references.AllocateHeadingId(content.Text.AsSpan());
            }
            else
            {
                // 流式未完成标题直接计算当前预览 slug，不污染全局去重计数
                var preview = UnicodeClassification.ToGitHubSlug(content.Text.AsSpan());
                id = preview.Length == 0 ? null : preview;
            }
        }
        return new HeadingBlock(heading.Level, content, id) { Range = heading.Range };
    }

    private static CodeBlock MaterializeFencedCode(FencedCodeNode fenced, int sourceLength, bool isStreaming)
    {
        var isOpen = !fenced.SawClose;
        string code;
        int lineCount;
        if (isOpen && isStreaming)
        {
            code = fenced.Buffer.GetTrimmedStreamingCode(out lineCount);
        }
        else
        {
            code = fenced.Buffer.GetString();
            lineCount = Math.Max(1, fenced.Buffer.LineCount);
        }
        var range = isOpen
            ? new SourceRange(fenced.StartOffset, Math.Max(0, sourceLength - fenced.StartOffset))
            : fenced.Range;
        return new CodeBlock(fenced.Language, fenced.Info, code, IsFenced: true, IsOpen: isOpen) { Range = range }.WithCachedLineCount(lineCount);
    }

    private static MathBlock MaterializeMath(MathBlockNode math, int sourceLength)
    {
        var isOpen = !math.SawClose;
        var range = isOpen
            ? new SourceRange(math.StartOffset, Math.Max(0, sourceLength - math.StartOffset))
            : math.Range;
        return new MathBlock(math.Buffer.GetString(), IsOpen: isOpen) { Range = range };
    }

    private MarkdownBlock MaterializeQuote(
        QuoteNode quote,
        SourceBuffer source,
        int sourceLength,
        ReferenceRegistry references,
        BlockParser blockParser,
        RemendOptions remendOptions,
        bool isStreaming,
        BlockNode? activeTailLeaf,
        HashSet<int>? affectedTopKeys,
        int topKey)
    {
        if (_profile.EnableAlertBlocks && quote.Children.Count > 0 && quote.Children[0] is ParagraphNode firstPara && firstPara.Lines.Count > 0)
        {
            var line0 = firstPara.Lines[0];
            var line0Span = source.GetSpan(line0.Start, line0.Length, ref _lineScratch);
            if (BlockMatchers.TryMatchAlertKind(line0Span, out var alertKind))
            {
                var builder = ImmutableArray.CreateBuilder<MarkdownBlock>(quote.Children.Count);
                if (firstPara.Lines.Count > 1)
                {
                    var isFirstActive = isStreaming && ReferenceEquals(firstPara, activeTailLeaf);
                    if (MaterializeParagraph(firstPara, source, references, blockParser, remendOptions, isFirstActive, affectedTopKeys, topKey, skipFirstLinePrefix: 0, startLineIndex: 1) is { } restPara)
                    {
                        builder.Add(restPara);
                    }
                }
                if (quote.Children.Count > 1)
                {
                    var tail = MaterializeSiblings(quote.Children, 1, quote.Children.Count - 1, source, sourceLength, references, blockParser, remendOptions, isStreaming, activeTailLeaf, affectedTopKeys, topKey);
                    builder.AddRange(tail);
                }
                return new AlertBlock(alertKind, builder.ToImmutable()) { Range = quote.Range };
            }
        }

        var children = MaterializeSiblings(quote.Children, 0, quote.Children.Count, source, sourceLength, references, blockParser, remendOptions, isStreaming, activeTailLeaf, affectedTopKeys, topKey);
        return new QuoteBlock(children) { Range = quote.Range };
    }

    private ListBlock MaterializeList(
        ListNode list,
        SourceBuffer source,
        int sourceLength,
        ReferenceRegistry references,
        BlockParser blockParser,
        RemendOptions remendOptions,
        bool isStreaming,
        BlockNode? activeTailLeaf,
        HashSet<int>? affectedTopKeys,
        int topKey)
    {
        var items = ImmutableArray.CreateBuilder<ListItemBlock>(list.Children.Count);
        for (var i = 0; i < list.Children.Count; i++)
        {
            if (list.Children[i] is not ListItemNode item)
            {
                continue;
            }
            var itemContainsActiveLeaf = isStreaming && activeTailLeaf is not null && IsDescendantOrSelf(activeTailLeaf, item);
            if (!item.IsOpen && !item.Dirty && !itemContainsActiveLeaf && item.CachedItem is { } cachedItem)
            {
                items.Add(cachedItem);
                continue;
            }

            bool? task = null;
            var taskMarker = default(SourceRange);
            var skipPrefix = 0;

            if (_profile.EnableTaskLists && item.Children.Count > 0 && item.Children[0] is ParagraphNode firstPara && firstPara.Lines.Count > 0)
            {
                var line0 = firstPara.Lines[0];
                var line0Span = source.GetSpan(line0.Start, line0.Length, ref _lineScratch);
                if (TryMatchTaskMarker(line0Span, out var isChecked, out var prefixLen))
                {
                    task = isChecked;
                    taskMarker = new SourceRange(line0.Start, 3);
                    skipPrefix = prefixLen;
                }
            }

            ImmutableArray<MarkdownBlock> children;
            if (skipPrefix > 0 && item.Children[0] is ParagraphNode taskPara)
            {
                var childBuilder = ImmutableArray.CreateBuilder<MarkdownBlock>(item.Children.Count);
                var isFirstActive = isStreaming && ReferenceEquals(taskPara, activeTailLeaf);
                var p = MaterializeParagraph(taskPara, source, references, blockParser, remendOptions, isFirstActive, affectedTopKeys, topKey, skipFirstLinePrefix: skipPrefix, startLineIndex: 0);
                childBuilder.Add(p ?? new ParagraphBlock(InlineContent.Empty) { Range = taskPara.Range });
                if (item.Children.Count > 1)
                {
                    childBuilder.AddRange(MaterializeSiblings(item.Children, 1, item.Children.Count - 1, source, sourceLength, references, blockParser, remendOptions, isStreaming, activeTailLeaf, affectedTopKeys, topKey));
                }
                children = childBuilder.ToImmutable();
            }
            else
            {
                children = MaterializeSiblings(item.Children, 0, item.Children.Count, source, sourceLength, references, blockParser, remendOptions, isStreaming, activeTailLeaf, affectedTopKeys, topKey);
            }

            var built = new ListItemBlock(task, children) { Range = item.Range, TaskMarker = taskMarker };
            if (!item.IsOpen && !itemContainsActiveLeaf)
            {
                item.CachedItem = built;
                item.Dirty = false;
            }
            items.Add(built);
        }

        return new ListBlock(list.IsOrdered, list.StartNumber, list.Bullet, list.IsLoose, items.ToImmutable()) { Range = list.Range };
    }

    private static bool TryMatchTaskMarker(ReadOnlySpan<char> line, out bool isChecked, out int prefixLen)
    {
        isChecked = false;
        prefixLen = 0;
        if (line.Length >= 3 && line[0] == '[' && line[2] == ']' && line[1] is ' ' or 'x' or 'X')
        {
            if (line.Length == 3)
            {
                isChecked = line[1] is 'x' or 'X';
                prefixLen = 3;
                return true;
            }
            if (line[3] is ' ' or '\t')
            {
                isChecked = line[1] is 'x' or 'X';
                prefixLen = 4;
                return true;
            }
        }
        return false;
    }

    private TableBlock MaterializeTable(
        TableNode table,
        SourceBuffer source,
        ReferenceRegistry references,
        RemendOptions remendOptions,
        bool isStreaming,
        bool isActiveLeaf,
        int topKey)
    {
        var colCount = table.Columns.Length;
        var rows = ImmutableArray.CreateBuilder<TableRow>(table.Rows.Count);
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var rowNode = table.Rows[r];
            var isLastActiveRow = isActiveLeaf && r == table.Rows.Count - 1 && !rowNode.IsClosed;
            if (rowNode.IsClosed && !isLastActiveRow && rowNode.CachedRow is { } cachedRow)
            {
                rows.Add(cachedRow);
                continue;
            }

            var lineSpan = source.GetSpan(rowNode.Line.Start, rowNode.Line.Length, ref _lineScratch);
            BlockMatchers.SplitTableRowCells(lineSpan, _tableCellScratch);
            var cellCount = colCount;
            var cells = ImmutableArray.CreateBuilder<InlineContent>(cellCount);
            for (var c = 0; c < cellCount; c++)
            {
                if (c < _tableCellScratch.Count)
                {
                    var cellText = _tableCellScratch[c];
                    var healCell = isLastActiveRow && c == _tableCellScratch.Count - 1;
                    cells.Add(ParseInline(cellText.AsSpan(), references, remendOptions, healCell, topKey));
                }
                else
                {
                    cells.Add(InlineContent.Empty);
                }
            }
            var built = new TableRow(rowNode.IsHeader, cells.MoveToImmutable());
            if (rowNode.IsClosed && !isLastActiveRow)
            {
                rowNode.CachedRow = built;
            }
            rows.Add(built);
        }
        return new TableBlock(table.Columns, rows.MoveToImmutable()) { Range = table.Range };
    }

    private DefinitionListBlock MaterializeDefinitionList(
        DefinitionListNode dl,
        SourceBuffer source,
        int sourceLength,
        ReferenceRegistry references,
        BlockParser blockParser,
        RemendOptions remendOptions,
        bool isStreaming,
        BlockNode? activeTailLeaf,
        HashSet<int>? affectedTopKeys,
        int topKey)
    {
        var items = ImmutableArray.CreateBuilder<DefinitionItem>(dl.Items.Count);
        for (var i = 0; i < dl.Items.Count; i++)
        {
            var item = dl.Items[i];
            var isActive = isStreaming && i == dl.Items.Count - 1;
            if (!isActive && !item.Dirty && item.CachedItem is { } cached)
            {
                items.Add(cached);
                continue;
            }
            var terms = ImmutableArray.CreateBuilder<InlineContent>(item.TermLines.Count);
            for (var t = 0; t < item.TermLines.Count; t++)
            {
                var slice = item.TermLines[t];
                var span = source.GetSpan(slice.Start, slice.Length, ref _lineScratch);
                terms.Add(ParseInline(span, references, remendOptions, healActiveLeaf: false, topKey));
            }
            var defs = MaterializeSiblings(item.Definitions, 0, item.Definitions.Count, source, sourceLength, references, blockParser, remendOptions, isStreaming, activeTailLeaf, affectedTopKeys, topKey);
            var built = new DefinitionItem(terms.MoveToImmutable(), defs);
            if (!isActive)
            {
                item.CachedItem = built;
                item.Dirty = false;
            }
            items.Add(built);
        }
        return new DefinitionListBlock(items.MoveToImmutable()) { Range = dl.Range };
    }

    private ComponentBlock MaterializeComponent(
        ComponentNode comp,
        SourceBuffer source,
        int sourceLength,
        ReferenceRegistry references,
        BlockParser blockParser,
        RemendOptions remendOptions,
        bool isStreaming,
        BlockNode? activeTailLeaf,
        HashSet<int>? affectedTopKeys,
        int topKey)
    {
        var isOpen = !comp.SawClose;
        var range = isOpen
            ? new SourceRange(comp.StartOffset, Math.Max(0, sourceLength - comp.StartOffset))
            : comp.Range;
        if (comp.IsRaw)
        {
            return new ComponentBlock(comp.Name.ToLowerInvariant(), comp.Attributes, [])
            {
                Raw = comp.RawBuffer.GetString(),
                IsOpen = isOpen,
                Range = range,
            };
        }
        var children = MaterializeSiblings(comp.Children, 0, comp.Children.Count, source, sourceLength, references, blockParser, remendOptions, isStreaming, activeTailLeaf, affectedTopKeys, topKey);
        return new ComponentBlock(comp.Name.ToLowerInvariant(), comp.Attributes, children)
        {
            IsOpen = isOpen,
            Range = range,
        };
    }

    private MarkdownBlock MaterializeCustomRule(
        CustomRuleBlockNode custom,
        SourceBuffer source,
        int sourceLength,
        ReferenceRegistry references,
        BlockParser blockParser,
        RemendOptions remendOptions,
        bool isStreaming,
        BlockNode? activeTailLeaf,
        HashSet<int>? affectedTopKeys,
        int topKey)
    {
        var isOpen = custom.IsOpen && !custom.State.SawClose;
        var range = isOpen
            ? new SourceRange(custom.StartOffset, Math.Max(0, sourceLength - custom.StartOffset))
            : custom.Range;
        if (custom.State.Kind == BlockRuleKind.Container)
        {
            var children = MaterializeSiblings(custom.Children, 0, custom.Children.Count, source, sourceLength, references, blockParser, remendOptions, isStreaming, activeTailLeaf, affectedTopKeys, topKey);
            return custom.Rule.Build(in custom.State, children, rawContent: null, isOpen, range);
        }
        return custom.Rule.Build(in custom.State, [], custom.RawBuffer.GetString(), isOpen, range);
    }

    private InlineContent ParseInline(
        ReadOnlySpan<char> span,
        ReferenceRegistry references,
        RemendOptions remendOptions,
        bool healActiveLeaf,
        int topKey)
    {
        if (span.IsEmpty)
        {
            return InlineContent.Empty;
        }
        if (healActiveLeaf && HasAnyInlineHealing(remendOptions))
        {
            var raw = span.ToString();
            var healed = RemendHealer.Complete(raw, remendOptions);
            return _inlineParser.Parse(healed.AsSpan(), _profile, references, topKey);
        }
        return _inlineParser.Parse(span, _profile, references, topKey);
    }

    private static bool HasAnyInlineHealing(RemendOptions options) =>
        options.Bold || options.BoldItalic || options.Italic || options.InlineCode
        || options.Strikethrough || options.Links || options.Images
        || options.Katex || options.InlineKatex || options.HtmlTags
        || (options.Handlers is { Count: > 0 });

    private ReadOnlySpan<char> JoinLines(SourceBuffer source, List<BlockLineSlice> lines, int startIndex, int skipFirstLinePrefix)
    {
        if (startIndex >= lines.Count)
        {
            return ReadOnlySpan<char>.Empty;
        }
        if (lines.Count - startIndex == 1)
        {
            var single = lines[startIndex];
            var s = source.GetSpan(single.Start, single.Length, ref _lineScratch);
            return skipFirstLinePrefix > 0 && skipFirstLinePrefix <= s.Length ? s[skipFirstLinePrefix..] : s;
        }
        var total = 0;
        for (var i = startIndex; i < lines.Count; i++)
        {
            var len = lines[i].Length;
            if (i == startIndex && skipFirstLinePrefix > 0)
            {
                len = Math.Max(0, len - skipFirstLinePrefix);
            }
            total += len + (i > startIndex ? 1 : 0);
        }
        if (_joinScratch.Length < total)
        {
            _joinScratch = new char[Math.Max(total, _joinScratch.Length * 2)];
        }
        var pos = 0;
        for (var i = startIndex; i < lines.Count; i++)
        {
            if (i > startIndex)
            {
                _joinScratch[pos++] = '\n';
            }
            var l = lines[i];
            var start = l.Start;
            var len = l.Length;
            if (i == startIndex && skipFirstLinePrefix > 0)
            {
                var skip = Math.Min(skipFirstLinePrefix, len);
                start += skip;
                len -= skip;
            }
            if (len > 0)
            {
                source.CopyTo(start, len, _joinScratch.AsSpan(pos, len));
                pos += len;
            }
        }
        return _joinScratch.AsSpan(0, pos);
    }

    private static bool IsDescendantOrSelf(BlockNode candidate, BlockNode ancestor)
    {
        for (var cur = candidate; cur is not null; cur = cur.Parent)
        {
            if (ReferenceEquals(cur, ancestor))
            {
                return true;
            }
        }
        return false;
    }
}
