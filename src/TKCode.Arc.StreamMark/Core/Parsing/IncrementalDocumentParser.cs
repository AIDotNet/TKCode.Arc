using System.Collections.Immutable;
using StreamMark.Core.Model;
using StreamMark.Core.Remend;
using StreamMark.Core.Streaming;

namespace StreamMark.Core.Parsing;

/// <summary>
/// 原生增量 Markdown 解析引擎（替代 Markdig）：
/// 组合 <see cref="SourceBuffer"/> + <see cref="BlockParser"/>（完整行提交 + 1 行推测回滚）+
/// <see cref="BlockMaterializer"/>（容器内子项/表格行缓存 + 顶层类型感知冻结）+
/// <see cref="ReferenceRegistry"/>（引用/脚注定向失效，消除 <c>Complete()</c> 全篇重解析）。
/// </summary>
internal sealed class IncrementalDocumentParser
{
    private static readonly RemendOptions SettledOptions = new()
    {
        Bold = false,
        BoldItalic = false,
        Italic = false,
        InlineCode = false,
        Strikethrough = false,
        Katex = false,
        InlineKatex = false,
        Links = false,
        Images = false,
        HtmlTags = false,
        SetextHeadings = false,
    };

    private readonly MarkdownProfile _profile;
    private readonly RemendOptions _streamingOptions;
    private readonly SourceBuffer _source = new();
    private readonly List<LineSpan> _lines = [];
    private readonly BlockParser _blockParser;
    private readonly BlockMaterializer _materializer;
    private readonly ReferenceRegistry _references = new();
    private readonly HashSet<int> _affectedTopKeys = [];
    // 仅保存含未解析前向引用的已冻结顶层块与脚注定义（99% 场景为空字典）
    private readonly Dictionary<int, (int FrozenIndex, BlockNode Node)> _unresolvedFrozen = [];
    private readonly Dictionary<int, FootnoteDefNode> _unresolvedFootnoteDefs = [];
    private AppendOnlyList<MarkdownBlock> _frozen = new();
    private int _indexedPos;
    private int _committedLineCount;
    private int _frozenEnd;
    private int _generation;

    public IncrementalDocumentParser(MarkdownProfile? profile = null, RemendOptions? remendOptions = null)
    {
        _profile = profile ?? MarkdownProfile.Default;
        var baseOpts = remendOptions ?? RemendOptions.Default;
        _streamingOptions = _profile.RemendHandlers.IsEmpty
            ? baseOpts
            : baseOpts with { Handlers = [.. baseOpts.Handlers ?? [], .. _profile.RemendHandlers] };
        _blockParser = new BlockParser(_profile);
        _materializer = new BlockMaterializer(_profile);
    }

    public int FrozenEnd => _frozenEnd;

    public void Reset()
    {
        _source.Clear();
        _lines.Clear();
        _blockParser.Reset();
        _references.Clear();
        _affectedTopKeys.Clear();
        _unresolvedFrozen.Clear();
        _unresolvedFootnoteDefs.Clear();
        _frozen = new AppendOnlyList<MarkdownBlock>();
        _indexedPos = 0;
        _committedLineCount = 0;
        _frozenEnd = 0;
        _generation++;
    }

    /// <summary>
    /// 增量追加新文本片段并产出快照。要求 <paramref name="chunk"/> 是自上次提交之后的新增部分。
    /// </summary>
    public DocumentSnapshot AppendAndParse(ReadOnlySpan<char> chunk, long version)
    {
        if (!chunk.IsEmpty)
        {
            _source.Append(chunk);
        }
        return UpdateCurrent(version, isComplete: false);
    }

