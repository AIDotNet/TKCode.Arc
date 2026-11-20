using System.Collections.Immutable;
using StreamMark.Core.Model;
using StreamMark.Core.Remend;

namespace StreamMark.Core.Parsing;

/// <summary>
/// CommonMark 0.31.2 + GFM + StreamMark 增量逐行块解析器：
/// 维护开放块栈（<c>_openStack</c>），完整行永久提交、末尾未结束半行通过轻量 <c>UndoLog</c>
/// 实现 O(depth) 推测与回滚；闭合子块/表格行/列表项在容器内原地缓存。
/// </summary>
internal sealed class BlockParser
{
    private readonly MarkdownProfile _profile;
    private readonly DocumentNode _document = new();
    private readonly List<BlockNode> _openStack = [];
    private readonly List<UndoOp> _undoLog = [];
    private readonly List<BlockNode> _savedOpenStack = [];
    private bool _recordingUndo;
    private ReferenceRegistry.Checkpoint _savedRefCheckpoint;
    private bool _hasPartialSpeculation;
    private char[] _lineScratch = new char[256];

    private enum UndoKind : byte
    {
        PopChildren,
        ReplaceChild,
        RestoreBlockMeta,
        PopParagraphLine,
        PopTableRow,
        RestoreBuffer,
        RestoreListState,
        RestoreListItemState,
        RestoreComponentFlags,
        RestoreFenceClose,
        RestoreMathClose,
        RestoreCustomContainerClose,
        RestoreCustomRuleState,
        RestoreContainerBlank,
        PopDefinitionItem,
        PopDefinitionBody,
        RestoreParagraphMeta,
        RestoreParagraphLines,
        RestoreFootnoteBlank,
        RestoreRemovedChild,
    }

    private struct UndoOp
    {
        public UndoKind Kind;
        public object? Target;
        public object? Aux;
        public int Int1;
        public int Int2;
        public bool Bool1;
        public bool Bool2;
        public BlockRuleState RuleState;
    }

    public BlockParser(MarkdownProfile profile)
    {
        _profile = profile;
        _openStack.Add(_document);
    }

    public DocumentNode Document => _document;

    public IReadOnlyList<BlockNode> OpenStack => _openStack;

    public void Reset()
    {
        _hasPartialSpeculation = false;
        _recordingUndo = false;
        _undoLog.Clear();
        _savedOpenStack.Clear();
        _document.Children.Clear();
        _document.LastChildEndedWithBlankLine = false;
        _document.StartOffset = 0;
        _document.EndOffset = 0;
        _document.Dirty = true;
        _document.CachedModel = null;
        _openStack.Clear();
        _openStack.Add(_document);
    }

    /// <summary>永久提交一条已以换行结束的完整行。</summary>
    public void FeedCompleteLine(
        SourceBuffer source,
        LineSpan lineSpan,
        int lineIndex,
        ReferenceRegistry references,
        RemendOptions remendOptions,
        HashSet<int>? affectedTopKeys)
    {
        RollbackPartialLine(references);
        var line = source.GetSpan(lineSpan.Start, lineSpan.ContentLength, ref _lineScratch);
        FeedLineCore(source, line, lineSpan.Start, lineSpan.ContentEnd, lineIndex, isPartialStreamingLine: false, references, remendOptions, affectedTopKeys);
        _document.EndOffset = lineSpan.FullEnd;
        _document.EndLine = lineIndex;
    }

    /// <summary>推测性消费末尾尚未见到换行符的半行（下一次追加时 O(1) 回滚）。</summary>
    public void FeedPartialLine(
        SourceBuffer source,
        LineSpan lineSpan,
        int lineIndex,
        bool isStreaming,
        ReferenceRegistry references,
        RemendOptions remendOptions,
        HashSet<int>? affectedTopKeys)
    {
        RollbackPartialLine(references);
        _savedOpenStack.Clear();
        _savedOpenStack.AddRange(_openStack);
        _savedRefCheckpoint = references.Save();
        _undoLog.Clear();
        _recordingUndo = true;
        _hasPartialSpeculation = true;

        var rawLine = source.GetSpan(lineSpan.Start, lineSpan.ContentLength, ref _lineScratch);
        var effectiveLine = rawLine;
        var effectiveEnd = lineSpan.ContentEnd;

        // 流式末尾未闭合 HTML 标签剥离（对应 HtmlTests.Streaming_PartialHtml_NeverShowsRawTags）：
        // 仅在不在代码/公式/原样组件/HTML 1-5 块内部时生效
        if (isStreaming && remendOptions.HtmlTags && !IsInsideOpaqueTextLeaf())
        {
            var stripped = BlockMatchers.StripTrailingIncompleteHtmlTag(rawLine);
            if (stripped.Length != rawLine.Length)
            {
                effectiveLine = stripped;
                effectiveEnd = lineSpan.Start + stripped.Length;
                if (effectiveLine.IsEmpty)
                {
                    _recordingUndo = false;
                    return;
                }
            }
        }

        FeedLineCore(source, effectiveLine, lineSpan.Start, effectiveEnd, lineIndex, isPartialStreamingLine: isStreaming, references, remendOptions, affectedTopKeys);
        _document.EndOffset = lineSpan.FullEnd;
        _document.EndLine = lineIndex;
        _recordingUndo = false;
    }