    /// <summary>
    /// 兼容 <see cref="DocumentComposer.Update"/> 签名：<paramref name="text"/> 覆盖 [<paramref name="from"/>, 末尾)。
    /// 仅追加自当前已缓冲末尾之后的新增后缀，避免重复处理。
    /// </summary>
    public DocumentSnapshot UpdateSlice(ReadOnlySpan<char> text, int from, long version)
    {
        var expectedEnd = from + text.Length;
        if (from <= _source.Length && expectedEnd >= _source.Length)
        {
            var deltaStart = _source.Length - from;
            if (deltaStart < text.Length)
            {
                _source.Append(text[deltaStart..]);
            }
        }
        else
        {
            // 非追加编辑：重建（正常流式不会走此分支）
            Reset();
            if (!text.IsEmpty)
            {
                _source.Append(text);
            }
        }
        return UpdateCurrent(version, isComplete: false);
    }

    /// <summary>
    /// 流结束收尾：提交最后半行、关闭剩余开放块、消除流式修补；
    /// 仅当存在被迟到定义解析的已冻结引用块时定向刷新受影响块，否则 O(1) 直接收尾，不再整篇重解析！
    /// </summary>
    public DocumentSnapshot Finish(string fullText, long version)
    {
        // 若此前未曾流式追加过（如 Load / LoadNow 一次性加载完整静态文档），零拷贝直接加载并单遍解析
        if (_source.Length == 0 && fullText.Length > 0)
        {
            _source.LoadStatic(fullText);
        }
        else if (fullText.Length > _source.Length)
        {
            _source.Append(fullText.AsSpan(_source.Length));
        }
        else if (fullText.Length < _source.Length)
        {
            Reset();
            _source.LoadStatic(fullText);
        }

        // 只有在迟到引用/脚注定义实际改写已冻结前缀时才由 RebuildAffectedFrozenBlocks 递增 _generation，
        // 其余 99% 场景保持 SharesFrozenWith == true，让 Finish 的 SnapshotDiff O(tail) 跳过整个冻结前缀。
        return UpdateCurrent(version, isComplete: true);
    }

    /// <summary>增量流式收尾：仅追加最后尚未送入的增量片段即完成收尾，无需拷贝全文。</summary>
    public DocumentSnapshot FinishDelta(ReadOnlySpan<char> delta, long version)
    {
        if (!delta.IsEmpty)
        {
            _source.Append(delta);
        }
        return UpdateCurrent(version, isComplete: true);
    }

    /// <summary>一次性完整解析一段 Markdown（无流式状态，供 <see cref="MarkdownParser"/> 与测试对比使用）。</summary>
    public ImmutableArray<MarkdownBlock> ParseFull(ReadOnlySpan<char> markdown, int baseOffset, RemendOptions options)
    {
        if (markdown.IsEmpty)
        {
            return [];
        }
        var isStreaming = HasAnyStreamingOption(options);
        var buf = new SourceBuffer();
        buf.Append(markdown);
        var lines = new List<LineSpan>();
        var indexed = 0;
        buf.IndexLines(lines, ref indexed, buf.Length);

        var parser = new BlockParser(_profile);
        var refs = new ReferenceRegistry();
        var materializer = new BlockMaterializer(_profile);

        var completeCount = lines.Count;
        var hasTrailingPartial = completeCount > 0 && !lines[^1].IsComplete;
        var fullLines = hasTrailingPartial ? completeCount - 1 : completeCount;

        for (var i = 0; i < fullLines; i++)
        {
            parser.FeedCompleteLine(buf, lines[i], i, refs, options, affectedTopKeys: null);
        }
        if (hasTrailingPartial)
        {
            if (isStreaming)
            {
                parser.FeedPartialLine(buf, lines[^1], lines.Count - 1, isStreaming: true, refs, options, affectedTopKeys: null);
            }
            else
            {
                parser.FeedCompleteLine(buf, lines[^1], lines.Count - 1, refs, options, affectedTopKeys: null);
            }
        }
        if (!isStreaming)
        {
            parser.CloseAllOpenBlocks(buf, refs, affectedTopKeys: null);
        }

        // 先扫一遍顶层脚注定义（确保正文中对后文脚注的引用在一次解析中即生效）
        PreRegisterFootnotes(parser.Document, buf, buf.Length, refs, parser, materializer, options, isStreaming);

        var activeLeaf = isStreaming ? FindActiveTailLeaf(parser.Document) : null;
        var blocks = materializer.MaterializeSiblings(
            parser.Document.Children, 0, parser.Document.Children.Count,
            buf, buf.Length, refs, parser, options, isStreaming, activeLeaf, affectedTopKeys: null);

        if (refs.BuildFootnotesBlock() is { } footnotes)
        {
            blocks = blocks.Add(footnotes);
        }

        if (baseOffset != 0)
        {
            blocks = ShiftRanges(blocks, baseOffset);
        }
        return blocks;
    }

    private DocumentSnapshot UpdateCurrent(long version, bool isComplete)
    {
        _source.IndexLines(_lines, ref _indexedPos, _source.Length);
        var activeOptions = isComplete ? SettledOptions : _streamingOptions;

        // 1. 回滚上次半行推测，并永久提交所有新增完整行
        _blockParser.RollbackPartialLine(_references);

        var totalLines = _lines.Count;
        var hasTrailingPartial = totalLines > 0 && !_lines[^1].IsComplete;
        var completeLines = hasTrailingPartial ? totalLines - 1 : totalLines;

        while (_committedLineCount < completeLines)
        {
            _blockParser.FeedCompleteLine(_source, _lines[_committedLineCount], _committedLineCount, _references, activeOptions, _affectedTopKeys);
            _committedLineCount++;
        }

        // 2. 在推测半行之前推进顶层稳定前缀冻结（支持在明确的非续行半行到达时提前冻结前一个已闭合顶层块）
        if (!isComplete)
        {
            LineSpan? partialForFreeze = hasTrailingPartial ? _lines[^1] : null;
            TryAdvanceFrozenPrefix(activeOptions, partialForFreeze);
            // 若新提交的链接/脚注定义命中了先前的已冻结块或未冻结块，立即定向失效并刷新
            if (_affectedTopKeys.Count > 0)
            {
                RebuildAffectedFrozenBlocks();
            }
        }

        // 3. 处理末尾半行或收尾关闭全部块
        if (isComplete)
        {
            if (hasTrailingPartial)
            {
                _blockParser.FeedCompleteLine(_source, _lines[^1], _lines.Count - 1, _references, SettledOptions, _affectedTopKeys);
                _committedLineCount = _lines.Count;
            }
            _blockParser.CloseAllOpenBlocks(_source, _references, _affectedTopKeys);
            // 确保末尾未消费完的脚注全部登记
            PreRegisterFootnotes(_blockParser.Document, _source, _source.Length, _references, _blockParser, _materializer, SettledOptions, isStreaming: false);

            // 若有迟到的引用/脚注定义命中了先前已冻结块，仅定向重物化受影响块（O(affected)）
            if (_affectedTopKeys.Count > 0)
            {
                RebuildAffectedFrozenBlocks();
            }

            // 把剩余未冻结的顶层活跃块全部物化并追加进 _frozen
            var remaining = _materializer.MaterializeSiblings(
                _blockParser.Document.Children, 0, _blockParser.Document.Children.Count,
                _source, _source.Length, _references, _blockParser, SettledOptions,
                isStreaming: false, activeTailLeaf: null, affectedTopKeys: null);

            foreach (var block in remaining)
            {
                _frozen.Add(block);
            }
            if (_references.BuildFootnotesBlock() is { } footnotes)
            {
                _frozen.Add(footnotes);
            }
            _blockParser.Document.Children.Clear();
            _frozenEnd = _source.Length;
            return new DocumentSnapshot(_frozen, _frozen.Count, [], version, _generation, _source.Length, _frozenEnd, isComplete: true);
        }

        // 流式快照：推测性消费末尾半行（若存在）
        if (hasTrailingPartial)
        {
            _blockParser.FeedPartialLine(_source, _lines[^1], _lines.Count - 1, isStreaming: true, _references, activeOptions, _affectedTopKeys);
        }

        var activeLeaf = FindActiveTailLeaf(_blockParser.Document);
        var tailBlocks = _materializer.MaterializeSiblings(
            _blockParser.Document.Children, 0, _blockParser.Document.Children.Count,
            _source, _source.Length, _references, _blockParser, activeOptions,
            isStreaming: true, activeTailLeaf: activeLeaf, affectedTopKeys: _affectedTopKeys);

        if (_references.BuildFootnotesBlock() is { } streamingFootnotes)
        {
            tailBlocks = tailBlocks.Add(streamingFootnotes);
        }

        return new DocumentSnapshot(_frozen, _frozen.Count, tailBlocks, version, _generation, _source.Length, _frozenEnd, isComplete: false);
    }