    /// <summary>O(depth) 回滚上一次 <see cref="FeedPartialLine"/> 做的全部改动。</summary>
    public void RollbackPartialLine(ReferenceRegistry references)
    {
        if (!_hasPartialSpeculation)
        {
            return;
        }
        _hasPartialSpeculation = false;
        _recordingUndo = false;
        for (var i = _undoLog.Count - 1; i >= 0; i--)
        {
            ref var op = ref System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_undoLog)[i];
            switch (op.Kind)
            {
                case UndoKind.PopChildren:
                    {
                        var container = (ContainerNode)op.Target!;
                        if (container.Children.Count > op.Int1)
                        {
                            container.Children.RemoveRange(op.Int1, container.Children.Count - op.Int1);
                        }
                        container.Dirty = true;
                        container.CachedModel = null;
                        break;
                    }
                case UndoKind.ReplaceChild:
                    {
                        var container = (ContainerNode)op.Target!;
                        var oldChild = (BlockNode)op.Aux!;
                        container.Children[op.Int1] = oldChild;
                        oldChild.Parent = container;
                        container.Dirty = true;
                        container.CachedModel = null;
                        break;
                    }
                case UndoKind.RestoreBlockMeta:
                    {
                        var node = (BlockNode)op.Target!;
                        node.EndOffset = op.Int1;
                        node.EndLine = op.Int2;
                        node.IsOpen = op.Bool1;
                        node.Dirty = true;
                        node.CachedModel = null;
                        break;
                    }
                case UndoKind.PopParagraphLine:
                    {
                        var para = (ParagraphNode)op.Target!;
                        if (para.Lines.Count > op.Int1)
                        {
                            para.Lines.RemoveRange(op.Int1, para.Lines.Count - op.Int1);
                        }
                        para.Dirty = true;
                        para.CachedModel = null;
                        break;
                    }
                case UndoKind.PopTableRow:
                    {
                        var table = (TableNode)op.Target!;
                        if (table.Rows.Count > op.Int1)
                        {
                            table.Rows.RemoveRange(op.Int1, table.Rows.Count - op.Int1);
                        }
                        table.Dirty = true;
                        table.CachedModel = null;
                        break;
                    }
                case UndoKind.RestoreBuffer:
                    {
                        var buf = (OpenBlockBuffer)op.Target!;
                        buf.Restore(new OpenBlockBuffer.Checkpoint(op.Int1, op.Int2));
                        break;
                    }
                case UndoKind.RestoreListState:
                    {
                        var list = (ListNode)op.Target!;
                        list.IsLoose = op.Bool1;
                        list.SawBlankLineBetweenItems = op.Bool2;
                        list.Dirty = true;
                        list.CachedModel = null;
                        break;
                    }
                case UndoKind.RestoreListItemState:
                    {
                        var item = (ListItemNode)op.Target!;
                        item.SawBlankLine = op.Bool1;
                        item.HadContentAfterMarker = op.Bool2;
                        item.CachedItem = null;
                        item.Dirty = true;
                        item.CachedModel = null;
                        break;
                    }
                case UndoKind.RestoreComponentFlags:
                    {
                        var comp = (ComponentNode)op.Target!;
                        comp.SawClose = op.Bool1;
                        comp.CloseAfterLine = op.Bool2;
                        comp.Dirty = true;
                        comp.CachedModel = null;
                        break;
                    }
                case UndoKind.RestoreFenceClose:
                    ((FencedCodeNode)op.Target!).SawClose = op.Bool1;
                    break;
                case UndoKind.RestoreMathClose:
                    ((MathBlockNode)op.Target!).SawClose = op.Bool1;
                    break;
                case UndoKind.RestoreCustomContainerClose:
                    ((CustomContainerNode)op.Target!).SawClose = op.Bool1;
                    break;
                case UndoKind.RestoreCustomRuleState:
                    ((CustomRuleBlockNode)op.Target!).State = op.RuleState;
                    break;
                case UndoKind.RestoreContainerBlank:
                    ((ContainerNode)op.Target!).LastChildEndedWithBlankLine = op.Bool1;
                    break;
                case UndoKind.PopDefinitionItem:
                    {
                        var dl = (DefinitionListNode)op.Target!;
                        if (dl.Items.Count > op.Int1)
                        {
                            dl.Items.RemoveRange(op.Int1, dl.Items.Count - op.Int1);
                        }
                        dl.Dirty = true;
                        dl.CachedModel = null;
                        break;
                    }
                case UndoKind.PopDefinitionBody:
                    {
                        var item = (DefinitionItemNode)op.Target!;
                        if (item.Definitions.Count > op.Int1)
                        {
                            item.Definitions.RemoveRange(op.Int1, item.Definitions.Count - op.Int1);
                        }
                        item.Dirty = true;
                        item.CachedItem = null;
                        break;
                    }
                case UndoKind.RestoreParagraphMeta:
                    {
                        var para = (ParagraphNode)op.Target!;
                        para.LeadingDefsChecked = op.Bool1;
                        para.StartOffset = op.Int1;
                        break;
                    }
                case UndoKind.RestoreParagraphLines:
                    {
                        var para = (ParagraphNode)op.Target!;
                        para.Lines.Clear();
                        para.Lines.AddRange((List<BlockLineSlice>)op.Aux!);
                        para.Dirty = true;
                        para.CachedModel = null;
                        break;
                    }
                case UndoKind.RestoreFootnoteBlank:
                    ((FootnoteDefNode)op.Target!).SawBlankLine = op.Bool1;
                    break;
                case UndoKind.RestoreRemovedChild:
                    {
                        var container = (ContainerNode)op.Target!;
                        var removed = (BlockNode)op.Aux!;
                        container.Children.Insert(op.Int1, removed);
                        removed.Parent = container;
                        container.Dirty = true;
                        container.CachedModel = null;
                        break;
                    }
            }
        }
        _undoLog.Clear();
        _openStack.Clear();
        _openStack.AddRange(_savedOpenStack);
        _savedOpenStack.Clear();
        references.Restore(_savedRefCheckpoint);
    }

    /// <summary>流结束时关闭所有仍开放的块（提交最终链接引用定义与脚注）。</summary>
    public void CloseAllOpenBlocks(SourceBuffer source, ReferenceRegistry references, HashSet<int>? affectedTopKeys)
    {
        RollbackPartialLine(references);
        CloseFromDepth(1, source, references, affectedTopKeys);
    }

    private bool IsInsideOpaqueTextLeaf()
    {
        if (_openStack.Count <= 1)
        {
            return false;
        }
        return _openStack[^1] switch
        {
            FencedCodeNode or IndentedCodeNode or MathBlockNode => true,
            ComponentNode { IsRaw: true } => true,
            CustomRuleBlockNode { State.Kind: BlockRuleKind.RawBody } => true,
            HtmlBlockNode { HtmlKind: <= CommonMarkHtmlBlockKind.Type5CData } => true,
            _ => false,
        };
    }

    // ---- 核心逐行解析算法（CommonMark Appendix A + 扩展） ----

    private void FeedLineCore(
        SourceBuffer source,
        ReadOnlySpan<char> line,
        int lineStartOffset,
        int lineContentEnd,
        int lineIndex,
        bool isPartialStreamingLine,
        ReferenceRegistry references,
        RemendOptions remendOptions,
        HashSet<int>? affectedTopKeys)
    {
        var idx = 0;
        var col = 0;
        var lineEndLimit = line.Length;
        var virtualSpaces = 0;

        // Phase 1: 沿开放块栈自顶向下匹配续行
        var matchedDepth = 1; // _openStack[0] 是 DocumentNode，始终匹配
        var consumedLine = false;

        for (var depth = 1; depth < _openStack.Count; depth++)
        {
            var node = _openStack[depth];
            var indent = virtualSpaces + UnicodeClassification.MeasureIndent(line[..lineEndLimit], idx, col, out var firstNonWs);
            var isBlank = firstNonWs >= lineEndLimit;
            var contentSpan = isBlank ? ReadOnlySpan<char>.Empty : line.Slice(firstNonWs, lineEndLimit - firstNonWs);

            switch (node)
            {
                case QuoteNode:
                    if (!isBlank && indent <= 3 && contentSpan[0] == '>')
                    {
                        idx = firstNonWs + 1;
                        col += indent + 1;
                        virtualSpaces = 0;
                        // 可选单个空格（或 Tab 展开出 1 列）
                        if (idx < lineEndLimit)
                        {
                            if (line[idx] == ' ')
                            {
                                idx++;
                                col++;
                            }
                            else if (line[idx] == '\t')
                            {
                                var tabWidth = 4 - (col % 4);
                                idx++;
                                col++;
                                virtualSpaces = tabWidth - 1;
                            }
                        }
                        matchedDepth = depth + 1;
                        continue;
                    }
                    goto DoneMatching;

                case ListNode:
                    matchedDepth = depth + 1;
                    continue;

                case ListItemNode item:
                    if (isBlank)
                    {
                        if (item.StartedEmpty && item.Children.Count == 0 && item.SawBlankLine)
                        {
                            goto DoneMatching;
                        }
                        RecordListItemState(item);
                        item.SawBlankLine = true;
                        matchedDepth = depth + 1;
                        continue;
                    }
                    if (col + indent >= item.ContentColumn)
                    {
                        var advance = item.ContentColumn - col;
                        AdvanceWithVirtual(line[..lineEndLimit], ref idx, ref col, ref virtualSpaces, advance);
                        matchedDepth = depth + 1;
                        continue;
                    }
                    goto DoneMatching;

                case FootnoteDefNode fn:
                    if (isBlank)
                    {
                        RecordFootnoteBlank(fn);
                        fn.SawBlankLine = true;
                        matchedDepth = depth + 1;
                        continue;
                    }
                    if (col + indent >= fn.ContentColumn)
                    {
                        var advance = fn.ContentColumn - col;
                        AdvanceWithVirtual(line[..lineEndLimit], ref idx, ref col, ref virtualSpaces, advance);
                        matchedDepth = depth + 1;
                        continue;
                    }
                    goto DoneMatching;

                case DefinitionListNode:
                    if (!isBlank && indent <= 3 && BlockMatchers.TryMatchDefinitionMarker(contentSpan, out _))
                    {
                        matchedDepth = depth + 1;
                        continue;
                    }
                    goto DoneMatching;

                case CustomContainerNode cc:
                    if (!isBlank && indent <= 3 && BlockMatchers.IsCustomContainerClose(contentSpan, cc.FenceLength))
                    {
                        RecordCustomContainerClose(cc);
                        cc.SawClose = true;
                        UpdateNodeEnd(cc, lineContentEnd, lineIndex);
                        CloseFromDepth(depth, source, references, affectedTopKeys);
                        consumedLine = true;
                        goto DoneLine;
                    }
                    UpdateNodeEnd(cc, lineContentEnd, lineIndex);
                    matchedDepth = depth + 1;
                    continue;

                case ComponentNode comp:
                    if (comp.CloseAfterLine)
                    {
                        goto DoneMatching;
                    }
                    if (comp.IsRaw)
                    {
                        var rawSlice = line.Slice(idx, lineEndLimit - idx);
                        var trimmedStart = rawSlice.TrimStart();
                        if (HtmlTagReader.TryRead(trimmedStart, out var closeTag)
                            && closeTag.IsClosing
                            && string.Equals(closeTag.Name, comp.Name, StringComparison.OrdinalIgnoreCase)
                            && trimmedStart[closeTag.Length..].IsWhiteSpace())
                        {
                            RecordComponentFlags(comp);
                            comp.SawClose = true;
                            UpdateNodeEnd(comp, lineContentEnd, lineIndex);
                            CloseFromDepth(depth, source, references, affectedTopKeys);
                            consumedLine = true;
                            goto DoneLine;
                        }
                        var trimmedEnd = rawSlice.TrimEnd();
                        var closingLiteral = $"</{comp.Name}>";
                        if (trimmedEnd.Length >= closingLiteral.Length && trimmedEnd.EndsWith(closingLiteral, StringComparison.OrdinalIgnoreCase))
                        {
                            var bodyPart = trimmedEnd[..^closingLiteral.Length];
                            RecordBuffer(comp.RawBuffer);
                            comp.RawBuffer.AppendLine(bodyPart);
                            RecordComponentFlags(comp);
                            comp.CloseAfterLine = true;
                            comp.SawClose = true;
                            UpdateNodeEnd(comp, lineContentEnd, lineIndex);
                            CloseFromDepth(depth, source, references, affectedTopKeys);
                            consumedLine = true;
                            goto DoneLine;
                        }
                        RecordBuffer(comp.RawBuffer);
                        comp.RawBuffer.AppendLine(rawSlice);
                        UpdateNodeEnd(comp, lineContentEnd, lineIndex);
                        comp.MarkDirtyUpwards();
                        consumedLine = true;
                        goto DoneLine;
                    }
                    else
                    {
                        // Markdown 内容块级组件：若内层还有同名未闭合组件，结束标签归内层
                        if (!HasOpenSameNameComponentBelow(depth, comp.Name))
                        {
                            if (!isBlank && HtmlTagReader.TryRead(contentSpan, out var closeTag)
                                && closeTag.IsClosing
                                && string.Equals(closeTag.Name, comp.Name, StringComparison.OrdinalIgnoreCase)
                                && contentSpan[closeTag.Length..].IsWhiteSpace())
                            {
                                RecordComponentFlags(comp);
                                comp.SawClose = true;
                                UpdateNodeEnd(comp, lineContentEnd, lineIndex);
                                CloseFromDepth(depth, source, references, affectedTopKeys);
                                consumedLine = true;
                                goto DoneLine;
                            }
                            var restTrimmed = line.Slice(idx, lineEndLimit - idx).TrimEnd();
                            var closingLiteral = $"</{comp.Name}>";
                            if (restTrimmed.Length >= closingLiteral.Length && restTrimmed.EndsWith(closingLiteral, StringComparison.OrdinalIgnoreCase))
                            {
                                lineEndLimit = idx + restTrimmed.Length - closingLiteral.Length;
                                RecordComponentFlags(comp);
                                comp.CloseAfterLine = true;
                                comp.SawClose = true;
                            }
                        }
                        UpdateNodeEnd(comp, lineContentEnd, lineIndex);
                        matchedDepth = depth + 1;
                        continue;
                    }

                case CustomRuleBlockNode custom:
                    if (custom.State.CloseAfterLine)
                    {
                        goto DoneMatching;
                    }
                    {
                        var ruleLine = line.Slice(idx, lineEndLimit - idx);
                        RecordCustomRuleState(custom);
                        var res = custom.Rule.TryContinue(ruleLine, ref custom.State);
                        switch (res)
                        {
                            case BlockContinueResult.None:
                                goto DoneMatching;
                            case BlockContinueResult.Continue:
                                UpdateNodeEnd(custom, lineContentEnd, lineIndex);
                                if (custom.State.Kind == BlockRuleKind.RawBody)
                                {
                                    RecordBuffer(custom.RawBuffer);
                                    custom.RawBuffer.AppendLine(ruleLine);
                                    custom.MarkDirtyUpwards();
                                    consumedLine = true;
                                    goto DoneLine;
                                }
                                matchedDepth = depth + 1;
                                continue;
                            case BlockContinueResult.ContinueDiscard:
                                UpdateNodeEnd(custom, lineContentEnd, lineIndex);
                                consumedLine = true;
                                goto DoneLine;
                            case BlockContinueResult.Break:
                                UpdateNodeEnd(custom, lineContentEnd, lineIndex);
                                if (custom.State.Kind == BlockRuleKind.RawBody)
                                {
                                    RecordBuffer(custom.RawBuffer);
                                    custom.RawBuffer.AppendLine(ruleLine);
                                    CloseFromDepth(depth, source, references, affectedTopKeys);
                                    consumedLine = true;
                                    goto DoneLine;
                                }
                                custom.State.CloseAfterLine = true;
                                matchedDepth = depth + 1;
                                continue;
                            case BlockContinueResult.BreakDiscard:
                                UpdateNodeEnd(custom, lineContentEnd, lineIndex);
                                CloseFromDepth(depth, source, references, affectedTopKeys);
                                consumedLine = true;
                                goto DoneLine;
                        }
                        goto DoneMatching;
                    }

                case FencedCodeNode fenced:
                    if (!isBlank && indent <= 3 && BlockMatchers.IsFencedCodeClose(contentSpan, fenced.FenceChar, fenced.FenceLength))
                    {
                        RecordFenceClose(fenced);
                        fenced.SawClose = true;
                        UpdateNodeEnd(fenced, lineContentEnd, lineIndex);
                        CloseFromDepth(depth, source, references, affectedTopKeys);
                        consumedLine = true;
                        goto DoneLine;
                    }
                    RecordBuffer(fenced.Buffer);
                    fenced.Buffer.AppendFencedLine(line.Slice(idx, lineEndLimit - idx), fenced.Indent);
                    UpdateNodeEnd(fenced, lineContentEnd, lineIndex);
                    fenced.MarkDirtyUpwards();
                    consumedLine = true;
                    goto DoneLine;

                case IndentedCodeNode indented:
                    if (isBlank)
                    {
                        var remainingSpaces = Math.Max(0, indent - 4);
                        RecordBuffer(indented.Buffer);
                        if (remainingSpaces > 0)
                        {
                            AppendSpacesLine(indented.Buffer, remainingSpaces);
                        }
                        else
                        {
                            indented.Buffer.AppendLine(ReadOnlySpan<char>.Empty);
                        }
                        matchedDepth = depth + 1;
                        consumedLine = true;
                        goto DoneLine;
                    }
                    if (indent >= 4)
                    {
                        AdvanceWithVirtual(line[..lineEndLimit], ref idx, ref col, ref virtualSpaces, 4);
                        RecordBuffer(indented.Buffer);
                        AppendWithVirtualSpaces(indented.Buffer, virtualSpaces, line.Slice(idx, lineEndLimit - idx));
                        UpdateNodeEnd(indented, lineContentEnd, lineIndex);
                        indented.MarkDirtyUpwards();
                        consumedLine = true;
                        goto DoneLine;
                    }
                    goto DoneMatching;

                case MathBlockNode math:
                    if (!math.IsBracket)
                    {
                        if (!isBlank && indent <= 3 && contentSpan.TrimEnd().SequenceEqual("$$"))
                        {
                            RecordMathClose(math);
                            math.SawClose = true;
                            UpdateNodeEnd(math, lineContentEnd, lineIndex);
                            CloseFromDepth(depth, source, references, affectedTopKeys);
                            consumedLine = true;
                            goto DoneLine;
                        }
                        RecordBuffer(math.Buffer);
                        math.Buffer.AppendLine(line.Slice(idx, lineEndLimit - idx));
                        UpdateNodeEnd(math, lineContentEnd, lineIndex);
                        math.MarkDirtyUpwards();
                        consumedLine = true;
                        goto DoneLine;
                    }
                    else
                    {
                        var slice = line.Slice(idx, lineEndLimit - idx);
                        var closeAt = BlockMatchers.FindClosingBracketMath(slice);
                        if (closeAt >= 0)
                        {
                            var beforeClose = slice[..closeAt];
                            if (!beforeClose.IsWhiteSpace())
                            {
                                RecordBuffer(math.Buffer);
                                math.Buffer.AppendLine(beforeClose);
                            }
                            RecordMathClose(math);
                            math.SawClose = true;
                            UpdateNodeEnd(math, lineStartOffset + idx + closeAt + 2, lineIndex);
                            CloseFromDepth(depth, source, references, affectedTopKeys);
                            consumedLine = true;
                            goto DoneLine;
                        }
                        if (!slice.IsWhiteSpace())
                        {
                            RecordBuffer(math.Buffer);
                            math.Buffer.AppendLine(slice);
                        }
                        UpdateNodeEnd(math, lineContentEnd, lineIndex);
                        math.MarkDirtyUpwards();
                        consumedLine = true;
                        goto DoneLine;
                    }

                case HtmlBlockNode html:
                    if (html.HtmlKind is CommonMarkHtmlBlockKind.Type6BlockTag or CommonMarkHtmlBlockKind.Type7CompleteTag)
                    {
                        if (isBlank)
                        {
                            goto DoneMatching;
                        }
                        RecordBuffer(html.Buffer);
                        html.Buffer.AppendLine(line.Slice(idx, lineEndLimit - idx));
                        UpdateNodeEnd(html, lineContentEnd, lineIndex);
                        html.MarkDirtyUpwards();
                        consumedLine = true;
                        goto DoneLine;
                    }
                    else
                    {
                        var htmlLine = line.Slice(idx, lineEndLimit - idx);
                        RecordBuffer(html.Buffer);
                        html.Buffer.AppendLine(htmlLine);
                        UpdateNodeEnd(html, lineContentEnd, lineIndex);
                        html.MarkDirtyUpwards();
                        if (BlockMatchers.IsHtmlBlockCloseLine(htmlLine, html.HtmlKind, html.RawTagName))
                        {
                            CloseFromDepth(depth, source, references, affectedTopKeys);
                        }
                        consumedLine = true;
                        goto DoneLine;
                    }

                case TableNode table:
                    if (isBlank || (indent <= 3 && CanInterruptTable(contentSpan)))
                    {
                        goto DoneMatching;
                    }
                    RecordPopTableRow(table);
                    table.Rows.Add(new TableRowNode
                    {
                        IsHeader = false,
                        Line = new BlockLineSlice(lineStartOffset + idx, lineEndLimit - idx, lineIndex),
                        IsClosed = !isPartialStreamingLine,
                    });
                    UpdateNodeEnd(table, lineContentEnd, lineIndex);
                    table.MarkDirtyUpwards();
                    consumedLine = true;
                    goto DoneLine;

                case ParagraphNode:
                    if (isBlank)
                    {
                        goto DoneMatching;
                    }
                    matchedDepth = depth + 1;
                    continue;
            }
        }

    DoneMatching:
        if (consumedLine)
        {
            goto DoneLine;
        }

        // 记录是否全部开放块都已匹配
        var allMatched = matchedDepth == _openStack.Count;
        var lastMatchedContainer = FindContainerAtOrAbove(matchedDepth - 1);
        var openTip = _openStack[^1];
        var hasOpenParagraph = openTip is ParagraphNode;

        // 处理空行：关闭 unmatched 节点与段落，标记容器空行标志
        var remIndent = virtualSpaces + UnicodeClassification.MeasureIndent(line[..lineEndLimit], idx, col, out var remFirstNonWs);
        if (remFirstNonWs >= lineEndLimit)
        {
            CloseFromDepth(matchedDepth, source, references, affectedTopKeys);
            if (_openStack[^1] is ParagraphNode)
            {
                CloseFromDepth(_openStack.Count - 1, source, references, affectedTopKeys);
            }
            if (_openStack[^1] is ContainerNode blankContainer && blankContainer.Children.Count > 0)
            {
                RecordContainerBlank(blankContainer);
                blankContainer.LastChildEndedWithBlankLine = true;
            }
            goto DoneLine;
        }

        // Phase 2: 在最内层匹配容器下尝试开启新块（含容器嵌套循环，如 "> - # title"）
        var openedListOnThisLine = false;
        var container = lastMatchedContainer;
        var closeUnmatchedPending = !allMatched;

        while (true)
        {
            var curIndent = virtualSpaces + UnicodeClassification.MeasureIndent(line[..lineEndLimit], idx, col, out var curFirstNonWs);
            if (curFirstNonWs >= lineEndLimit)
            {
                break;
            }
            var rest = line.Slice(curFirstNonWs, lineEndLimit - curFirstNonWs);
            var absFirstNonWs = lineStartOffset + curFirstNonWs;
            var interruptingPara = hasOpenParagraph && _openStack[^1] is ParagraphNode;

            // 1. 缩进代码块（非打断段落，缩进 ≥ 4）
            if (curIndent >= 4 && !interruptingPara)
            {
                CloseUnmatchedIfNeeded(ref closeUnmatchedPending, matchedDepth, source, references, affectedTopKeys, ref container);
                CloseListIfDirectContainer(source, references, affectedTopKeys, ref container);
                AdvanceWithVirtual(line[..lineEndLimit], ref idx, ref col, ref virtualSpaces, 4);
                var code = new IndentedCodeNode
                {
                    StartOffset = lineStartOffset + idx,
                    EndOffset = lineContentEnd,
                    StartLine = lineIndex,
                    EndLine = lineIndex,
                };
                AppendWithVirtualSpaces(code.Buffer, virtualSpaces, line.Slice(idx, lineEndLimit - idx));
                AttachChild(container, code);
                _openStack.Add(code);
                consumedLine = true;
                break;
            }

            if (curIndent > 3)
            {
                break;
            }

            // 2. 自定义块级规则（IBlockSyntaxRule）
            if (!_profile.BlockRules.IsEmpty && TryOpenCustomBlockRule(
                rest, absFirstNonWs, lineContentEnd, lineIndex, interruptingPara,
                ref closeUnmatchedPending, matchedDepth, source, references, affectedTopKeys, ref container, ref idx, ref col, ref virtualSpaces, ref consumedLine))
            {
                if (consumedLine)
                {
                    break;
                }
                continue;
            }

            // 3. 块引用 '>'（注意列表项内比较运算符 "- > 25" 守卫）
            if (rest[0] == '>' && !(openedListOnThisLine && remendOptions.ComparisonOperators && BlockMatchers.IsComparisonOperatorAfterQuote(rest[1..])))
            {
                CloseUnmatchedIfNeeded(ref closeUnmatchedPending, matchedDepth, source, references, affectedTopKeys, ref container);
                CloseTipParagraphIfOpen(source, references, affectedTopKeys, ref container);
                CloseListIfDirectContainer(source, references, affectedTopKeys, ref container);
                hasOpenParagraph = false;

                var quote = new QuoteNode
                {
                    StartOffset = absFirstNonWs,
                    EndOffset = lineContentEnd,
                    StartLine = lineIndex,
                    EndLine = lineIndex,
                };
                AttachChild(container, quote);
                _openStack.Add(quote);
                container = quote;

                idx = curFirstNonWs + 1;
                col += curIndent + 1;
                virtualSpaces = 0;
                if (idx < lineEndLimit)
                {
                    if (line[idx] == ' ')
                    {
                        idx++;
                        col++;
                    }
                    else if (line[idx] == '\t')
                    {
                        var tabWidth = 4 - (col % 4);
                        idx++;
                        col++;
                        virtualSpaces = tabWidth - 1;
                    }
                }
                continue;
            }

            // 4. ATX 标题
            if (rest[0] == '#' && BlockMatchers.TryMatchAtxHeading(rest, out var atxLevel, out var atxStart, out var atxLen))
            {
                CloseUnmatchedIfNeeded(ref closeUnmatchedPending, matchedDepth, source, references, affectedTopKeys, ref container);
                CloseTipParagraphIfOpen(source, references, affectedTopKeys, ref container);
                CloseListIfDirectContainer(source, references, affectedTopKeys, ref container);
                var heading = new HeadingNode
                {
                    Level = atxLevel,
                    IsSetext = false,
                    StartOffset = absFirstNonWs,
                    EndOffset = lineContentEnd,
                    StartLine = lineIndex,
                    EndLine = lineIndex,
                    IsOpen = false,
                };
                if (atxLen > 0)
                {
                    heading.Lines.Add(new BlockLineSlice(absFirstNonWs + atxStart, atxLen, lineIndex));
                }
                AttachChild(container, heading);
                consumedLine = true;
                break;
            }

            // 5. Setext 标题下划线（仅当所有外层均匹配且栈顶是段落时）
            if (allMatched && _openStack[^1] is ParagraphNode setextPara
                && BlockMatchers.TryMatchSetextUnderline(rest, isPartialStreamingLine, remendOptions.SetextHeadings, out var setextLevel))
            {
                // 若段落开头是链接引用定义，先消费定义；剩余非空行才转 Setext 标题
                if (!setextPara.LeadingDefsChecked)
                {
                    ConsumeLeadingDefinitions(setextPara, source, references, affectedTopKeys);
                }
                if (setextPara.Lines.Count > 0)
                {
                    var parent = setextPara.Parent!;
                    var childIdx = parent.Children.Count - 1;
                    var heading = new HeadingNode
                    {
                        Level = setextLevel,
                        IsSetext = true,
                        StartOffset = setextPara.StartOffset,
                        EndOffset = lineContentEnd,
                        StartLine = setextPara.StartLine,
                        EndLine = lineIndex,
                        IsOpen = false,
                    };
                    heading.Lines.AddRange(setextPara.Lines);
                    ReplaceChild(parent, childIdx, heading);
                    _openStack.RemoveAt(_openStack.Count - 1);
                    consumedLine = true;
                    break;
                }
            }

            // 6. GFM 管道表格分隔行（当所有外层均匹配且栈顶是单行段落且列数与分隔行一致）
            if (_profile.EnableTables && allMatched && _openStack[^1] is ParagraphNode headerPara
                && headerPara.Lines.Count == 1
                && BlockMatchers.TryParseTableDelimiterRow(rest, out var tableCols))
            {
                var headerLine = headerPara.Lines[0];
                var headerSpan = source.GetSpan(headerLine.Start, headerLine.Length, ref _lineScratch);
                if (BlockMatchers.CountTableRowCells(headerSpan) == tableCols.Length)
                {
                    var parent = headerPara.Parent!;
                    var childIdx = parent.Children.Count - 1;
                    var table = new TableNode
                    {
                        Columns = tableCols,
                        StartOffset = headerPara.StartOffset,
                        EndOffset = lineContentEnd,
                        StartLine = headerPara.StartLine,
                        EndLine = lineIndex,
                        IsOpen = true,
                    };
                    table.Rows.Add(new TableRowNode
                    {
                        IsHeader = true,
                        Line = headerLine,
                        IsClosed = true,
                    });
                    ReplaceChild(parent, childIdx, table);
                    _openStack[^1] = table;
                    consumedLine = true;
                    break;
                }
            }

            // 7. 代码围栏 ``` / ~~~
            if (rest[0] is '`' or '~' && BlockMatchers.TryMatchFencedCodeOpen(rest, out var fChar, out var fLen, out var fInfo, out var fLang))
            {
                CloseUnmatchedIfNeeded(ref closeUnmatchedPending, matchedDepth, source, references, affectedTopKeys, ref container);
                CloseTipParagraphIfOpen(source, references, affectedTopKeys, ref container);
                CloseListIfDirectContainer(source, references, affectedTopKeys, ref container);
                var fenced = new FencedCodeNode
                {
                    FenceChar = fChar,
                    FenceLength = fLen,
                    Indent = curIndent,
                    Info = fInfo,
                    Language = fLang,
                    StartOffset = absFirstNonWs,
                    EndOffset = lineContentEnd,
                    StartLine = lineIndex,
                    EndLine = lineIndex,
                };
                AttachChild(container, fenced);
                _openStack.Add(fenced);
                consumedLine = true;
                break;
            }

            // 8. 自定义容器 :::
            if (_profile.EnableCustomContainers && rest[0] == ':' && BlockMatchers.TryMatchCustomContainerOpen(rest, out var ccLen, out var ccInfo, out var ccArgs))
            {
                CloseUnmatchedIfNeeded(ref closeUnmatchedPending, matchedDepth, source, references, affectedTopKeys, ref container);
                CloseTipParagraphIfOpen(source, references, affectedTopKeys, ref container);
                CloseListIfDirectContainer(source, references, affectedTopKeys, ref container);
                var cc = new CustomContainerNode
                {
                    FenceLength = ccLen,
                    Info = ccInfo,
                    Arguments = ccArgs,
                    StartOffset = absFirstNonWs,
                    EndOffset = lineContentEnd,
                    StartLine = lineIndex,
                    EndLine = lineIndex,
                };
                AttachChild(container, cc);
                _openStack.Add(cc);
                consumedLine = true;
                break;
            }

            // 9. 块级数学公式 $$ 或 \[
            if (_profile.EnableMathematics && rest[0] is '$' or '\\'
                && BlockMatchers.TryMatchMathBlockOpen(rest, out var isBracket, out var mathClosedSameLine, out var firstMathBody))
            {
                CloseUnmatchedIfNeeded(ref closeUnmatchedPending, matchedDepth, source, references, affectedTopKeys, ref container);
                CloseTipParagraphIfOpen(source, references, affectedTopKeys, ref container);
                CloseListIfDirectContainer(source, references, affectedTopKeys, ref container);
                var math = new MathBlockNode
                {
                    IsBracket = isBracket,
                    SawClose = mathClosedSameLine,
                    StartOffset = absFirstNonWs,
                    EndOffset = lineContentEnd,
                    StartLine = lineIndex,
                    EndLine = lineIndex,
                    IsOpen = !mathClosedSameLine,
                };
                if (!firstMathBody.IsWhiteSpace())
                {
                    math.Buffer.AppendLine(firstMathBody);
                }
                AttachChild(container, math);
                if (!mathClosedSameLine)
                {
                    _openStack.Add(math);
                }
                consumedLine = true;
                break;
            }

            // 10. 分隔线 *** / --- / ___（注意优先级在 Setext 之后、列表之前）
            if (rest[0] is '-' or '*' or '_' && BlockMatchers.IsThematicBreak(rest))
            {
                // 流式半行如果是单独 "---" 不应误判，但 3+ 个 '-' 已明确；1-2 个不进此分支
                CloseUnmatchedIfNeeded(ref closeUnmatchedPending, matchedDepth, source, references, affectedTopKeys, ref container);
                CloseTipParagraphIfOpen(source, references, affectedTopKeys, ref container);
                CloseListIfDirectContainer(source, references, affectedTopKeys, ref container);
                var hr = new ThematicBreakNode
                {
                    StartOffset = absFirstNonWs,
                    EndOffset = lineContentEnd,
                    StartLine = lineIndex,
                    EndLine = lineIndex,
                    IsOpen = false,
                };
                AttachChild(container, hr);
                consumedLine = true;
                break;
            }

            // 11. 列表项
            // CommonMark §5.2: 仅当当前直接匹配容器本身是段落（allMatched 且栈顶仍是段落）时，
            // 才受“空项或非 1. 有序列表不可打断段落”限制；已有列表内的后续兄弟项（如 2.）不受限。
            var listInterruptingPara = allMatched && interruptingPara;
            if ((rest[0] is '-' or '*' or '+' || char.IsAsciiDigit(rest[0]))
                && BlockMatchers.TryMatchListMarker(line[..lineEndLimit], curFirstNonWs, col + curIndent, listInterruptingPara,
                    out var isOrdered, out var bullet, out var startNum, out var afterMarkerIdx, out var contentCol, out var emptyAfterMarker))
            {
                CloseUnmatchedIfNeeded(ref closeUnmatchedPending, matchedDepth, source, references, affectedTopKeys, ref container);
                CloseTipParagraphIfOpen(source, references, affectedTopKeys, ref container);
                hasOpenParagraph = false;

                // 复用当前容器已打开的同类 ListNode，否则新建
                ListNode list;
                if (container is ListNode existingList && existingList.Matches(isOrdered, bullet))
                {
                    list = existingList;
                    if (list.LastChild is ListItemNode prevItem && prevItem.SawBlankLine)
                    {
                        RecordListState(list);
                        list.IsLoose = true;
                        list.SawBlankLineBetweenItems = true;
                    }
                }
                else
                {
                    if (container is ListNode)
                    {
                        // 列表类型/项目符号改变（如 * → +）：关闭旧列表回到父容器
                        var listDepth = _openStack.IndexOf(container);
                        if (listDepth > 0)
                        {
                            CloseFromDepth(listDepth, source, references, affectedTopKeys);
                            container = FindContainerAtOrAbove(listDepth - 1);
                        }
                    }
                    list = new ListNode
                    {
                        IsOrdered = isOrdered,
                        StartNumber = startNum,
                        Bullet = bullet,
                        StartOffset = absFirstNonWs,
                        EndOffset = lineContentEnd,
                        StartLine = lineIndex,
                        EndLine = lineIndex,
                    };
                    AttachChild(container, list);
                    _openStack.Add(list);
                }

                var item = new ListItemNode
                {
                    ContentColumn = contentCol,
                    StartedEmpty = emptyAfterMarker,
                    StartOffset = absFirstNonWs,
                    EndOffset = lineContentEnd,
                    StartLine = lineIndex,
                    EndLine = lineIndex,
                };
                AttachChild(list, item);
                _openStack.Add(item);
                container = item;
                openedListOnThisLine = true;

                if (emptyAfterMarker)
                {
                    consumedLine = true;
                    break;
                }
                // 前进到列表标记之后的内容起点
                if (afterMarkerIdx < lineEndLimit && line[afterMarkerIdx] == '\t')
                {
                    var advanceCols = contentCol - col;
                    AdvanceWithVirtual(line[..lineEndLimit], ref idx, ref col, ref virtualSpaces, advanceCols);
                }
                else
                {
                    idx = afterMarkerIdx;
                    col = contentCol;
                    virtualSpaces = 0;
                }
                continue;
            }

            // 12. 脚注定义 [^label]: ...（仅文档顶层，且不打断段落）
            if (_profile.EnableFootnotes && !interruptingPara && container == _document
                && rest[0] == '[' && BlockMatchers.TryMatchFootnoteOpen(rest, out var fnLabel, out var fnBodyStart))
            {
                CloseUnmatchedIfNeeded(ref closeUnmatchedPending, matchedDepth, source, references, affectedTopKeys, ref container);
                var fn = new FootnoteDefNode
                {
                    RawLabel = fnLabel,
                    NormalizedLabel = UnicodeClassification.NormalizeLabel(fnLabel.AsSpan()),
                    ContentColumn = 4,
                    StartOffset = absFirstNonWs,
                    EndOffset = lineContentEnd,
                    StartLine = lineIndex,
                    EndLine = lineIndex,
                };
                AttachChild(_document, fn);
                _openStack.Add(fn);
                container = fn;
                idx = curFirstNonWs + fnBodyStart;
                col += curIndent + fnBodyStart;
                virtualSpaces = 0;
                if (idx >= lineEndLimit)
                {
                    consumedLine = true;
                    break;
                }
                continue;
            }

            // 13. 定义列表释义行（": ..."）
            if (_profile.EnableDefinitionLists && rest[0] == ':' && BlockMatchers.TryMatchDefinitionMarker(rest, out var defBodyStart))
            {
                // 前一个子块是段落（不论仍开着或刚被空行闭合）或已是定义列表
                if (TryAttachDefinitionBody(container, absFirstNonWs, lineStartOffset + curFirstNonWs + defBodyStart, lineContentEnd, lineIndex,
                    ref closeUnmatchedPending, matchedDepth, source, references, affectedTopKeys, ref idx, ref col, ref virtualSpaces, curFirstNonWs + defBodyStart, curIndent))
                {
                    hasOpenParagraph = false;
                    container = (ContainerNode)_openStack[^1];
                    if (idx >= lineEndLimit)
                    {
                        consumedLine = true;
                        break;
                    }
                    continue;
                }
            }

            // 14. 白名单块级 HTML 组件（<details>, <think>, <tool-call> 等），优先于 CommonMark HTML 块
            if (rest[0] == '<' && HtmlTagReader.TryRead(rest, out var compTag)
                && _profile.Table.TryGet(compTag.Name, out var compSpec))
            {
                if (compSpec.Kind == ComponentKind.Block && !compTag.IsClosing)
                {
                    CloseUnmatchedIfNeeded(ref closeUnmatchedPending, matchedDepth, source, references, affectedTopKeys, ref container);
                    CloseTipParagraphIfOpen(source, references, affectedTopKeys, ref container);
                    CloseListIfDirectContainer(source, references, affectedTopKeys, ref container);
                    hasOpenParagraph = false;

                    var isRaw = compSpec.Content == ComponentContent.Raw;
                    var comp = new ComponentNode
                    {
                        Name = compSpec.Name,
                        Attributes = compTag.Attributes,
                        IsRaw = isRaw,
                        StartOffset = absFirstNonWs,
                        EndOffset = lineContentEnd,
                        StartLine = lineIndex,
                        EndLine = lineIndex,
                    };
                    AttachChild(container, comp);
                    _openStack.Add(comp);

                    idx = curFirstNonWs + compTag.Length;
                    col += curIndent + compTag.Length;
                    virtualSpaces = 0;

                    var afterTag = line.Slice(idx, lineEndLimit - idx);
                    var trimmedEnd = afterTag.TrimEnd();
                    var closingLiteral = $"</{comp.Name}>";
                    if (trimmedEnd.Length >= closingLiteral.Length && trimmedEnd.EndsWith(closingLiteral, StringComparison.OrdinalIgnoreCase))
                    {
                        var inlineBody = trimmedEnd[..^closingLiteral.Length];
                        comp.CloseAfterLine = true;
                        comp.SawClose = true;
                        if (isRaw)
                        {
                            if (!inlineBody.IsEmpty)
                            {
                                comp.RawBuffer.AppendLine(inlineBody);
                            }
                            CloseFromDepth(_openStack.Count - 1, source, references, affectedTopKeys);
                            consumedLine = true;
                            break;
                        }
                        lineEndLimit = idx + inlineBody.Length;
                        if (line.Slice(idx, lineEndLimit - idx).IsWhiteSpace())
                        {
                            CloseFromDepth(_openStack.Count - 1, source, references, affectedTopKeys);
                            consumedLine = true;
                            break;
                        }
                        container = comp;
                        continue;
                    }

                    if (isRaw)
                    {
                        if (!afterTag.IsWhiteSpace())
                        {
                            comp.RawBuffer.AppendLine(afterTag);
                        }
                        consumedLine = true;
                        break;
                    }
                    if (afterTag.IsWhiteSpace())
                    {
                        consumedLine = true;
                        break;
                    }
                    container = comp;
                    continue;
                }
                if (compSpec.Kind != ComponentKind.Block)
                {
                    // 行内白名单组件独占一行（如 <Citation id="1" />、<img ...>）：落入段落，不触发 CommonMark HTML Type 7
                    break;
                }
            }

            // 15. CommonMark HTML 块 Type 1–7
            if (rest[0] == '<' && BlockMatchers.TryMatchHtmlBlockOpen(rest, interruptingPara, out var htmlKind, out var rawTagName))
            {
                CloseUnmatchedIfNeeded(ref closeUnmatchedPending, matchedDepth, source, references, affectedTopKeys, ref container);
                CloseTipParagraphIfOpen(source, references, affectedTopKeys, ref container);
                CloseListIfDirectContainer(source, references, affectedTopKeys, ref container);
                var htmlNode = new HtmlBlockNode
                {
                    HtmlKind = htmlKind,
                    RawTagName = rawTagName,
                    StartOffset = absFirstNonWs,
                    EndOffset = lineContentEnd,
                    StartLine = lineIndex,
                    EndLine = lineIndex,
                };
                var fullHtmlLine = line.Slice(idx, lineEndLimit - idx);
                htmlNode.Buffer.AppendLine(fullHtmlLine);
                AttachChild(container, htmlNode);
                if (htmlKind <= CommonMarkHtmlBlockKind.Type5CData && BlockMatchers.IsHtmlBlockCloseLine(fullHtmlLine, htmlKind, rawTagName))
                {
                    htmlNode.IsOpen = false;
                }
                else
                {
                    _openStack.Add(htmlNode);
                }
                consumedLine = true;
                break;
            }

            // 没有任何块级开启语法匹配：退出循环，进入段落追加 / 懒续行
            break;
        }

        if (consumedLine)
        {
            goto DoneLine;
        }

        // Phase 3: 将本行剩余文本追加到段落（支持懒续行）
        var tailIndent = virtualSpaces + UnicodeClassification.MeasureIndent(line[..lineEndLimit], idx, col, out var tailFirstNonWs);
        if (tailFirstNonWs >= lineEndLimit)
        {
            goto DoneLine;
        }
        var paraSlice = new BlockLineSlice(lineStartOffset + tailFirstNonWs, lineEndLimit - tailFirstNonWs, lineIndex);

        // 懒续行：如果未全部匹配，但旧栈顶是段落（且没有新开容器），直接追加到该段落
        if (closeUnmatchedPending && _openStack[^1] is ParagraphNode lazyPara)
        {
            RecordPopParagraphLine(lazyPara);
            lazyPara.Lines.Add(paraSlice);
            UpdateNodeEnd(lazyPara, lineContentEnd, lineIndex);
            UpdateAncestorsEnd(lazyPara, lineContentEnd, lineIndex);
            lazyPara.MarkDirtyUpwards();
            goto DoneLine;
        }

        CloseUnmatchedIfNeeded(ref closeUnmatchedPending, matchedDepth, source, references, affectedTopKeys, ref container);

        if (_openStack[^1] is ParagraphNode activePara)
        {
            RecordPopParagraphLine(activePara);
            activePara.Lines.Add(paraSlice);
            UpdateNodeEnd(activePara, lineContentEnd, lineIndex);
            UpdateAncestorsEnd(activePara, lineContentEnd, lineIndex);
            activePara.MarkDirtyUpwards();
        }
        else
        {
            CloseListIfDirectContainer(source, references, affectedTopKeys, ref container);
            // 若在列表项内，检查是否之前有过空行（用于列表松紧判定）
            if (container is ListItemNode li && li.SawBlankLine && li.Children.Count > 0 && li.Parent is ListNode parentList && !parentList.IsLoose)
            {
                RecordListState(parentList);
                parentList.IsLoose = true;
            }
            var para = new ParagraphNode
            {
                StartOffset = paraSlice.Start,
                EndOffset = lineContentEnd,
                StartLine = lineIndex,
                EndLine = lineIndex,
                MaybeDefinition = line[tailFirstNonWs] == '[',
            };
            para.Lines.Add(paraSlice);
            AttachChild(container, para);
            _openStack.Add(para);
            UpdateAncestorsEnd(para, lineContentEnd, lineIndex);
        }

    DoneLine:
        // 若开放栈顶有标记为 CloseAfterLine 的组件或自定义块，本行处理完后立即关闭
        for (var d = _openStack.Count - 1; d >= 1; d--)
        {
            if (_openStack[d] is ComponentNode { CloseAfterLine: true } or CustomRuleBlockNode { State.CloseAfterLine: true })
            {
                CloseFromDepth(d, source, references, affectedTopKeys);
                break;
            }
        }
    }

    private bool TryOpenCustomBlockRule(
        ReadOnlySpan<char> rest,
        int absFirstNonWs,
        int lineContentEnd,
        int lineIndex,
        bool interruptingPara,
        ref bool closeUnmatchedPending,
        int matchedDepth,
        SourceBuffer source,
        ReferenceRegistry references,
        HashSet<int>? affectedTopKeys,
        ref ContainerNode container,
        ref int idx,
        ref int col,
        ref int virtualSpaces,
        ref bool consumedLine)
    {
        var first = rest[0];
        var rules = _profile.BlockRules;
        for (var i = 0; i < rules.Length; i++)
        {
            var rule = rules[i];
            if (rule.OpeningCharacters.IndexOf(first) < 0)
            {
                continue;
            }
            if (interruptingPara && !rule.CanInterruptParagraph)
            {
                continue;
            }
            if (!rule.TryOpen(rest, out var state))
            {
                continue;
            }
            CloseUnmatchedIfNeeded(ref closeUnmatchedPending, matchedDepth, source, references, affectedTopKeys, ref container);
            CloseTipParagraphIfOpen(source, references, affectedTopKeys, ref container);
            CloseListIfDirectContainer(source, references, affectedTopKeys, ref container);
            var node = new CustomRuleBlockNode
            {
                Rule = rule,
                State = state,
                StartOffset = absFirstNonWs,
                EndOffset = lineContentEnd,
                StartLine = lineIndex,
                EndLine = lineIndex,
                IsOpen = state.Kind != BlockRuleKind.SingleLine && !state.CloseAfterLine,
            };
            AttachChild(container, node);
            var consumed = Math.Clamp(state.ConsumedLength, 0, rest.Length);
            var remaining = rest[consumed..];
            if (state.Kind == BlockRuleKind.SingleLine)
            {
                if (!remaining.IsEmpty)
                {
                    node.RawBuffer.AppendLine(remaining);
                }
                consumedLine = true;
                return true;
            }
            if (state.Kind == BlockRuleKind.RawBody)
            {
                if (!remaining.IsWhiteSpace())
                {
                    node.RawBuffer.AppendLine(remaining);
                }
                if (!state.CloseAfterLine)
                {
                    _openStack.Add(node);
                }
                consumedLine = true;
                return true;
            }
            // Container
            _openStack.Add(node);
            if (remaining.IsWhiteSpace())
            {
                if (state.CloseAfterLine)
                {
                    CloseFromDepth(_openStack.Count - 1, source, references, affectedTopKeys);
                }
                consumedLine = true;
                return true;
            }
            container = node;
            idx += consumed;
            col += consumed;
            virtualSpaces = 0;
            return true;
        }
        return false;
    }

    private bool TryAttachDefinitionBody(
        ContainerNode container,
        int absMarkerOffset,
        int absBodyOffset,
        int lineContentEnd,
        int lineIndex,
        ref bool closeUnmatchedPending,
        int matchedDepth,
        SourceBuffer source,
        ReferenceRegistry references,
        HashSet<int>? affectedTopKeys,
        ref int idx,
        ref int col,
        ref int virtualSpaces,
        int newIdx,
        int curIndent)
    {
        // 1. 当前容器本身已是开放的 DefinitionListNode（同一术语下多个 ": " 释义）
        if (container is DefinitionListNode activeDl)
        {
            CloseUnmatchedIfNeeded(ref closeUnmatchedPending, matchedDepth, source, references, affectedTopKeys, ref container);
            CloseTipParagraphIfOpen(source, references, affectedTopKeys, ref container);
            UpdateNodeEnd(activeDl, lineContentEnd, lineIndex);
            col += newIdx - idx;
            idx = newIdx;
            virtualSpaces = 0;
            return true;
        }
        // 2. 前一个子块是仍开放的术语 ParagraphNode
        if (_openStack[^1] is ParagraphNode openTerm && openTerm.Parent == container && openTerm.Lines.Count > 0)
        {
            var termParent = openTerm.Parent!;
            var termIdx = termParent.Children.Count - 1;
            if (termIdx > 0 && termParent.Children[termIdx - 1] is DefinitionListNode prevDl)
            {
                RecordRemovedChild(termParent, termIdx, openTerm);
                termParent.Children.RemoveAt(termIdx);
                RecordPopDefinitionItem(prevDl);
                var newItem = new DefinitionItemNode();
                newItem.TermLines.AddRange(openTerm.Lines);
                prevDl.Items.Add(newItem);
                UpdateNodeEnd(prevDl, lineContentEnd, lineIndex);
                prevDl.IsOpen = true;
                _openStack[^1] = prevDl;
            }
            else
            {
                var dl = new DefinitionListNode
                {
                    StartOffset = openTerm.StartOffset,
                    EndOffset = lineContentEnd,
                    StartLine = openTerm.StartLine,
                    EndLine = lineIndex,
                };
                var item = new DefinitionItemNode();
                item.TermLines.AddRange(openTerm.Lines);
                dl.Items.Add(item);
                ReplaceChild(termParent, termIdx, dl);
                _openStack[^1] = dl;
            }
            col += newIdx - idx;
            idx = newIdx;
            virtualSpaces = 0;
            return true;
        }
        // 3. 前一个子块是刚闭合的术语 ParagraphNode（如术语与 ": " 之间隔了空行）
        if (container.LastChild is ParagraphNode closedTerm && closedTerm.Lines.Count > 0)
        {
            CloseUnmatchedIfNeeded(ref closeUnmatchedPending, matchedDepth, source, references, affectedTopKeys, ref container);
            var termIdx = container.Children.Count - 1;
            if (termIdx > 0 && container.Children[termIdx - 1] is DefinitionListNode prevDl)
            {
                RecordRemovedChild(container, termIdx, closedTerm);
                container.Children.RemoveAt(termIdx);
                RecordPopDefinitionItem(prevDl);
                var newItem = new DefinitionItemNode();
                newItem.TermLines.AddRange(closedTerm.Lines);
                prevDl.Items.Add(newItem);
                UpdateNodeEnd(prevDl, lineContentEnd, lineIndex);
                prevDl.IsOpen = true;
                _openStack.Add(prevDl);
            }
            else
            {
                var dl = new DefinitionListNode
                {
                    StartOffset = closedTerm.StartOffset,
                    EndOffset = lineContentEnd,
                    StartLine = closedTerm.StartLine,
                    EndLine = lineIndex,
                };
                var item = new DefinitionItemNode();
                item.TermLines.AddRange(closedTerm.Lines);
                dl.Items.Add(item);
                ReplaceChild(container, termIdx, dl);
                _openStack.Add(dl);
            }
            col += newIdx - idx;
            idx = newIdx;
            virtualSpaces = 0;
            return true;
        }
        // 4. 空行之后再次出现 ": " 释义，延续上一个刚闭合的 DefinitionListNode
        if (container.LastChild is DefinitionListNode lastDl && lastDl.Items.Count > 0)
        {
            CloseUnmatchedIfNeeded(ref closeUnmatchedPending, matchedDepth, source, references, affectedTopKeys, ref container);
            UpdateNodeEnd(lastDl, lineContentEnd, lineIndex);
            lastDl.IsOpen = true;
            _openStack.Add(lastDl);
            col += newIdx - idx;
            idx = newIdx;
            virtualSpaces = 0;
            return true;
        }
        return false;
    }

    private bool CanInterruptTable(ReadOnlySpan<char> content)
    {
        if (content.IsEmpty)
        {
            return true;
        }
        var c = content[0];
        if (c == '>' || c == '#' && BlockMatchers.TryMatchAtxHeading(content, out _, out _, out _))
        {
            return true;
        }
        if (c is '`' or '~' && BlockMatchers.TryMatchFencedCodeOpen(content, out _, out _, out _, out _))
        {
            return true;
        }
        if (c is '-' or '*' or '_' && BlockMatchers.IsThematicBreak(content))
        {
            return true;
        }
        if (c == ':' && _profile.EnableCustomContainers && BlockMatchers.TryMatchCustomContainerOpen(content, out _, out _, out _))
        {
            return true;
        }
        if (c is '$' or '\\' && _profile.EnableMathematics && BlockMatchers.TryMatchMathBlockOpen(content, out _, out _, out _))
        {
            return true;
        }
        if (c == '<')
        {
            if (HtmlTagReader.TryRead(content, out var tag) && _profile.Table.TryGet(tag.Name, out var spec) && spec.Kind == ComponentKind.Block)
            {
                return true;
            }
            if (BlockMatchers.TryMatchHtmlBlockOpen(content, interruptingParagraph: true, out _, out _))
            {
                return true;
            }
        }
        return false;
    }

    private bool HasOpenSameNameComponentBelow(int depth, string name)
    {
        for (var i = depth + 1; i < _openStack.Count; i++)
        {
            if (_openStack[i] is ComponentNode nested && string.Equals(nested.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private ContainerNode FindContainerAtOrAbove(int depth)
    {
        for (var i = Math.Min(depth, _openStack.Count - 1); i >= 0; i--)
        {
            if (_openStack[i] is ContainerNode c)
            {
                return c;
            }
        }
        return _document;
    }

    private void CloseUnmatchedIfNeeded(
        ref bool pending,
        int matchedDepth,
        SourceBuffer source,
        ReferenceRegistry references,
        HashSet<int>? affectedTopKeys,
        ref ContainerNode container)
    {
        if (!pending)
        {
            return;
        }
        pending = false;
        CloseFromDepth(matchedDepth, source, references, affectedTopKeys);
        container = FindContainerAtOrAbove(_openStack.Count - 1);
    }

    private void CloseTipParagraphIfOpen(
        SourceBuffer source,
        ReferenceRegistry references,
        HashSet<int>? affectedTopKeys,
        ref ContainerNode container)
    {
        if (_openStack.Count > 1 && _openStack[^1] is ParagraphNode)
        {
            CloseFromDepth(_openStack.Count - 1, source, references, affectedTopKeys);
            container = FindContainerAtOrAbove(_openStack.Count - 1);
        }
    }

    /// <summary><see cref="ListNode"/> 只能直接包含 <see cref="ListItemNode"/>；即将附加非列表项子块时先关闭开放的列表。</summary>
    private void CloseListIfDirectContainer(
        SourceBuffer source,
        ReferenceRegistry references,
        HashSet<int>? affectedTopKeys,
        ref ContainerNode container)
    {
        while (container is ListNode)
        {
            var depth = _openStack.LastIndexOf(container);
            if (depth <= 0)
            {
                break;
            }
            CloseFromDepth(depth, source, references, affectedTopKeys);
            container = FindContainerAtOrAbove(depth - 1);
        }
    }

    /// <summary>关闭栈中索引 <paramref name="fromDepth"/> 及以下的所有节点，并在段落闭合时消费前导链接引用定义。</summary>
    private void CloseFromDepth(int fromDepth, SourceBuffer source, ReferenceRegistry references, HashSet<int>? affectedTopKeys)
    {
        for (var i = _openStack.Count - 1; i >= fromDepth; i--)
        {
            var node = _openStack[i];
            RecordBlockMeta(node);
            node.IsOpen = false;
            _openStack.RemoveAt(i);

            if (node is ParagraphNode para && !para.LeadingDefsChecked)
            {
                ConsumeLeadingDefinitions(para, source, references, affectedTopKeys);
                if (para.Lines.Count == 0 && para.Parent is { } parent && parent.Children.Count > 0 && ReferenceEquals(parent.Children[^1], para))
                {
                    var removeIdx = parent.Children.Count - 1;
                    RecordRemovedChild(parent, removeIdx, para);
                    parent.Children.RemoveAt(removeIdx);
                    parent.Dirty = true;
                    parent.CachedModel = null;
                }
            }
            else if (node is TableNode table && table.Rows.Count > 0)
            {
                table.Rows[^1].IsClosed = true;
            }
        }
    }

    /// <summary>在段落闭合或转 Setext 前，消费开头连续的 CommonMark 链接引用定义。</summary>
    internal void ConsumeLeadingDefinitions(ParagraphNode para, SourceBuffer source, ReferenceRegistry references, HashSet<int>? affectedTopKeys)
    {
        if (para.LeadingDefsChecked || !para.MaybeDefinition || para.Lines.Count == 0)
        {
            para.LeadingDefsChecked = true;
            return;
        }
        RecordParagraphMeta(para);
        para.LeadingDefsChecked = true;

        // 拼接段落行以供 ConsumeLinkReferenceDefinitions 解析
        var totalLen = 0;
        for (var i = 0; i < para.Lines.Count; i++)
        {
            totalLen += para.Lines[i].Length + (i > 0 ? 1 : 0);
        }
        var buf = new char[totalLen];
        var pos = 0;
        for (var i = 0; i < para.Lines.Count; i++)
        {
            if (i > 0)
            {
                buf[pos++] = '\n';
            }
            var l = para.Lines[i];
            source.CopyTo(l.Start, l.Length, buf.AsSpan(pos, l.Length));
            pos += l.Length;
        }

        var consumed = BlockMatchers.ConsumeLinkReferenceDefinitions(buf.AsSpan(0, pos), references, affectedTopKeys);
        if (consumed <= 0)
        {
            return;
        }

        // 计算消费掉了多少整行
        var linesConsumed = 0;
        var cursor = 0;
        while (linesConsumed < para.Lines.Count && cursor < consumed)
        {
            var lineLen = para.Lines[linesConsumed].Length;
            cursor += lineLen + 1; // + '\n'
            linesConsumed++;
        }
        if (linesConsumed > 0)
        {
            if (_recordingUndo)
            {
                _undoLog.Add(new UndoOp { Kind = UndoKind.RestoreParagraphLines, Target = para, Aux = new List<BlockLineSlice>(para.Lines) });
            }
            if (linesConsumed >= para.Lines.Count)
            {
                para.Lines.Clear();
            }
            else
            {
                para.Lines.RemoveRange(0, linesConsumed);
                para.StartOffset = para.Lines[0].Start;
            }
            para.MarkDirtyUpwards();
        }
    }

    private void AttachChild(ContainerNode parent, BlockNode child)
    {
        if (parent is DefinitionListNode dl && dl.Items.Count > 0)
        {
            var item = dl.Items[^1];
            RecordPopDefinitionBody(item);
            child.Parent = dl;
            item.Definitions.Add(child);
            item.Dirty = true;
            item.CachedItem = null;
            dl.MarkDirtyUpwards();
            return;
        }
        if (parent is ListItemNode li)
        {
            if (li.SawBlankLine && li.Children.Count > 0 && li.Parent is ListNode pl && !pl.IsLoose)
            {
                RecordListState(pl);
                pl.IsLoose = true;
            }
            RecordListItemState(li);
            li.HadContentAfterMarker = true;
            li.CachedItem = null;
        }
        RecordPopChildren(parent);
        parent.AddChild(child);
        if (parent.LastChildEndedWithBlankLine)
        {
            RecordContainerBlank(parent);
            parent.LastChildEndedWithBlankLine = false;
        }
        parent.MarkDirtyUpwards();
    }

    private void ReplaceChild(ContainerNode parent, int childIndex, BlockNode replacement)
    {
        RecordReplaceChild(parent, childIndex, parent.Children[childIndex]);
        parent.Children[childIndex] = replacement;
        replacement.Parent = parent;
        parent.MarkDirtyUpwards();
    }

    private void UpdateNodeEnd(BlockNode node, int endOffset, int endLine)
    {
        if (node.EndOffset != endOffset || node.EndLine != endLine)
        {
            RecordBlockMeta(node);
            node.EndOffset = endOffset;
            node.EndLine = endLine;
        }
    }

    private void UpdateAncestorsEnd(BlockNode node, int endOffset, int endLine)
    {
        for (var cur = node.Parent; cur is not null && cur != _document; cur = cur.Parent)
        {
            UpdateNodeEnd(cur, endOffset, endLine);
        }
    }

    private static void AdvanceWithVirtual(ReadOnlySpan<char> line, ref int idx, ref int col, ref int virtualSpaces, int columns)
    {
        if (virtualSpaces >= columns)
        {
            virtualSpaces -= columns;
            col += columns;
            return;
        }
        var remain = columns - virtualSpaces;
        col += virtualSpaces;
        virtualSpaces = 0;
        UnicodeClassification.AdvanceColumns(line, ref idx, ref col, remain, out virtualSpaces);
    }

    private static void AppendSpacesLine(OpenBlockBuffer buffer, int count)
    {
        Span<char> sp = count <= 64 ? stackalloc char[count] : new char[count];
        sp.Fill(' ');
        buffer.AppendLine(sp);
    }

    private static void AppendWithVirtualSpaces(OpenBlockBuffer buffer, int virtualSpaces, ReadOnlySpan<char> slice)
    {
        if (virtualSpaces <= 0)
        {
            buffer.AppendLine(slice);
            return;
        }
        var total = virtualSpaces + slice.Length;
        Span<char> stack = total <= 256 ? stackalloc char[total] : [];
        var rented = total > 256 ? new char[total] : null;
        var dst = rented is not null ? rented.AsSpan() : stack;
        dst[..virtualSpaces].Fill(' ');
        slice.CopyTo(dst[virtualSpaces..]);
        buffer.AppendLine(dst);
    }

    // ---- 轻量 UndoLog 记录辅助（仅在 _recordingUndo == true 即末尾半行时生效） ----

    private void RecordPopChildren(ContainerNode parent)
    {
        if (_recordingUndo)
        {
            _undoLog.Add(new UndoOp { Kind = UndoKind.PopChildren, Target = parent, Int1 = parent.Children.Count });
        }
    }

    private void RecordReplaceChild(ContainerNode parent, int childIndex, BlockNode oldChild)
    {
        if (_recordingUndo)
        {
            _undoLog.Add(new UndoOp { Kind = UndoKind.ReplaceChild, Target = parent, Aux = oldChild, Int1 = childIndex });
        }
    }

    private void RecordRemovedChild(ContainerNode parent, int childIndex, BlockNode removedChild)
    {
        if (_recordingUndo)
        {
            _undoLog.Add(new UndoOp { Kind = UndoKind.RestoreRemovedChild, Target = parent, Aux = removedChild, Int1 = childIndex });
        }
    }

    private void RecordBlockMeta(BlockNode node)
    {
        if (_recordingUndo)
        {
            _undoLog.Add(new UndoOp { Kind = UndoKind.RestoreBlockMeta, Target = node, Int1 = node.EndOffset, Int2 = node.EndLine, Bool1 = node.IsOpen });
        }
    }

    private void RecordPopParagraphLine(ParagraphNode para)
    {
        if (_recordingUndo)
        {
            _undoLog.Add(new UndoOp { Kind = UndoKind.PopParagraphLine, Target = para, Int1 = para.Lines.Count });
        }
    }

    private void RecordPopTableRow(TableNode table)
    {
        if (_recordingUndo)
        {
            _undoLog.Add(new UndoOp { Kind = UndoKind.PopTableRow, Target = table, Int1 = table.Rows.Count });
        }
    }

    private void RecordBuffer(OpenBlockBuffer buffer)
    {
        if (_recordingUndo)
        {
            var cp = buffer.Save();
            _undoLog.Add(new UndoOp { Kind = UndoKind.RestoreBuffer, Target = buffer, Int1 = cp.Length, Int2 = cp.LineCount });
        }
    }

    private void RecordListState(ListNode list)
    {
        if (_recordingUndo)
        {
            _undoLog.Add(new UndoOp { Kind = UndoKind.RestoreListState, Target = list, Bool1 = list.IsLoose, Bool2 = list.SawBlankLineBetweenItems });
        }
    }

    private void RecordListItemState(ListItemNode item)
    {
        if (_recordingUndo)
        {
            _undoLog.Add(new UndoOp { Kind = UndoKind.RestoreListItemState, Target = item, Bool1 = item.SawBlankLine, Bool2 = item.HadContentAfterMarker });
        }
    }

    private void RecordComponentFlags(ComponentNode comp)
    {
        if (_recordingUndo)
        {
            _undoLog.Add(new UndoOp { Kind = UndoKind.RestoreComponentFlags, Target = comp, Bool1 = comp.SawClose, Bool2 = comp.CloseAfterLine });
        }
    }

    private void RecordFenceClose(FencedCodeNode code)
    {
        if (_recordingUndo)
        {
            _undoLog.Add(new UndoOp { Kind = UndoKind.RestoreFenceClose, Target = code, Bool1 = code.SawClose });
        }
    }

    private void RecordMathClose(MathBlockNode math)
    {
        if (_recordingUndo)
        {
            _undoLog.Add(new UndoOp { Kind = UndoKind.RestoreMathClose, Target = math, Bool1 = math.SawClose });
        }
    }

    private void RecordCustomContainerClose(CustomContainerNode cc)
    {
        if (_recordingUndo)
        {
            _undoLog.Add(new UndoOp { Kind = UndoKind.RestoreCustomContainerClose, Target = cc, Bool1 = cc.SawClose });
        }
    }

    private void RecordCustomRuleState(CustomRuleBlockNode node)
    {
        if (_recordingUndo)
        {
            _undoLog.Add(new UndoOp { Kind = UndoKind.RestoreCustomRuleState, Target = node, RuleState = node.State });
        }
    }

    private void RecordContainerBlank(ContainerNode container)
    {
        if (_recordingUndo)
        {
            _undoLog.Add(new UndoOp { Kind = UndoKind.RestoreContainerBlank, Target = container, Bool1 = container.LastChildEndedWithBlankLine });
        }
    }

    private void RecordPopDefinitionItem(DefinitionListNode dl)
    {
        if (_recordingUndo)
        {
            _undoLog.Add(new UndoOp { Kind = UndoKind.PopDefinitionItem, Target = dl, Int1 = dl.Items.Count });
        }
    }

    private void RecordPopDefinitionBody(DefinitionItemNode item)
    {
        if (_recordingUndo)
        {
            _undoLog.Add(new UndoOp { Kind = UndoKind.PopDefinitionBody, Target = item, Int1 = item.Definitions.Count });
        }
    }

    private void RecordParagraphMeta(ParagraphNode para)
    {
        if (_recordingUndo)
        {
            _undoLog.Add(new UndoOp { Kind = UndoKind.RestoreParagraphMeta, Target = para, Bool1 = para.LeadingDefsChecked, Int1 = para.StartOffset });
        }
    }

    private void RecordFootnoteBlank(FootnoteDefNode fn)
    {
        if (_recordingUndo)
        {
            _undoLog.Add(new UndoOp { Kind = UndoKind.RestoreFootnoteBlank, Target = fn, Bool1 = fn.SawBlankLine });
        }
    }
}