    /// <summary>
    /// 类型感知顶层冻结（结合 lobehub/streamdown FROZEN_TYPES 与 StreamMark 流式规则）：
    /// 当顶层至少有 2 个完整块且首个块已彻底闭合、不处于未闭合 HTML 组或待定定义术语时，
    /// 将其物化后移入不可变 <c>_frozen</c>，并从 <c>Document.Children</c> 释放，使内存与每帧开销仅与尾部成正比。
    /// </summary>
    private char[] _freezeScratch = new char[64];

    private void TryAdvanceFrozenPrefix(RemendOptions options, LineSpan? trailingPartial)
    {
        var children = _blockParser.Document.Children;

        while (children.Count >= 2)
        {
            var first = children[0];
            if (first.IsOpen)
            {
                break;
            }
            // 脚注定义不在原位输出，直接吸收后继续检查下一个（含未解析链接引用的脚注保留句柄以便迟到定义刷新）
            if (first is FootnoteDefNode fn)
            {
                var fnChildren = _materializer.MaterializeSiblings(fn.Children, 0, fn.Children.Count, _source, _source.Length, _references, _blockParser, SettledOptions, isStreaming: false, activeTailLeaf: null, affectedTopKeys: _affectedTopKeys, topKeyOverride: fn.StartOffset);
                _references.UpsertFootnote(fn.RawLabel, fn.Range, fnChildren, _affectedTopKeys);
                if (MayContainUnresolvedReference(fn))
                {
                    _unresolvedFootnoteDefs[fn.StartOffset] = fn;
                }
                var nextBoundary = children[1].StartOffset;
                children.RemoveAt(0);
                _frozenEnd = Math.Max(_frozenEnd, nextBoundary);
                continue;
            }
            // 若是 HTML 块，必须确认它所属的 HtmlGroup 已全部闭合（不含未闭合的 <div> 等容器标签）
            if (first is HtmlBlockNode)
            {
                var stitchCount = CountClosedHtmlGroupPrefix(children);
                if (stitchCount <= 0 || stitchCount >= children.Count)
                {
                    break;
                }
                var stitched = _materializer.MaterializeSiblings(children, 0, stitchCount, _source, _source.Length, _references, _blockParser, SettledOptions, isStreaming: false, activeTailLeaf: null, affectedTopKeys: _affectedTopKeys);
                var nextStart = children[stitchCount].StartOffset;
                foreach (var b in stitched)
                {
                    _frozen.Add(b);
                }
                children.RemoveRange(0, stitchCount);
                _frozenEnd = Math.Max(_frozenEnd, nextStart);
                continue;
            }
            // 如果定义列表后面紧跟一个可能即将成为下一术语的段落，暂不冻结定义列表（等待确认是否续接 ": "）
            if (first is DefinitionListNode && children[1] is ParagraphNode && _profile.EnableDefinitionLists)
            {
                break;
            }

            // 此时 first 已闭合且后面至少有一个已开始的新完整块（children[1]），安全冻结！
            var frozenStartBefore = _frozen.Count;
            var block = _materializer.MaterializeNode(first, _source, _source.Length, _references, _blockParser, SettledOptions, isStreaming: false, activeTailLeaf: null, affectedTopKeys: _affectedTopKeys, topKey: first.StartOffset);
            var boundary = children[1].StartOffset;
            if (block is not null)
            {
                // 如果这个块内有未解析的 '[' 引用候选，在 _unresolvedFrozen 中保留其节点句柄供 Finish 定向刷新
                if (MayContainUnresolvedReference(first))
                {
                    _unresolvedFrozen[block.Key] = (frozenStartBefore, first);
                }
                _frozen.Add(block);
            }
            children.RemoveAt(0);
            _frozenEnd = Math.Max(_frozenEnd, boundary);
        }

        // 若只剩 1 个已闭合顶层块，且当前末尾半行已明确开启一个不会回头改变该块的新块，也可安全提前冻结
        if (children.Count == 1 && trailingPartial is { } partial && partial.ContentLength > 0)
        {
            var only = children[0];
            if (!only.IsOpen && only is not (FootnoteDefNode or HtmlBlockNode or ListNode or IndentedCodeNode or DefinitionListNode))
            {
                var partialSpan = _source.GetSpan(partial.Start, partial.ContentLength, ref _freezeScratch);
                if (IsDecisiveNewBlockStart(partialSpan))
                {
                    var frozenStartBefore = _frozen.Count;
                    var block = _materializer.MaterializeNode(only, _source, _source.Length, _references, _blockParser, SettledOptions, isStreaming: false, activeTailLeaf: null, affectedTopKeys: _affectedTopKeys, topKey: only.StartOffset);
                    if (block is not null)
                    {
                        if (MayContainUnresolvedReference(only))
                        {
                            _unresolvedFrozen[block.Key] = (frozenStartBefore, only);
                        }
                        _frozen.Add(block);
                    }
                    children.RemoveAt(0);
                    _frozenEnd = Math.Max(_frozenEnd, partial.Start);
                }
            }
        }
    }

    private static bool IsDecisiveNewBlockStart(ReadOnlySpan<char> partial)
    {
        if (partial.IsEmpty || char.IsWhiteSpace(partial[0]))
        {
            return false;
        }
        var c = partial[0];
        if (c is ':' or '<')
        {
            return false;
        }
        if (c is '-' or '*' or '+' or '[' or '=' or (>= '0' and <= '9'))
        {
            return partial.Length >= 16;
        }
        return true;
    }

    private int CountClosedHtmlGroupPrefix(List<BlockNode> children)
    {
        var tree = new Html.HtmlTreeBuilder();
        for (var i = 0; i < children.Count - 1; i++)
        {
            if (children[i].IsOpen)
            {
                return 0;
            }
            if (children[i] is HtmlBlockNode html)
            {
                tree.Feed(html.Buffer.GetString());
                if (!tree.HasOpenElements)
                {
                    return i + 1;
                }
            }
        }
        return 0;
    }

    private void RebuildAffectedFrozenBlocks()
    {
        if (_affectedTopKeys.Count == 0)
        {
            return;
        }
        // 标记尚未冻结但已被缓存的尾部块为脏，保证前向引用及时生效
        var children = _blockParser.Document.Children;
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (_affectedTopKeys.Contains(child.StartOffset))
            {
                MarkSubtreeDirty(child);
            }
        }
        // 已吸收的脚注定义内若引用了迟到的链接定义，也一并刷新
        if (_unresolvedFootnoteDefs.Count > 0)
        {
            foreach (var key in _affectedTopKeys)
            {
                if (_unresolvedFootnoteDefs.TryGetValue(key, out var fn))
                {
                    MarkSubtreeDirty(fn);
                    var fnChildren = _materializer.MaterializeSiblings(fn.Children, 0, fn.Children.Count, _source, _source.Length, _references, _blockParser, SettledOptions, isStreaming: false, activeTailLeaf: null, affectedTopKeys: null, topKeyOverride: fn.StartOffset);
                    _references.UpsertFootnote(fn.RawLabel, fn.Range, fnChildren, affectedTopKeys: null);
                }
            }
        }
        if (_unresolvedFrozen.Count == 0)
        {
            _affectedTopKeys.Clear();
            return;
        }
        MarkdownBlock[]? updated = null;
        var anyChanged = false;
        foreach (var key in _affectedTopKeys)
        {
            if (_unresolvedFrozen.TryGetValue(key, out var entry) && entry.FrozenIndex < _frozen.Count)
            {
                MarkSubtreeDirty(entry.Node);
                if (_materializer.MaterializeNode(entry.Node, _source, _source.Length, _references, _blockParser, SettledOptions, isStreaming: false, activeTailLeaf: null, affectedTopKeys: null, topKey: key) is { } rebuilt)
                {
                    if (updated is null)
                    {
                        updated = new MarkdownBlock[_frozen.Count];
                        for (var i = 0; i < _frozen.Count; i++)
                        {
                            updated[i] = _frozen[i];
                        }
                    }
                    updated[entry.FrozenIndex] = rebuilt;
                    anyChanged = true;
                }
            }
        }
        _affectedTopKeys.Clear();
        if (anyChanged && updated is not null)
        {
            var nextFrozen = new AppendOnlyList<MarkdownBlock>();
            for (var i = 0; i < updated.Length; i++)
            {
                nextFrozen.Add(updated[i]);
            }
            _frozen = nextFrozen;
            _generation++;
        }
    }

    private static void MarkSubtreeDirty(BlockNode node)
    {
        node.Dirty = true;
        node.CachedModel = null;
        if (node is ContainerNode container)
        {
            for (var i = 0; i < container.Children.Count; i++)
            {
                MarkSubtreeDirty(container.Children[i]);
            }
        }
        else if (node is DefinitionListNode dl)
        {
            for (var i = 0; i < dl.Items.Count; i++)
            {
                var defs = dl.Items[i].Definitions;
                for (var j = 0; j < defs.Count; j++)
                {
                    MarkSubtreeDirty(defs[j]);
                }
            }
        }
    }

    private void PreRegisterFootnotes(
        DocumentNode doc,
        SourceBuffer source,
        int sourceLength,
        ReferenceRegistry refs,
        BlockParser parser,
        BlockMaterializer materializer,
        RemendOptions options,
        bool isStreaming)
    {
        for (var i = 0; i < doc.Children.Count; i++)
        {
            if (doc.Children[i] is FootnoteDefNode fn)
            {
                var fnChildren = materializer.MaterializeSiblings(fn.Children, 0, fn.Children.Count, source, sourceLength, refs, parser, options, isStreaming, activeTailLeaf: null, affectedTopKeys: _affectedTopKeys, topKeyOverride: fn.StartOffset);
                refs.UpsertFootnote(fn.RawLabel, fn.Range, fnChildren, _affectedTopKeys);
            }
            else if (doc.Children[i] is ParagraphNode para && para.MaybeDefinition && !para.LeadingDefsChecked)
            {
                parser.ConsumeLeadingDefinitions(para, source, refs, _affectedTopKeys);
            }
        }
    }

    private static BlockNode? FindActiveTailLeaf(DocumentNode doc)
    {
        BlockNode? cur = doc.LastChild;
        while (cur is not null)
        {
            switch (cur)
            {
                case DefinitionListNode dl when dl.Items.Count > 0 && dl.Items[^1].Definitions.Count > 0:
                    cur = dl.Items[^1].Definitions[^1];
                    continue;
                case ComponentNode { IsRaw: true }:
                    return cur;
                case ContainerNode container when container.Children.Count > 0:
                    cur = container.Children[^1];
                    continue;
                default:
                    return cur;
            }
        }
        return null;
    }

    private bool MayContainUnresolvedReference(BlockNode node)
    {
        var len = Math.Min(node.EndOffset - node.StartOffset, _source.Length - node.StartOffset);
        if (len <= 0 || node.StartOffset < 0)
        {
            return false;
        }
        // 快速扫描是否含 '['（绝大多数段落不含 '['，无需加入 _unresolvedFrozen）
        for (var i = node.StartOffset; i < node.StartOffset + len; i++)
        {
            if (_source[i] == '[')
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasAnyStreamingOption(RemendOptions options) =>
        options.Bold || options.BoldItalic || options.Italic || options.InlineCode
        || options.Strikethrough || options.Links || options.Images
        || options.Katex || options.InlineKatex || options.HtmlTags || options.SetextHeadings
        || (options.Handlers is { Count: > 0 });

    private static ImmutableArray<MarkdownBlock> ShiftRanges(ImmutableArray<MarkdownBlock> blocks, int delta)
    {
        var builder = ImmutableArray.CreateBuilder<MarkdownBlock>(blocks.Length);
        foreach (var b in blocks)
        {
            builder.Add(ShiftBlock(b, delta));
        }
        return builder.MoveToImmutable();
    }

    private static MarkdownBlock ShiftBlock(MarkdownBlock block, int delta)
    {
        var shiftedRange = new SourceRange(block.Range.Start + delta, block.Range.Length);
        return block switch
        {
            QuoteBlock q => q with { Range = shiftedRange, Children = ShiftRanges(q.Children, delta) },
            AlertBlock a => a with { Range = shiftedRange, Children = ShiftRanges(a.Children, delta) },
            CustomContainerBlock c => c with { Range = shiftedRange, Children = ShiftRanges(c.Children, delta) },
            HtmlBoxBlock h => h with { Range = shiftedRange, Children = ShiftRanges(h.Children, delta) },
            ComponentBlock comp => comp with { Range = shiftedRange, Children = comp.Children.IsEmpty ? comp.Children : ShiftRanges(comp.Children, delta) },
            ListBlock list => list with
            {
                Range = shiftedRange,
                Items = [.. list.Items.Select(item => item with
                {
                    Range = new SourceRange(item.Range.Start + delta, item.Range.Length),
                    TaskMarker = item.TaskMarker.Length == 0 ? default : new SourceRange(item.TaskMarker.Start + delta, item.TaskMarker.Length),
                    Children = ShiftRanges(item.Children, delta),
                })],
            },
            FootnotesBlock fns => fns with
            {
                Range = shiftedRange,
                Footnotes = [.. fns.Footnotes.Select(fn => fn with { Children = ShiftRanges(fn.Children, delta) })],
            },
            DefinitionListBlock dl => dl with
            {
                Range = shiftedRange,
                Items = [.. dl.Items.Select(it => it with { Definitions = ShiftRanges(it.Definitions, delta) })],
            },
            _ => block with { Range = shiftedRange },
        };
    }
}

/// <summary>
/// 静态一次性 Markdown 解析入口（零 Markdig 依赖，直接产出不可变 <see cref="MarkdownBlock"/> 模型）。
/// 流式增量场景请使用 <see cref="StreamingDocument"/>。
/// </summary>
public static class MarkdownParser
{
    private static readonly RemendOptions StaticOptions = new()
    {
        Bold = false,
        BoldItalic = false,
        Italic = false,
        InlineCode = false,
        Strikethrough = false,
        Katex = false,
        InlineKatex = false,
        Links = false,
        Images = false,
        HtmlTags = false,
        SetextHeadings = false,
    };

    /// <summary>解析完整 Markdown 字符串为不可变块模型列表。</summary>
    public static ImmutableArray<MarkdownBlock> Parse(string markdown, MarkdownProfile? profile = null)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        return Parse(markdown.AsSpan(), profile);
    }

    /// <summary>解析完整 Markdown 字符切片为不可变块模型列表。</summary>
    public static ImmutableArray<MarkdownBlock> Parse(ReadOnlySpan<char> markdown, MarkdownProfile? profile = null)
    {
        if (markdown.IsEmpty)
        {
            return [];
        }
        return new IncrementalDocumentParser(profile, StaticOptions).ParseFull(markdown, 0, StaticOptions);
    }
}
