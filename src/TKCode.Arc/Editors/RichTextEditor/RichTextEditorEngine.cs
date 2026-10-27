using System.Collections.Immutable;
using System.Globalization;

namespace TKCode.Arc;

/// <summary>Grapheme and word boundaries for caret movement and deletion.</summary>
internal static class RichTextEditorText
{
    public static int NextCaret(string text, int offset)
    {
        if (offset >= text.Length)
        {
            return text.Length;
        }
        return Math.Min(text.Length, offset + Math.Max(1, StringInfo.GetNextTextElementLength(text, offset)));
    }

    public static int PreviousCaret(string text, int offset)
    {
        if (offset <= 0)
        {
            return 0;
        }
        if (text[offset - 1] == '\n')
        {
            return offset - 1;
        }
        var start = offset >= 2 ? text.LastIndexOf('\n', offset - 2) + 1 : 0;
        var i = start;
        var previous = start;
        while (i < offset)
        {
            previous = i;
            i += Math.Max(1, StringInfo.GetNextTextElementLength(text, i));
        }
        return previous;
    }

    public static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_' || char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;

    private static int Class(char c) => char.IsWhiteSpace(c) ? 0 : IsWordChar(c) ? 1 : 2;

    public static int PreviousWord(string text, int offset)
    {
        var i = Math.Min(offset, text.Length);
        while (i > 0 && char.IsWhiteSpace(text[i - 1]) && text[i - 1] != '\n')
        {
            i--;
        }
        if (i > 0 && text[i - 1] == '\n')
        {
            return i == offset ? i - 1 : i;
        }
        if (i == 0)
        {
            return 0;
        }
        var kind = Class(text[i - 1]);
        while (i > 0 && Class(text[i - 1]) == kind && text[i - 1] != '\n')
        {
            i--;
        }
        return i;
    }

    public static int NextWord(string text, int offset)
    {
        var i = Math.Max(0, offset);
        while (i < text.Length && char.IsWhiteSpace(text[i]) && text[i] != '\n')
        {
            i++;
        }
        if (i < text.Length && text[i] == '\n')
        {
            return i == offset ? i + 1 : i;
        }
        if (i >= text.Length)
        {
            return text.Length;
        }
        var kind = Class(text[i]);
        while (i < text.Length && Class(text[i]) == kind && text[i] != '\n')
        {
            i++;
        }
        return i;
    }

    /// <summary>The word (or whitespace or punctuation run) around <paramref name="offset"/>, for double click.</summary>
    public static (int Start, int End) WordAt(string text, int offset)
    {
        if (text.Length == 0)
        {
            return (0, 0);
        }
        var at = Math.Clamp(offset, 0, text.Length - 1);
        if (text[at] == '\n' && at > 0 && offset == text.Length)
        {
            at--;
        }
        if (text[at] == '\n')
        {
            return (offset, offset);
        }
        var kind = Class(text[at]);
        var start = at;
        while (start > 0 && text[start - 1] != '\n' && Class(text[start - 1]) == kind)
        {
            start--;
        }
        var end = at + 1;
        while (end < text.Length && text[end] != '\n' && Class(text[end]) == kind)
        {
            end++;
        }
        return (start, end);
    }
}

internal enum RichTextEditorHistoryKind
{
    Type,
    Command
}

/// <summary>
/// The editor's own undo history (Arc: 300 snapshots; typing within one second coalesces, a space or a change of input
/// kind starts a new step, commands are always their own step). Snapshots share unchanged blocks, so they are cheap.
/// </summary>
internal sealed class RichTextEditorHistory
{
    public const int Limit = 300;
    public const long CoalesceMilliseconds = 1000;

    internal readonly record struct Snapshot(ImmutableList<RichTextEditorBlock> Blocks, RichTextEditorSelection Selection, RichTextEditorHistoryKind Kind, long At);

    private readonly List<Snapshot> _stack = new();
    private int _index = -1;

    public bool CanUndo => _index > 0;

    public bool CanRedo => _index >= 0 && _index < _stack.Count - 1;

    public int Count => _stack.Count;

    public void Reset(ImmutableList<RichTextEditorBlock> blocks, RichTextEditorSelection selection)
    {
        _stack.Clear();
        _stack.Add(new Snapshot(blocks, selection, RichTextEditorHistoryKind.Command, 0));
        _index = 0;
    }

    public void Record(ImmutableList<RichTextEditorBlock> blocks, RichTextEditorSelection selection, RichTextEditorHistoryKind kind, bool boundary, long now)
    {
        var top = _stack[_index];
        if (SameContent(top.Blocks, blocks))
        {
            _stack[_index] = top with { Blocks = blocks, Selection = selection };
            return;
        }
        if (_index < _stack.Count - 1)
        {
            _stack.RemoveRange(_index + 1, _stack.Count - _index - 1);
        }
        if (kind == RichTextEditorHistoryKind.Type && top.Kind == RichTextEditorHistoryKind.Type && _index > 0 && now - top.At < CoalesceMilliseconds && !boundary)
        {
            _stack[_index] = new Snapshot(blocks, selection, kind, top.At);
            return;
        }
        _stack.Add(new Snapshot(blocks, selection, kind, now));
        if (_stack.Count > Limit)
        {
            _stack.RemoveAt(0);
        }
        _index = _stack.Count - 1;
    }

    public Snapshot? Undo()
    {
        if (!CanUndo)
        {
            return null;
        }
        _index--;
        return _stack[_index];
    }

    public Snapshot? Redo()
    {
        if (!CanRedo)
        {
            return null;
        }
        _index++;
        return _stack[_index];
    }

    public static bool SameContent(ImmutableList<RichTextEditorBlock> a, ImmutableList<RichTextEditorBlock> b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }
        if (a.Count != b.Count)
        {
            return false;
        }
        using var left = a.GetEnumerator();
        using var right = b.GetEnumerator();
        while (left.MoveNext() && right.MoveNext())
        {
            if (!SameBlock(left.Current, right.Current))
            {
                return false;
            }
        }
        return true;
    }

    private static bool SameBlock(RichTextEditorBlock a, RichTextEditorBlock b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }
        if (a.Type != b.Type || a.Level != b.Level || a.Attrs != b.Attrs || !string.Equals(a.Text, b.Text, StringComparison.Ordinal) || a.SpanCount != b.SpanCount)
        {
            return false;
        }
        var left = a.Spans;
        var right = b.Spans;
        for (var i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>The outcome of typed text: whether a Markdown shortcut converted it, and whether a slash may open the block menu.</summary>
internal readonly record struct RichTextEditorInsertResult(bool Converted, bool SlashTyped);

/// <summary>
/// The editing core of <see cref="RichTextEditor"/>, independent of any visual: an immutable block document, the
/// selection, the caret's pending marks and the history, with Arc's editing rules (Markdown shortcuts, Enter and
/// Backspace at block edges, list indenting, block conversion, marks, links and paste).
/// </summary>
internal sealed partial class RichTextEditorEngine
{
    private enum InputKind
    {
        None,
        Insert,
        DeleteBackward,
        DeleteForward
    }

    private readonly RichTextEditorHistory _history = new();
    private ImmutableList<RichTextEditorBlock> _blocks;
    private RichTextEditorSelection _selection;
    private InputKind _lastInput;

    public RichTextEditorEngine()
    {
        _blocks = ImmutableList.Create(RichTextEditorBlock.EmptyParagraph);
        _selection = RichTextEditorSelection.Caret(0, 0);
        _history.Reset(_blocks, _selection);
    }

    /// <summary>Milliseconds clock for history coalescing (tests replace it).</summary>
    public Func<long> Clock { get; set; } = static () => Environment.TickCount64;

    /// <summary>True for the Docs profile: to-do shortcuts type in, and the color, alignment and language commands work.</summary>
    public bool Docs { get; set; }

    public ImmutableList<RichTextEditorBlock> Blocks => _blocks;

    public RichTextEditorSelection Selection => _selection;

    /// <summary>Marks the next typed text takes (set by toggling a mark at a collapsed caret, or after a Markdown mark).</summary>
    public RichTextEditorMarks? StoredMarks { get; private set; }

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    /// <summary>Raised after every change; the argument is true when the content changed.</summary>
    public event Action<bool>? Changed;

    /* ----------------------------------------------------------------------------------------------------------
     * Document and selection
     * -------------------------------------------------------------------------------------------------------- */

    /// <summary>Replaces the document. <paramref name="record"/> false resets the history (initial content).</summary>
    public void Load(ImmutableList<RichTextEditorBlock> blocks, bool record, bool caretAtEnd = false)
    {
        var normalized = Normalize(blocks);
        var caret = caretAtEnd ? End(normalized) : Clamp(normalized, RichTextEditorSelection.Caret(0, 0)).Focus;
        _blocks = normalized;
        _selection = Clamp(normalized, RichTextEditorSelection.Caret(caret));
        StoredMarks = null;
        _lastInput = InputKind.None;
        if (record)
        {
            _history.Record(_blocks, _selection, RichTextEditorHistoryKind.Command, true, Clock());
        }
        else
        {
            _history.Reset(_blocks, _selection);
        }
        Changed?.Invoke(true);
    }

    public void SetSelection(RichTextEditorSelection selection)
    {
        var next = Clamp(_blocks, selection);
        // A caret never rests inside a collapsed toggle: it stops at the toggle's end.
        next = new RichTextEditorSelection(Shown(next.Anchor), Shown(next.Focus));
        if (next == _selection)
        {
            return;
        }
        _selection = next;
        StoredMarks = null;
        _lastInput = InputKind.None;
        Changed?.Invoke(false);
    }

    public void SelectAll()
    {
        var last = _blocks.Count - 1;
        SetSelection(new RichTextEditorSelection(new RichTextEditorPosition(0, 0), new RichTextEditorPosition(last, _blocks[last].Length)));
    }

    public static RichTextEditorPosition End(ImmutableList<RichTextEditorBlock> blocks)
    {
        for (var i = blocks.Count - 1; i >= 0; i--)
        {
            if (blocks[i].IsTextual)
            {
                return new RichTextEditorPosition(i, blocks[i].Length);
            }
        }
        return new RichTextEditorPosition(0, 0);
    }

    /// <summary>Keeps positions inside the document; a caret never rests on a divider.</summary>
    public static RichTextEditorSelection Clamp(ImmutableList<RichTextEditorBlock> blocks, RichTextEditorSelection selection)
    {
        RichTextEditorPosition Fix(RichTextEditorPosition p)
        {
            var block = Math.Clamp(p.Block, 0, blocks.Count - 1);
            return new RichTextEditorPosition(block, Math.Clamp(p.Offset, 0, blocks[block].Length));
        }
        var anchor = Fix(selection.Anchor);
        var focus = Fix(selection.Focus);
        if (anchor == focus && blocks[focus.Block].IsDivider)
        {
            var caret = NearestTextual(blocks, focus.Block, forward: true);
            return RichTextEditorSelection.Caret(caret);
        }
        return new RichTextEditorSelection(anchor, focus);
    }

    public static RichTextEditorPosition NearestTextual(ImmutableList<RichTextEditorBlock> blocks, int index, bool forward)
    {
        for (var i = index; i >= 0 && i < blocks.Count; i += forward ? 1 : -1)
        {
            if (blocks[i].IsTextual)
            {
                return new RichTextEditorPosition(i, forward ? 0 : blocks[i].Length);
            }
        }
        for (var i = index; i >= 0 && i < blocks.Count; i += forward ? -1 : 1)
        {
            if (blocks[i].IsTextual)
            {
                return new RichTextEditorPosition(i, forward ? blocks[i].Length : 0);
            }
        }
        return new RichTextEditorPosition(0, 0);
    }

    /// <summary>
    /// Structural invariants: at least one block, the document does not end on a divider, an image or a table (a paragraph follows it), tables are complete grids,
    /// and list levels never skip (an item is at most one level deeper than the item before it).
    /// </summary>
    public static ImmutableList<RichTextEditorBlock> Normalize(ImmutableList<RichTextEditorBlock> blocks)
    {
        if (blocks.Count == 0)
        {
            return ImmutableList.Create(RichTextEditorBlock.EmptyParagraph);
        }
        var builder = blocks.ToBuilder();
        var previousLevel = -1;
        for (var i = 0; i < builder.Count; i++)
        {
            var block = builder[i];
            if (block.IsCell)
            {
                // A table is a complete grid of numbered cells, and two tables never touch.
                var table = RichTextEditorTables.Extent(builder, i);
                if (RichTextEditorTables.Repair(builder, table) is { } repaired)
                {
                    builder.RemoveRange(table.Start, table.End - table.Start);
                    builder.InsertRange(table.Start, repaired);
                    table = RichTextEditorTables.Extent(builder, table.Start);
                }
                i = table.End - 1;
                if (table.End < builder.Count && builder[table.End].StartsTable)
                {
                    builder.Insert(table.End, RichTextEditorBlock.EmptyParagraph);
                }
                previousLevel = -1;
                continue;
            }
            if (!block.IsList)
            {
                previousLevel = -1;
                continue;
            }
            var level = Math.Min(block.Level, previousLevel + 1);
            if (level != block.Level)
            {
                builder[i] = block.WithLevel(level);
            }
            previousLevel = level;
        }
        if (builder[^1].IsStructural)
        {
            builder.Add(RichTextEditorBlock.EmptyParagraph);
        }
        return builder.Count == blocks.Count && Same(builder, blocks) ? blocks : builder.ToImmutable();
    }

    private static bool Same(ImmutableList<RichTextEditorBlock>.Builder builder, ImmutableList<RichTextEditorBlock> blocks)
    {
        for (var i = 0; i < builder.Count; i++)
        {
            if (!ReferenceEquals(builder[i], blocks[i]))
            {
                return false;
            }
        }
        return true;
    }

    private void Commit(ImmutableList<RichTextEditorBlock> blocks, RichTextEditorSelection selection, RichTextEditorHistoryKind kind, bool boundary,
                        RichTextEditorMarks? storedMarks = null)
    {
        _blocks = Normalize(blocks);
        _selection = Clamp(_blocks, selection);
        // An edit that leaves the caret inside a collapsed toggle (indenting under it, pasting into it) unfolds it.
        for (var owner = RichTextEditorToggles.Owner(_blocks, HiddenMask, _selection.Focus.Block); owner >= 0;
             owner = RichTextEditorToggles.Owner(_blocks, HiddenMask, _selection.Focus.Block))
        {
            _blocks = _blocks.SetItem(owner, _blocks[owner].WithAttrs(_blocks[owner].Attrs with { Collapsed = false }));
        }
        StoredMarks = storedMarks;
        _history.Record(_blocks, _selection, kind, boundary, Clock());
        Changed?.Invoke(true);
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Queries
     * -------------------------------------------------------------------------------------------------------- */

    /// <summary>Marks typed text takes at <paramref name="caret"/>: bold, italic and strike carry from the character before; code and links only inside them.</summary>
    public static RichTextEditorMarks TypingMarks(ImmutableList<RichTextEditorBlock> blocks, RichTextEditorPosition caret)
    {
        var block = blocks[caret.Block];
        if (!block.IsTextual || block.IsCode)
        {
            return RichTextEditorMarks.None;
        }
        var offset = caret.Offset;
        if (offset > 0)
        {
            var before = block.MarksAt(offset - 1);
            var marks = before & RichTextEditorMarks.Carried;
            if ((before & RichTextEditorMarks.Code) != 0 && offset < block.Length && (block.MarksAt(offset) & RichTextEditorMarks.Code) != 0)
            {
                marks |= RichTextEditorMarks.Code;
            }
            return marks;
        }
        return block.Length > 0 ? block.MarksAt(0) & RichTextEditorMarks.Carried : RichTextEditorMarks.None;
    }

    public static string? TypingHref(ImmutableList<RichTextEditorBlock> blocks, RichTextEditorPosition caret)
    {
        var block = blocks[caret.Block];
        var offset = caret.Offset;
        if (offset <= 0 || offset >= block.Length || block.IsCode)
        {
            return null;
        }
        var href = block.HrefAt(offset - 1);
        return href is not null && string.Equals(href, block.HrefAt(offset), StringComparison.Ordinal) ? href : null;
    }

    public RichTextEditorMarks CurrentTypingMarks() => StoredMarks ?? TypingMarks(_blocks, _selection.Focus);

    /// <summary>The formatting at the selection, for toolbars.</summary>
    public RichTextFormats GetFormats()
    {
        var start = _selection.Start;
        var block = _blocks[start.Block];
        var type = block.Type;
        if (_selection.IsCollapsed)
        {
            var marks = CurrentTypingMarks();
            return new RichTextFormats((marks & RichTextEditorMarks.Bold) != 0, (marks & RichTextEditorMarks.Italic) != 0, (marks & RichTextEditorMarks.Strike) != 0,
                InsideCode(start, collapsed: true), LinkAt(start, collapsed: true), type,
                (marks & RichTextEditorMarks.Underline) != 0, marks.TextColor(), marks.Highlight(), block.Align, block.Language,
                (marks & RichTextEditorMarks.Superscript) != 0, (marks & RichTextEditorMarks.Subscript) != 0, marks.Size(), block.Spacing);
        }
        bool All(RichTextEditorMarks mark)
        {
            var any = false;
            foreach (var (index, from, to) in Segments(_blocks, _selection.Start, _selection.End))
            {
                var b = _blocks[index];
                if (b.IsCode || from == to)
                {
                    continue;
                }
                any = true;
                if (!b.AllHave(from, to, mark))
                {
                    return false;
                }
            }
            return any;
        }
        T Shared<T>(Func<RichTextEditorMarks, T> pick, T none) where T : struct
        {
            T? found = null;
            foreach (var (index, from, to) in Segments(_blocks, _selection.Start, _selection.End))
            {
                var b = _blocks[index];
                if (b.IsCode || from == to)
                {
                    continue;
                }
                foreach (var run in b.Slice(from, to))
                {
                    var value = pick(run.Marks);
                    if (found is { } seen && !EqualityComparer<T>.Default.Equals(seen, value))
                    {
                        return none;
                    }
                    found = value;
                }
            }
            return found ?? none;
        }
        return new RichTextFormats(All(RichTextEditorMarks.Bold), All(RichTextEditorMarks.Italic), All(RichTextEditorMarks.Strike),
            InsideCode(start, collapsed: false), LinkAt(start, collapsed: false), type,
            All(RichTextEditorMarks.Underline), Shared(static m => m.TextColor(), RichTextColor.Default), Shared(static m => m.Highlight(), RichTextColor.Default), block.Align, block.Language,
            All(RichTextEditorMarks.Superscript), All(RichTextEditorMarks.Subscript), Shared(static m => m.Size(), RichTextSize.Default), block.Spacing);
    }

    private bool InsideCode(RichTextEditorPosition position, bool collapsed)
    {
        var block = _blocks[position.Block];
        if (block.IsCode || !block.IsTextual)
        {
            return false;
        }
        var index = collapsed ? position.Offset - 1 : position.Offset;
        if (collapsed && index < 0)
        {
            index = 0;
        }
        return (block.MarksAt(index) & RichTextEditorMarks.Code) != 0;
    }

    private string? LinkAt(RichTextEditorPosition position, bool collapsed)
    {
        var block = _blocks[position.Block];
        if (!block.IsTextual)
        {
            return null;
        }
        if (collapsed)
        {
            return block.HrefAt(position.Offset - 1) ?? (position.Offset == 0 ? block.HrefAt(0) : null);
        }
        return block.HrefAt(position.Offset);
    }

    /// <summary>Per block pieces of a range: block index and the covered offsets.</summary>
    public static IEnumerable<(int Block, int From, int To)> Segments(ImmutableList<RichTextEditorBlock> blocks, RichTextEditorPosition start, RichTextEditorPosition end)
    {
        for (var i = start.Block; i <= end.Block; i++)
        {
            var from = i == start.Block ? start.Offset : 0;
            var to = i == end.Block ? end.Offset : blocks[i].Length;
            yield return (i, from, Math.Max(from, to));
        }
    }

    public static RichTextBlockType CurrentType(RichTextEditorBlock block) => block.Type;

    /// <summary>The selected content as blocks; a piece of a single block comes back as a paragraph so it pastes inline.</summary>
    public List<RichTextEditorBlock> GetFragment()
    {
        var result = new List<RichTextEditorBlock>();
        if (_selection.IsCollapsed)
        {
            return result;
        }
        if (CellSelection is { } rect)
        {
            return CellFragment(rect);
        }
        var start = _selection.Start;
        var end = _selection.End;
        if (start.Block == end.Block)
        {
            var block = _blocks[start.Block];
            if (block.IsAtomic)
            {
                result.Add(block);
                return result;
            }
            var whole = start.Offset == 0 && end.Offset == block.Length && !block.IsCell;
            result.Add(RichTextEditorBlock.Create(whole ? block.Type : RichTextBlockType.Paragraph, whole ? block.Level : 0, block.Slice(start.Offset, end.Offset), whole ? block.Attrs : default));
            return result;
        }
        foreach (var (index, from, to) in Segments(_blocks, start, end))
        {
            var block = _blocks[index];
            if (block.IsCell && !CoversTable(_blocks, index, start, end))
            {
                // Part of a table pastes as plain paragraphs, one per cell.
                result.Add(RichTextEditorBlock.Create(RichTextBlockType.Paragraph, 0, block.Slice(from, to)));
                continue;
            }
            result.Add(block.IsAtomic ? block : RichTextEditorBlock.Create(block.Type, block.Level, block.Slice(from, to), block.Attrs));
        }
        var minLevel = result.Where(b => b.IsList).Select(b => b.Level).DefaultIfEmpty(0).Min();
        if (minLevel > 0)
        {
            for (var i = 0; i < result.Count; i++)
            {
                if (result[i].IsList)
                {
                    result[i] = result[i].WithLevel(result[i].Level - minLevel);
                }
            }
        }
        return result;
    }

    /// <summary>True when the range <c>[start, end]</c> holds every cell of the table that has the cell at <paramref name="index"/>, in full.</summary>
    private static bool CoversTable(ImmutableList<RichTextEditorBlock> blocks, int index, RichTextEditorPosition start, RichTextEditorPosition end)
    {
        var table = RichTextEditorTables.Extent(blocks, index);
        var last = blocks[table.End - 1];
        return (start.Block < table.Start || (start.Block == table.Start && start.Offset == 0))
            && (end.Block >= table.End || (end.Block == table.End - 1 && end.Offset == last.Length));
    }

    /// <summary>Plain text of the selection: one line per block, soft breaks kept.</summary>
    public string GetSelectedText()
    {
        var lines = new List<string>();
        if (_selection.IsCollapsed)
        {
            return string.Empty;
        }
        if (CellSelection is { } rect)
        {
            return CellText(rect);
        }
        foreach (var (index, from, to) in Segments(_blocks, _selection.Start, _selection.End))
        {
            var block = _blocks[index];
            if (block.IsAtomic)
            {
                continue;
            }
            lines.Add(block.Text.Substring(from, to - from));
        }
        return string.Join("\n", lines);
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Typing
     * -------------------------------------------------------------------------------------------------------- */

    private static (ImmutableList<RichTextEditorBlock> Blocks, RichTextEditorPosition Caret) DeleteRange(ImmutableList<RichTextEditorBlock> blocks, RichTextEditorPosition start, RichTextEditorPosition end)
    {
        if (start >= end)
        {
            return (blocks, start);
        }
        if (start.Block == end.Block)
        {
            var block = blocks[start.Block];
            return block.IsAtomic ? (blocks, start) : (blocks.SetItem(start.Block, block.Delete(start.Offset, end.Offset)), start);
        }
        var first = blocks[start.Block];
        var last = blocks[end.Block];
        if (first.IsCell || last.IsCell)
        {
            return DeleteAcrossTables(blocks, start, end);
        }
        RichTextEditorBlock merged;
        var caretOffset = start.Offset;
        if (first.IsAtomic && last.IsAtomic)
        {
            merged = RichTextEditorBlock.EmptyParagraph;
            caretOffset = 0;
        }
        else if (first.IsAtomic)
        {
            merged = last.Delete(0, end.Offset);
            caretOffset = 0;
        }
        else if (last.IsAtomic)
        {
            merged = first.Delete(start.Offset, first.Length);
        }
        else
        {
            merged = first.Replace(start.Offset, first.Length, last.Slice(end.Offset, last.Length));
        }
        var result = blocks.RemoveRange(start.Block, end.Block - start.Block + 1).Insert(start.Block, merged);
        return (result, new RichTextEditorPosition(start.Block, caretOffset));
    }

    /// <summary>
    /// Deleting a range that starts or ends in a table: a table inside the range goes whole, the cells a range only touches are
    /// emptied (never merged into their neighbours), and the blocks outside the tables join as usual.
    /// </summary>
    private static (ImmutableList<RichTextEditorBlock> Blocks, RichTextEditorPosition Caret) DeleteAcrossTables(ImmutableList<RichTextEditorBlock> blocks, RichTextEditorPosition start, RichTextEditorPosition end)
    {
        var builder = blocks.ToBuilder();
        var caret = start;
        // Back to front, so indices stay valid.
        var i = end.Block;
        while (i >= start.Block)
        {
            var block = builder[i];
            if (block.IsCell)
            {
                var table = RichTextEditorTables.Extent(builder, i);
                var covered = CoversTable(blocks, i, start, end) && (start.Block < table.Start || end.Block >= table.End);
                var from = Math.Max(table.Start, start.Block);
                var to = Math.Min(table.End - 1, end.Block);
                if (covered)
                {
                    builder.RemoveRange(table.Start, table.End - table.Start);
                    if (table.Start <= start.Block)
                    {
                        // The caret was in or before the table; it lands where the table stood (fixed up below).
                        caret = new RichTextEditorPosition(Math.Max(0, table.Start - 1), caret.Offset);
                    }
                }
                else
                {
                    for (var k = from; k <= to; k++)
                    {
                        var cell = builder[k];
                        var cellFrom = k == start.Block ? start.Offset : 0;
                        var cellTo = k == end.Block ? end.Offset : cell.Length;
                        builder[k] = cell.Delete(cellFrom, Math.Max(cellFrom, cellTo));
                    }
                }
                i = table.Start - 1;
                continue;
            }
            if (block.IsAtomic)
            {
                builder.RemoveAt(i);
            }
            else if (i == start.Block)
            {
                builder[i] = block.Delete(start.Offset, Math.Max(start.Offset, i == end.Block ? end.Offset : block.Length));
            }
            else if (i == end.Block)
            {
                builder[i] = block.Delete(0, end.Offset);
            }
            else
            {
                builder.RemoveAt(i);
            }
            i--;
        }
        var result = builder.ToImmutable();
        if (result.Count == 0)
        {
            return (ImmutableList.Create(RichTextEditorBlock.EmptyParagraph), new RichTextEditorPosition(0, 0));
        }
        caret = new RichTextEditorPosition(Math.Clamp(caret.Block, 0, result.Count - 1), caret.Offset);
        caret = caret with { Offset = Math.Clamp(caret.Offset, 0, result[caret.Block].Length) };
        return (result, caret);
    }

    private (ImmutableList<RichTextEditorBlock> Blocks, RichTextEditorPosition Caret) DeleteSelection()
    {
        if (_selection.IsCollapsed)
        {
            return (_blocks, _selection.Focus);
        }
        // A rectangle of cells empties them all and leaves the caret in its first one.
        return CellSelection is { } rect
            ? (EmptyCells(_blocks, rect), new RichTextEditorPosition(rect.Table.IndexOf(rect.R1, rect.C1), 0))
            : DeleteRange(_blocks, _selection.Start, _selection.End);
    }

    /// <summary>
    /// Inserts typed (or committed IME) text at the selection, then applies Arc's Markdown shortcuts unless the text came
    /// from a composition.
    /// </summary>
    public RichTextEditorInsertResult InsertText(string text, bool composition = false)
    {
        text = CleanInput(text);
        if (text.Length == 0)
        {
            return default;
        }
        var typingMarks = StoredMarks ?? TypingMarks(_blocks, _selection.Focus);
        var typingHref = TypingHref(_blocks, _selection.Focus);
        if (!_selection.IsCollapsed)
        {
            var startBlock = _blocks[_selection.Start.Block];
            typingMarks = StoredMarks ?? (startBlock.IsTextual && _selection.Start.Offset < startBlock.Length
                ? startBlock.MarksAt(_selection.Start.Offset) & RichTextEditorMarks.Carried
                : TypingMarks(_blocks, _selection.Start));
            typingHref = null;
        }
        var (blocks, caret) = DeleteSelection();
        var block = blocks[caret.Block];
        if (!block.IsTextual)
        {
            return default;
        }
        if (block.IsCode)
        {
            typingMarks = RichTextEditorMarks.None;
            typingHref = null;
        }
        blocks = blocks.SetItem(caret.Block, block.Insert(caret.Offset, new RichTextEditorRun(text, typingMarks, typingHref)));
        caret = caret with { Offset = caret.Offset + text.Length };

        RichTextEditorMarks? stored = null;
        var converted = !composition && ApplyShortcuts(ref blocks, ref caret, text, ref stored, Docs);
        var boundary = composition || text == " " || _lastInput != InputKind.Insert;
        Commit(blocks, RichTextEditorSelection.Caret(caret), converted ? RichTextEditorHistoryKind.Command : RichTextEditorHistoryKind.Type, converted || boundary, stored);
        _lastInput = InputKind.Insert;
        var slash = !converted && !composition && text == "/" && SlashAllowed(caret);
        return new RichTextEditorInsertResult(converted, slash);
    }

    private static string CleanInput(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }
        var sb = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    continue;
                }
                sb.Append('\n');
                continue;
            }
            if (c == '​' || c == '\u007f' || (char.IsControl(c) && c is not ('\n' or '\t')))
            {
                continue;
            }
            sb.Append(c == ' ' ? ' ' : c);
        }
        return sb.ToString();
    }

    private bool SlashAllowed(RichTextEditorPosition caret)
    {
        var block = _blocks[caret.Block];
        var at = caret.Offset - 1;
        if (block.IsCode || block.IsCell || at < 0 || at >= block.Length || block.Text[at] != '/')
        {
            return false;
        }
        if ((block.MarksAt(at) & RichTextEditorMarks.Code) != 0)
        {
            return false;
        }
        if (at == 0)
        {
            return true;
        }
        var previous = block.Text[at - 1];
        return char.IsWhiteSpace(previous) || block.SpanAt(at - 1).Start != block.SpanAt(at).Start && block.SpanAt(at).Start == at;
    }

    /// <summary>Arc's Markdown shortcuts, applied right after the character that completes them.</summary>
    private static bool ApplyShortcuts(ref ImmutableList<RichTextEditorBlock> blocks, ref RichTextEditorPosition caret, string data, ref RichTextEditorMarks? stored, bool docs)
    {
        var block = blocks[caret.Block];
        if (block.IsCode || !block.IsTextual)
        {
            return false;
        }
        var before = block.Text[..caret.Offset];
        var paragraph = block.Type == RichTextBlockType.Paragraph;
        if (data == " " && paragraph)
        {
            RichTextBlockType? target = before switch
            {
                "[] " or "[ ] " or "[x] " when docs => RichTextBlockType.TodoList,
                "# " => RichTextBlockType.Heading1,
                "## " => RichTextBlockType.Heading2,
                "### " => RichTextBlockType.Heading3,
                "#### " when docs => RichTextBlockType.Heading4,
                "##### " when docs => RichTextBlockType.Heading5,
                "###### " when docs => RichTextBlockType.Heading6,
                "- " or "* " or "+ " => RichTextBlockType.BulletedList,
                "> " => RichTextBlockType.Quote,
                _ => OrderedPrefixRegex().IsMatch(before) ? RichTextBlockType.NumberedList : null
            };
            if (target is { } type)
            {
                blocks = blocks.SetItem(caret.Block, block.Delete(0, caret.Offset));
                ConvertBlock(ref blocks, caret.Block, type);
                if (before == "[x] ")
                {
                    blocks = blocks.SetItem(caret.Block, blocks[caret.Block].WithAttrs(new RichTextEditorBlockAttrs(Checked: true)));
                }
                caret = new RichTextEditorPosition(caret.Block, 0);
                return true;
            }
        }
        if (data == "-" && paragraph && before == "---" && block.Text == "---")
        {
            blocks = blocks.SetItem(caret.Block, RichTextEditorBlock.Divider);
            caret = new RichTextEditorPosition(ParagraphAfter(ref blocks, caret.Block), 0);
            return true;
        }
        if (data == ")" && paragraph && docs && ImageShortcutRegex().Match(block.Text) is { Success: true } image && image.Groups[2].Value is var src
            && RichTextEditorHtml.IsSafeImageUrl(src))
        {
            // ![alt](address) as a whole paragraph becomes an image.
            blocks = blocks.SetItem(caret.Block, RichTextEditorBlock.Image(src, image.Groups[1].Value));
            caret = new RichTextEditorPosition(ParagraphAfter(ref blocks, caret.Block), 0);
            return true;
        }
        if (data == "`" && paragraph && before == "```" && block.Text == "```")
        {
            blocks = blocks.SetItem(caret.Block, RichTextEditorBlock.Plain(RichTextBlockType.CodeBlock, string.Empty));
            caret = new RichTextEditorPosition(caret.Block, 0);
            return true;
        }
        if (caret.Offset == 0)
        {
            return false;
        }
        var span = block.SpanAt(caret.Offset - 1);
        if ((span.Marks & RichTextEditorMarks.Code) != 0)
        {
            return false;
        }
        var node = block.Text[span.Start..caret.Offset];
        foreach (var (regex, mark, wholeGroup, innerGroup) in s_inlineRules)
        {
            var match = regex().Match(node);
            if (!match.Success)
            {
                continue;
            }
            var whole = match.Groups[wholeGroup].Value;
            var inner = match.Groups[innerGroup].Value;
            var from = caret.Offset - whole.Length;
            var replaced = block.Replace(from, caret.Offset, new[] { new RichTextEditorRun(inner, span.Marks | mark, span.Href) });
            blocks = blocks.SetItem(caret.Block, replaced);
            caret = new RichTextEditorPosition(caret.Block, from + inner.Length);
            // The next keystroke lands outside the new mark (Arc inserts a zero width space for this).
            stored = span.Marks;
            return true;
        }
        return false;
    }

    private static readonly (Func<System.Text.RegularExpressions.Regex> Regex, RichTextEditorMarks Mark, int Whole, int Inner)[] s_inlineRules =
    {
        (StrongShortcutRegex, RichTextEditorMarks.Bold, 0, 2),
        (StrikeShortcutRegex, RichTextEditorMarks.Strike, 0, 1),
        (CodeShortcutRegex, RichTextEditorMarks.Code, 0, 1),
        (EmStarShortcutRegex, RichTextEditorMarks.Italic, 1, 2),
        (EmUnderscoreShortcutRegex, RichTextEditorMarks.Italic, 1, 2)
    };

    [System.Text.RegularExpressions.GeneratedRegex(@"^!\[([^\]]*)\]\(([^)\s]+)\)$")]
    private static partial System.Text.RegularExpressions.Regex ImageShortcutRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"^[0-9]+[.)] $")]
    private static partial System.Text.RegularExpressions.Regex OrderedPrefixRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"(\*\*|__)([^*_\s](?:[^*_]*[^*_\s])?)\1$")]
    private static partial System.Text.RegularExpressions.Regex StrongShortcutRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"~~([^~\s](?:[^~]*[^~\s])?)~~$")]
    private static partial System.Text.RegularExpressions.Regex StrikeShortcutRegex();

    [System.Text.RegularExpressions.GeneratedRegex("`([^`]+)`$")]
    private static partial System.Text.RegularExpressions.Regex CodeShortcutRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"(?:^|[^*A-Za-z0-9_])(\*([^*\s](?:[^*]*[^*\s])?)\*)$")]
    private static partial System.Text.RegularExpressions.Regex EmStarShortcutRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"(?:^|[^_A-Za-z0-9_])(_([^_\s](?:[^_]*[^_\s])?)_)$")]
    private static partial System.Text.RegularExpressions.Regex EmUnderscoreShortcutRegex();

    /// <summary>Makes sure a paragraph follows the block at <paramref name="index"/>; returns the paragraph's index.</summary>
    private static int ParagraphAfter(ref ImmutableList<RichTextEditorBlock> blocks, int index)
    {
        if (index + 1 < blocks.Count && blocks[index + 1].Type == RichTextBlockType.Paragraph)
        {
            return index + 1;
        }
        blocks = blocks.Insert(index + 1, RichTextEditorBlock.EmptyParagraph);
        return index + 1;
    }

    /// <summary>Enter: splits the block, or leaves an empty list item, quote or a code block's trailing empty line.</summary>
    public void InsertParagraph()
    {
        var (blocks, caret) = DeleteSelection();
        var block = blocks[caret.Block];
        if (block.IsImage)
        {
            Commit(blocks, RichTextEditorSelection.Caret(ParagraphAfter(ref blocks, caret.Block), 0), RichTextEditorHistoryKind.Command, true);
            return;
        }
        if (!block.IsTextual)
        {
            return;
        }
        var index = caret.Block;
        if (block.IsCell)
        {
            // Enter in a cell moves down the column, adding a row at the bottom.
            MoveInTable(blocks, caret.Block, rowStep: 1, columnStep: 0, addRowAtEnd: true);
            return;
        }
        if (block.IsCode)
        {
            var head = block.Text[..caret.Offset];
            var tail = block.Text[caret.Offset..];
            if (tail.Length == 0 && head.EndsWith('\n'))
            {
                blocks = blocks.SetItem(index, RichTextEditorBlock.Create(RichTextBlockType.CodeBlock, 0, new[] { new RichTextEditorRun(head[..^1]) }, block.Attrs));
                blocks = blocks.Insert(index + 1, RichTextEditorBlock.EmptyParagraph);
                Commit(blocks, RichTextEditorSelection.Caret(index + 1, 0), RichTextEditorHistoryKind.Command, true);
                return;
            }
            blocks = blocks.SetItem(index, block.Insert(caret.Offset, new RichTextEditorRun("\n")));
            Commit(blocks, RichTextEditorSelection.Caret(index, caret.Offset + 1), RichTextEditorHistoryKind.Command, true);
            return;
        }
        if (block.IsList && block.IsEmpty && !HasChildren(blocks, index))
        {
            Outdent(ref blocks, index);
            Commit(blocks, RichTextEditorSelection.Caret(index, 0), RichTextEditorHistoryKind.Command, true);
            return;
        }
        if (block.Type is RichTextBlockType.Quote or RichTextBlockType.Callout && block.IsEmpty)
        {
            blocks = blocks.SetItem(index, block.With(RichTextBlockType.Paragraph));
            Commit(blocks, RichTextEditorSelection.Caret(index, 0), RichTextEditorHistoryKind.Command, true);
            return;
        }
        var atEnd = caret.Offset >= block.Length;
        if (block.IsToggle && atEnd)
        {
            // Enter at the end of an open toggle starts its first nested item; at the end of a collapsed one, the toggle after it.
            if (block.IsCollapsed)
            {
                var after = DescendantsEnd(blocks, index);
                blocks = blocks.Insert(after, RichTextEditorBlock.Create(RichTextBlockType.Toggle, block.Level, Array.Empty<RichTextEditorRun>(), block.ContinuedAttrs));
                Commit(blocks, RichTextEditorSelection.Caret(after, 0), RichTextEditorHistoryKind.Command, true);
                return;
            }
            var childType = HasChildren(blocks, index) ? blocks[index + 1].Type : RichTextBlockType.BulletedList;
            blocks = blocks.Insert(index + 1, RichTextEditorBlock.Create(childType, block.Level + 1, Array.Empty<RichTextEditorRun>(), new RichTextEditorBlockAttrs(Spacing: block.Spacing)));
            Commit(blocks, RichTextEditorSelection.Caret(index + 1, 0), RichTextEditorHistoryKind.Command, true);
            return;
        }
        var left = block.Delete(caret.Offset, block.Length);
        var right = block.IsHeading && atEnd
            ? RichTextEditorBlock.Create(RichTextBlockType.Paragraph, 0, Array.Empty<RichTextEditorRun>(), block.ContinuedAttrs)
            : RichTextEditorBlock.Create(block.Type, block.Level, block.Slice(caret.Offset, block.Length), block.ContinuedAttrs);
        blocks = blocks.SetItem(index, left).Insert(index + 1, right);
        Commit(blocks, RichTextEditorSelection.Caret(index + 1, 0), RichTextEditorHistoryKind.Command, true);
    }

    /// <summary>Shift+Enter: a line break inside the block.</summary>
    public void InsertLineBreak()
    {
        var marks = CurrentTypingMarks() & RichTextEditorMarks.Carried;
        var (blocks, caret) = DeleteSelection();
        var block = blocks[caret.Block];
        if (!block.IsTextual)
        {
            return;
        }
        blocks = blocks.SetItem(caret.Block, block.Insert(caret.Offset, new RichTextEditorRun("\n", block.IsCode ? RichTextEditorMarks.None : marks)));
        Commit(blocks, RichTextEditorSelection.Caret(caret.Block, caret.Offset + 1), RichTextEditorHistoryKind.Command, true);
    }

    /// <summary>Tab inside a code block: two spaces.</summary>
    public void InsertCodeIndent()
    {
        var (blocks, caret) = DeleteSelection();
        var block = blocks[caret.Block];
        blocks = blocks.SetItem(caret.Block, block.Insert(caret.Offset, new RichTextEditorRun("  ")));
        Commit(blocks, RichTextEditorSelection.Caret(caret.Block, caret.Offset + 2), RichTextEditorHistoryKind.Command, true);
    }

    public void DeleteBackward(bool word = false)
    {
        if (!_selection.IsCollapsed)
        {
            DeleteSelectionAsTyping(InputKind.DeleteBackward);
            return;
        }
        var caret = _selection.Focus;
        var blocks = _blocks;
        var block = blocks[caret.Block];
        if (block.IsImage)
        {
            RemoveImage(caret.Block);
            return;
        }
        if (caret.Offset == 0)
        {
            if (block.IsCell)
            {
                return;
            }
            if (caret.Block > 0 && blocks[caret.Block - 1].IsCell && block.Type == RichTextBlockType.Paragraph)
            {
                // Backspace after a table steps into its last cell instead of merging into it.
                SetSelection(RichTextEditorSelection.Caret(NearestTextual(blocks, caret.Block - 1, forward: false)));
                return;
            }
            if (BackspaceAtStart(ref blocks, ref caret))
            {
                Commit(blocks, RichTextEditorSelection.Caret(caret), RichTextEditorHistoryKind.Command, true);
                _lastInput = InputKind.None;
                return;
            }
            if (caret.Block == 0)
            {
                return;
            }
            var previous = blocks[caret.Block - 1];
            var merged = previous.Append(block.Runs());
            blocks = blocks.RemoveAt(caret.Block).SetItem(caret.Block - 1, merged);
            CommitTyping(blocks, RichTextEditorSelection.Caret(caret.Block - 1, previous.Length), InputKind.DeleteBackward);
            return;
        }
        var from = word ? RichTextEditorText.PreviousWord(block.Text, caret.Offset) : RichTextEditorText.PreviousCaret(block.Text, caret.Offset);
        blocks = blocks.SetItem(caret.Block, block.Delete(from, caret.Offset));
        CommitTyping(blocks, RichTextEditorSelection.Caret(caret.Block, from), InputKind.DeleteBackward);
    }

    public void DeleteForward(bool word = false)
    {
        if (!_selection.IsCollapsed)
        {
            DeleteSelectionAsTyping(InputKind.DeleteForward);
            return;
        }
        var caret = _selection.Focus;
        var blocks = _blocks;
        var block = blocks[caret.Block];
        if (block.IsImage)
        {
            RemoveImage(caret.Block);
            return;
        }
        if (caret.Offset >= block.Length)
        {
            if (caret.Block + 1 >= blocks.Count)
            {
                return;
            }
            if (block.IsToggle && block.IsCollapsed && IsHidden(caret.Block + 1))
            {
                // Delete at the end of a collapsed toggle unfolds it rather than pull a hidden item into it.
                SetCollapsed(caret.Block, false);
                return;
            }
            var next = blocks[caret.Block + 1];
            if (block.IsCell || next.IsCell)
            {
                return;
            }
            blocks = next.IsAtomic
                ? blocks.RemoveAt(caret.Block + 1)
                : blocks.RemoveAt(caret.Block + 1).SetItem(caret.Block, block.Append(next.Runs()));
            CommitTyping(blocks, RichTextEditorSelection.Caret(caret), InputKind.DeleteForward);
            return;
        }
        var to = word ? RichTextEditorText.NextWord(block.Text, caret.Offset) : RichTextEditorText.NextCaret(block.Text, caret.Offset);
        blocks = blocks.SetItem(caret.Block, block.Delete(caret.Offset, to));
        CommitTyping(blocks, RichTextEditorSelection.Caret(caret), InputKind.DeleteForward);
    }

    private void DeleteSelectionAsTyping(InputKind kind)
    {
        var (blocks, caret) = DeleteSelection();
        CommitTyping(blocks, RichTextEditorSelection.Caret(caret), kind);
    }

    private void CommitTyping(ImmutableList<RichTextEditorBlock> blocks, RichTextEditorSelection selection, InputKind kind)
    {
        Commit(blocks, selection, RichTextEditorHistoryKind.Type, _lastInput != kind);
        _lastInput = kind;
    }

    /// <summary>Arc's Backspace at the start of a block: headings, quotes and code become paragraphs, list items move out, a divider before a paragraph goes.</summary>
    private static bool BackspaceAtStart(ref ImmutableList<RichTextEditorBlock> blocks, ref RichTextEditorPosition caret)
    {
        var index = caret.Block;
        var block = blocks[index];
        if (block.IsHeading || block.Type is RichTextBlockType.Quote or RichTextBlockType.CodeBlock or RichTextBlockType.Callout)
        {
            blocks = blocks.SetItem(index, block.With(RichTextBlockType.Paragraph));
            return true;
        }
        if (block.IsList)
        {
            Outdent(ref blocks, index);
            return true;
        }
        if (index > 0 && blocks[index - 1].IsAtomic)
        {
            blocks = blocks.RemoveAt(index - 1);
            caret = new RichTextEditorPosition(index - 1, 0);
            return true;
        }
        if (index == 0 && block.IsEmpty && blocks.Count > 1)
        {
            blocks = blocks.RemoveAt(0);
            caret = NearestTextual(blocks, 0, forward: true);
            return true;
        }
        return false;
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Blocks and lists
     * -------------------------------------------------------------------------------------------------------- */

    private static bool HasChildren(ImmutableList<RichTextEditorBlock> blocks, int index) =>
        index + 1 < blocks.Count && blocks[index + 1].IsList && blocks[index + 1].Level > blocks[index].Level;

    private static int DescendantsEnd(ImmutableList<RichTextEditorBlock> blocks, int index)
    {
        var level = blocks[index].Level;
        var end = index + 1;
        while (end < blocks.Count && blocks[end].IsList && blocks[end].Level > level)
        {
            end++;
        }
        return end;
    }

    /// <summary>Moves a list item one level out with its own children; a top level item becomes a paragraph.</summary>
    private static void Outdent(ref ImmutableList<RichTextEditorBlock> blocks, int index)
    {
        var item = blocks[index];
        if (!item.IsList)
        {
            return;
        }
        var end = DescendantsEnd(blocks, index);
        var builder = blocks.ToBuilder();
        if (item.Level > 0)
        {
            builder[index] = item.WithLevel(item.Level - 1);
            for (var i = index + 1; i < end; i++)
            {
                builder[i] = builder[i].WithLevel(builder[i].Level - 1);
            }
        }
        else
        {
            builder[index] = item.With(RichTextBlockType.Paragraph, 0);
            for (var i = index + 1; i < end; i++)
            {
                var child = builder[i];
                // Direct children join the list the item left (Arc moves them into a list of its type).
                var type = child.Level == 1 ? item.Type : child.Type;
                builder[i] = child.With(type, child.Level - 1);
            }
        }
        blocks = builder.ToImmutable();
    }

    /// <summary>Moves a list item under the previous item of its list, taking its children along.</summary>
    private static bool Indent(ref ImmutableList<RichTextEditorBlock> blocks, int index)
    {
        var item = blocks[index];
        if (!item.IsList)
        {
            return false;
        }
        var level = item.Level;
        var j = index - 1;
        var sublistType = (RichTextBlockType?)null;
        while (j >= 0 && blocks[j].IsList && blocks[j].Level > level)
        {
            if (blocks[j].Level == level + 1 && sublistType is null)
            {
                sublistType = blocks[j].Type;
            }
            j--;
        }
        // An item nests under a previous sibling of its own list, or under any toggle (whose nested items are any list).
        if (j < 0 || !blocks[j].IsList || blocks[j].Level != level || blocks[j].Type != item.Type && !blocks[j].IsToggle)
        {
            return false;
        }
        var end = DescendantsEnd(blocks, index);
        var builder = blocks.ToBuilder();
        builder[index] = item.With(sublistType ?? item.Type, level + 1);
        for (var i = index + 1; i < end; i++)
        {
            builder[i] = builder[i].WithLevel(builder[i].Level + 1);
        }
        blocks = builder.ToImmutable();
        return true;
    }

    /// <summary>Changes the type of the list holding <paramref name="index"/> (the item's siblings at its level).</summary>
    private static void SwapListType(ref ImmutableList<RichTextEditorBlock> blocks, int index, RichTextBlockType type)
    {
        var item = blocks[index];
        var level = item.Level;
        var builder = blocks.ToBuilder();
        for (var dir = -1; dir <= 1; dir += 2)
        {
            for (var i = dir < 0 ? index : index + 1; i >= 0 && i < blocks.Count; i += dir)
            {
                var candidate = blocks[i];
                if (!candidate.IsList || candidate.Level < level)
                {
                    break;
                }
                if (candidate.Level > level)
                {
                    continue;
                }
                if (candidate.Type != item.Type)
                {
                    break;
                }
                builder[i] = candidate.With(type);
            }
        }
        blocks = builder.ToImmutable();
    }

    /// <summary>Arc's convertBlock: turns one block into another type, keeping its content; choosing the type a block already has turns it back into a paragraph.</summary>
    private static void ConvertBlock(ref ImmutableList<RichTextEditorBlock> blocks, int index, RichTextBlockType type)
    {
        var block = blocks[index];
        if (block.IsAtomic || block.IsCell)
        {
            return;
        }
        var targetIsList = type is RichTextBlockType.BulletedList or RichTextBlockType.NumberedList or RichTextBlockType.TodoList or RichTextBlockType.Toggle;
        if (block.IsList)
        {
            if (targetIsList)
            {
                if (block.Type == type)
                {
                    Outdent(ref blocks, index);
                }
                else
                {
                    SwapListType(ref blocks, index, type);
                }
                return;
            }
            Outdent(ref blocks, index);
            if (blocks[index].Type == RichTextBlockType.Paragraph && type != RichTextBlockType.Paragraph)
            {
                ConvertBlock(ref blocks, index, type);
            }
            return;
        }
        if (targetIsList)
        {
            blocks = blocks.SetItem(index, block.With(type, 0));
            return;
        }
        var target = block.Type == type ? RichTextBlockType.Paragraph : type;
        var converted = block.With(target);
        if (target == RichTextBlockType.Callout)
        {
            converted = converted.WithAttrs(converted.Attrs with { Tone = RichTextColor.Blue });
        }
        blocks = blocks.SetItem(index, converted);
    }

    /// <summary>
    /// Applies a block type to the selected blocks (Arc's applyBlock): if every selected block already has it they turn
    /// back into paragraphs. A divider goes in place of an empty block or after the block (or its list), followed by a paragraph.
    /// </summary>
    public void SetBlockType(RichTextBlockType type)
    {
        if (type is RichTextBlockType.TodoList or RichTextBlockType.Callout or RichTextBlockType.Heading4 or RichTextBlockType.Heading5 or RichTextBlockType.Heading6
                or RichTextBlockType.Toggle && !Docs)
        {
            return;
        }
        var blocks = _blocks;
        var selection = _selection;
        ApplyBlock(ref blocks, ref selection, type, null);
        Commit(blocks, selection, RichTextEditorHistoryKind.Command, true);
    }

    private static void ApplyBlock(ref ImmutableList<RichTextEditorBlock> blocks, ref RichTextEditorSelection selection, RichTextBlockType type, int? target)
    {
        var index = target ?? selection.Focus.Block;
        if (type == RichTextBlockType.Divider)
        {
            var block = blocks[index];
            int at;
            if (block.IsEmpty && !block.IsList && block.IsTextual && !block.IsCell)
            {
                blocks = blocks.SetItem(index, RichTextEditorBlock.Divider);
                at = index;
            }
            else
            {
                var after = index;
                if (block.IsCell)
                {
                    after = RichTextEditorTables.Extent(blocks, index).End - 1;
                }
                if (block.IsList)
                {
                    while (after + 1 < blocks.Count && blocks[after + 1].IsList)
                    {
                        after++;
                    }
                }
                at = after + 1;
                blocks = blocks.Insert(at, RichTextEditorBlock.Divider);
            }
            selection = RichTextEditorSelection.Caret(ParagraphAfter(ref blocks, at), 0);
            return;
        }
        var targets = new List<int>();
        if (target is not null || selection.IsCollapsed)
        {
            targets.Add(index);
        }
        else
        {
            for (var i = selection.Start.Block; i <= selection.End.Block; i++)
            {
                if (blocks[i].IsTextual && !blocks[i].IsCell)
                {
                    targets.Add(i);
                }
            }
        }
        var current = blocks;
        var allSame = targets.All(i => current[i].Type == type);
        foreach (var i in targets)
        {
            if (allSame || blocks[i].Type != type)
            {
                ConvertBlock(ref blocks, i, type);
            }
        }
        if (type == RichTextBlockType.CodeBlock)
        {
            var focus = selection.Focus.Block;
            if (blocks[focus].IsCode && focus == blocks.Count - 1)
            {
                blocks = blocks.Add(RichTextEditorBlock.EmptyParagraph);
            }
        }
    }

    /// <summary>The slash menu's choice: removes "/query" and applies the block type to that block.</summary>
    public void ApplySlashChoice(int block, int slashOffset, int caretOffset, RichTextBlockType type)
    {
        var blocks = _blocks;
        if (block < 0 || block >= blocks.Count || !blocks[block].IsTextual)
        {
            return;
        }
        var current = blocks[block];
        var end = Math.Clamp(Math.Max(slashOffset + 1, caretOffset), 0, current.Length);
        blocks = blocks.SetItem(block, current.Delete(Math.Clamp(slashOffset, 0, end), end));
        var selection = RichTextEditorSelection.Caret(block, slashOffset);
        if (type == RichTextBlockType.TableCell)
        {
            CommitTable(blocks, selection.Focus, 3, 3);
            return;
        }
        if (type == RichTextBlockType.Image)
        {
            // Only the slash text goes; the editor asks the host for the picture.
            Commit(blocks, selection, RichTextEditorHistoryKind.Command, true);
            return;
        }
        ApplyBlock(ref blocks, ref selection, type, block);
        Commit(blocks, selection, RichTextEditorHistoryKind.Command, true);
    }

    /// <summary>Tab on a list item: one level in (false when it has no previous sibling).</summary>
    public bool IndentListItem()
    {
        var blocks = _blocks;
        var index = _selection.Anchor.Block;
        if (!blocks[index].IsList)
        {
            return false;
        }
        if (Indent(ref blocks, index))
        {
            Commit(blocks, _selection, RichTextEditorHistoryKind.Command, true);
        }
        return true;
    }

    /// <summary>Shift+Tab on a list item: one level out (a top level item becomes a paragraph).</summary>
    public bool OutdentListItem()
    {
        var blocks = _blocks;
        var index = _selection.Anchor.Block;
        if (!blocks[index].IsList)
        {
            return false;
        }
        Outdent(ref blocks, index);
        Commit(blocks, _selection, RichTextEditorHistoryKind.Command, true);
        return true;
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Marks and links
     * -------------------------------------------------------------------------------------------------------- */

    public static RichTextEditorMarks ToMarks(RichTextMark mark) => mark switch
    {
        RichTextMark.Bold => RichTextEditorMarks.Bold,
        RichTextMark.Italic => RichTextEditorMarks.Italic,
        RichTextMark.Strikethrough => RichTextEditorMarks.Strike,
        RichTextMark.Underline => RichTextEditorMarks.Underline,
        RichTextMark.Superscript => RichTextEditorMarks.Superscript,
        RichTextMark.Subscript => RichTextEditorMarks.Subscript,
        _ => RichTextEditorMarks.Code
    };

    /// <summary>Toggles a mark like execCommand: on when any selected character lacks it, off when all have it; at a caret it applies to the next typed text.</summary>
    public bool ToggleMark(RichTextMark mark)
    {
        if (mark == RichTextMark.Code)
        {
            return ToggleCode();
        }
        if (mark is RichTextMark.Underline or RichTextMark.Superscript or RichTextMark.Subscript && !Docs)
        {
            return false;
        }
        var flag = ToMarks(mark);
        // Superscript and subscript exclude each other: turning one on drops the other.
        var other = flag == RichTextEditorMarks.Superscript ? RichTextEditorMarks.Subscript
            : flag == RichTextEditorMarks.Subscript ? RichTextEditorMarks.Superscript
            : RichTextEditorMarks.None;
        if (_selection.IsCollapsed)
        {
            var block = _blocks[_selection.Focus.Block];
            if (block.IsCode)
            {
                return false;
            }
            var typing = CurrentTypingMarks() ^ flag;
            StoredMarks = (typing & flag) != 0 ? typing & ~other : typing;
            Changed?.Invoke(false);
            return true;
        }
        var segments = Segments(_blocks, _selection.Start, _selection.End).Where(s => !_blocks[s.Block].IsCode && _blocks[s.Block].IsTextual && s.From < s.To).ToList();
        if (segments.Count == 0)
        {
            return false;
        }
        var on = !segments.All(s => _blocks[s.Block].AllHave(s.From, s.To, flag));
        var blocks = _blocks;
        foreach (var (index, from, to) in segments)
        {
            var block = on && other != RichTextEditorMarks.None ? blocks[index].SetMark(from, to, other, false) : blocks[index];
            blocks = blocks.SetItem(index, block.SetMark(from, to, flag, on));
        }
        Commit(blocks, _selection, RichTextEditorHistoryKind.Command, true);
        return true;
    }

    /// <summary>Sets the text color of the selection, or of the next typed text at a caret (<see cref="RichTextColor.Default"/> clears it).</summary>
    public bool SetTextColor(RichTextColor color) => RestyleMarks(marks => marks.WithTextColor(color));

    /// <summary>Sets the highlight color of the selection, or of the next typed text at a caret (<see cref="RichTextColor.Default"/> clears it).</summary>
    public bool SetHighlight(RichTextColor color) => RestyleMarks(marks => marks.WithHighlight(color));

    private bool RestyleMarks(Func<RichTextEditorMarks, RichTextEditorMarks> restyle)
    {
        if (!Docs)
        {
            return false;
        }
        if (_selection.IsCollapsed)
        {
            if (_blocks[_selection.Focus.Block].IsCode)
            {
                return false;
            }
            StoredMarks = restyle(CurrentTypingMarks());
            Changed?.Invoke(false);
            return true;
        }
        var blocks = _blocks;
        var any = false;
        foreach (var (index, from, to) in Segments(_blocks, _selection.Start, _selection.End))
        {
            if (blocks[index].IsCode || !blocks[index].IsTextual || from >= to)
            {
                continue;
            }
            any = true;
            blocks = blocks.SetItem(index, blocks[index].Restyle(from, to, (marks, href) => (restyle(marks), href)));
        }
        if (!any)
        {
            return false;
        }
        Commit(blocks, _selection, RichTextEditorHistoryKind.Command, true);
        return true;
    }

    /// <summary>Aligns every selected paragraph, heading and quote.</summary>
    /// <summary>Sets the text size of the selection, or of the next typed text at a caret (<see cref="RichTextSize.Default"/> clears it).</summary>
    public bool SetSize(RichTextSize size) => RestyleMarks(marks => marks.WithSize(size));

    /// <summary>Sets the line spacing of the selected text blocks (code blocks and table cells keep their own).</summary>
    public bool SetLineSpacing(RichTextLineSpacing spacing)
    {
        if (!Docs)
        {
            return false;
        }
        var blocks = _blocks;
        for (var i = _selection.Start.Block; i <= _selection.End.Block; i++)
        {
            blocks = blocks.SetItem(i, blocks[i].WithAttrs(blocks[i].Attrs with { Spacing = spacing }));
        }
        if (RichTextEditorHistory.SameContent(blocks, _blocks))
        {
            return false;
        }
        Commit(blocks, _selection, RichTextEditorHistoryKind.Command, true);
        return true;
    }

    /// <summary>Sets the line spacing of the unit the block at <paramref name="index"/> starts (the block handle's unit: a whole callout box, a list item with its nested items).</summary>
    public bool SetLineSpacingAt(int index, RichTextLineSpacing spacing)
    {
        if (!Docs || index < 0 || index >= _blocks.Count)
        {
            return false;
        }
        var (start, end) = UnitExtent(_blocks, index);
        var blocks = _blocks;
        for (var i = start; i < end; i++)
        {
            blocks = blocks.SetItem(i, blocks[i].WithAttrs(blocks[i].Attrs with { Spacing = spacing }));
        }
        if (RichTextEditorHistory.SameContent(blocks, _blocks))
        {
            return false;
        }
        Commit(blocks, _selection, RichTextEditorHistoryKind.Command, true);
        return true;
    }

    public bool SetAlign(RichTextAlign align)
    {
        if (!Docs)
        {
            return false;
        }
        var blocks = _blocks;
        for (var i = _selection.Start.Block; i <= _selection.End.Block; i++)
        {
            if (blocks[i].IsCell)
            {
                // A table aligns by column.
                var table = RichTextEditorTables.Extent(blocks, i);
                var col = blocks[i].Col;
                for (var k = table.Start; k < table.End; k++)
                {
                    if (blocks[k].Col == col)
                    {
                        blocks = blocks.SetItem(k, blocks[k].WithAttrs(blocks[k].Attrs with { Align = align }));
                    }
                }
                continue;
            }
            blocks = blocks.SetItem(i, blocks[i].WithAttrs(blocks[i].Attrs with { Align = align }));
        }
        if (RichTextEditorHistory.SameContent(blocks, _blocks))
        {
            return false;
        }
        Commit(blocks, _selection, RichTextEditorHistoryKind.Command, true);
        return true;
    }

    /// <summary>Checks or unchecks the to-do item at <paramref name="block"/> (its caret stays where it was).</summary>
    public bool SetChecked(int block, bool isChecked)
    {
        if (block < 0 || block >= _blocks.Count || !_blocks[block].IsTodo || _blocks[block].IsChecked == isChecked)
        {
            return false;
        }
        Commit(_blocks.SetItem(block, _blocks[block].WithAttrs(_blocks[block].Attrs with { Checked = isChecked })), _selection, RichTextEditorHistoryKind.Command, true);
        return true;
    }

    /// <summary>Sets the language label of the code block holding the caret.</summary>
    public bool SetCodeLanguage(string? language)
    {
        var index = _selection.Focus.Block;
        if (!Docs || !_blocks[index].IsCode)
        {
            return false;
        }
        var next = _blocks[index].WithAttrs(_blocks[index].Attrs with { Language = language });
        if (ReferenceEquals(next, _blocks[index]))
        {
            return false;
        }
        Commit(_blocks.SetItem(index, next), _selection, RichTextEditorHistoryKind.Command, true);
        return true;
    }

    /// <summary>Arc's inline code toggle: unwraps the code around the selection start, or wraps a selection inside one block.</summary>
    private bool ToggleCode()
    {
        var start = _selection.Start;
        var block = _blocks[start.Block];
        if (block.IsCode || !block.IsTextual)
        {
            return false;
        }
        var collapsed = _selection.IsCollapsed;
        var probe = collapsed ? Math.Max(0, start.Offset - 1) : start.Offset;
        if (block.Length > 0 && (block.MarksAt(probe) & RichTextEditorMarks.Code) != 0)
        {
            var (from, to) = block.Extent(probe, span => (span.Marks & RichTextEditorMarks.Code) != 0);
            var blocks = _blocks.SetItem(start.Block, block.SetMark(from, to, RichTextEditorMarks.Code, false));
            Commit(blocks, RichTextEditorSelection.Range(start.Block, from, to), RichTextEditorHistoryKind.Command, true);
            return true;
        }
        var end = _selection.End;
        if (collapsed || start.Block != end.Block)
        {
            return false;
        }
        // Arc writes the range's plain text into the new code element: only marks shared by the whole range survive.
        var common = RichTextEditorMarks.Carried;
        string? href = null;
        var first = true;
        foreach (var run in block.Slice(start.Offset, end.Offset))
        {
            common &= run.Marks;
            href = first ? run.Href : string.Equals(href, run.Href, StringComparison.Ordinal) ? href : null;
            first = false;
        }
        var next = block.Replace(start.Offset, end.Offset, new[] { new RichTextEditorRun(block.Text[start.Offset..end.Offset], common | RichTextEditorMarks.Code, href) });
        Commit(_blocks.SetItem(start.Block, next), RichTextEditorSelection.Range(start.Block, start.Offset, end.Offset), RichTextEditorHistoryKind.Command, true);
        return true;
    }

    /// <summary>The range a link edit applies to: the whole link at the selection start, else the selection (null when collapsed outside a link).</summary>
    public RichTextEditorSelection? LinkRange(out string? href)
    {
        var start = _selection.Start;
        var block = _blocks[start.Block];
        href = null;
        if (block.IsTextual && !block.IsCode)
        {
            var probe = _selection.IsCollapsed ? start.Offset - 1 : start.Offset;
            if (_selection.IsCollapsed && probe < 0)
            {
                probe = 0;
            }
            var existing = block.HrefAt(probe);
            if (existing is not null)
            {
                href = existing;
                var (from, to) = block.LinkExtent(probe);
                return RichTextEditorSelection.Range(start.Block, from, to);
            }
        }
        return _selection.IsCollapsed ? null : _selection;
    }

    /// <summary>Links <paramref name="range"/> to <paramref name="url"/> (already normalized); an empty url removes the link.</summary>
    public void SetLink(RichTextEditorSelection range, string? url)
    {
        var blocks = _blocks;
        range = Clamp(blocks, range);
        var start = range.Start;
        var end = range.End;
        if (string.IsNullOrEmpty(url))
        {
            var block = blocks[start.Block];
            var probe = range.IsCollapsed ? Math.Max(0, start.Offset - 1) : start.Offset;
            if (block.IsTextual && block.HrefAt(probe) is not null)
            {
                var (from, to) = block.LinkExtent(probe);
                blocks = blocks.SetItem(start.Block, block.SetHref(from, to, null));
            }
            else
            {
                foreach (var (index, from, to) in Segments(blocks, start, end))
                {
                    blocks = blocks.SetItem(index, blocks[index].SetHref(from, to, null));
                }
            }
            Commit(blocks, RichTextEditorSelection.Caret(end), RichTextEditorHistoryKind.Command, true);
            return;
        }
        if (range.IsCollapsed)
        {
            return;
        }
        foreach (var (index, from, to) in Segments(blocks, start, end))
        {
            if (blocks[index].IsTextual && !blocks[index].IsCode)
            {
                blocks = blocks.SetItem(index, blocks[index].SetHref(from, to, url));
            }
        }
        Commit(blocks, RichTextEditorSelection.Caret(end), RichTextEditorHistoryKind.Command, true);
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Clipboard
     * -------------------------------------------------------------------------------------------------------- */

    /// <summary>
    /// Arc's paste: plain text (and anything pasted into code blocks or list items) goes in as text; one pasted paragraph
    /// goes in inline; several blocks split the current block around them. A table or tab-separated text pasted into a table fills its cells.
    /// </summary>
    public void Paste(string text, IReadOnlyList<RichTextEditorBlock>? fragment)
    {
        if (PasteCells(text, fragment))
        {
            return;
        }
        var typing = _selection.IsCollapsed ? CurrentTypingMarks() & RichTextEditorMarks.Carried : RichTextEditorMarks.None;
        var (blocks, caret) = DeleteSelection();
        var block = blocks[caret.Block];
        if (!block.IsTextual)
        {
            return;
        }
        var index = caret.Block;
        var single = fragment is { Count: 1 } && fragment[0].Type == RichTextBlockType.Paragraph;
        if (fragment is null || fragment.Count == 0 || block.IsCode || ((block.IsList || block.IsCell) && !single))
        {
            var plain = block.IsCode ? CleanInput(text) : WhitespaceRunRegex().Replace(CleanInput(text), " ");
            if (plain.Length == 0)
            {
                return;
            }
            blocks = blocks.SetItem(index, block.Insert(caret.Offset, new RichTextEditorRun(plain, block.IsCode ? RichTextEditorMarks.None : typing)));
            Commit(blocks, RichTextEditorSelection.Caret(index, caret.Offset + plain.Length), RichTextEditorHistoryKind.Command, true);
            return;
        }
        if (single)
        {
            var runs = fragment![0].Runs().Select(run => run with { Marks = run.Marks | typing }).ToList();
            var length = runs.Sum(run => run.Text.Length);
            blocks = blocks.SetItem(index, block.Replace(caret.Offset, caret.Offset, runs));
            Commit(blocks, RichTextEditorSelection.Caret(index, caret.Offset + length), RichTextEditorHistoryKind.Command, true);
            return;
        }
        var head = block.Delete(caret.Offset, block.Length);
        var rest = RichTextEditorBlock.Create(block.IsList ? RichTextBlockType.Paragraph : block.Type, block.Level, block.Slice(caret.Offset, block.Length), block.ContinuedAttrs);
        var insertAt = index + 1;
        blocks = blocks.SetItem(index, head);
        var inserted = new List<RichTextEditorBlock>(fragment!);
        if (!rest.IsEmpty)
        {
            inserted.Add(rest);
        }
        blocks = blocks.InsertRange(insertAt, inserted);
        var last = insertAt + fragment!.Count - 1;
        if (head.Type == RichTextBlockType.Paragraph && head.IsEmpty)
        {
            blocks = blocks.RemoveAt(index);
            last--;
        }
        var caretPosition = blocks[last].IsTextual ? new RichTextEditorPosition(last, blocks[last].Length) : NearestTextual(blocks, last, forward: true);
        Commit(blocks, RichTextEditorSelection.Caret(caretPosition), RichTextEditorHistoryKind.Command, true);
    }

    /// <summary>Removes the selection as one step (cut).</summary>
    public void DeleteSelectionAsCommand()
    {
        if (_selection.IsCollapsed)
        {
            return;
        }
        var (blocks, caret) = DeleteSelection();
        Commit(blocks, RichTextEditorSelection.Caret(caret), RichTextEditorHistoryKind.Command, true);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\s*\n\s*")]
    private static partial System.Text.RegularExpressions.Regex WhitespaceRunRegex();

    /* ----------------------------------------------------------------------------------------------------------
     * History
     * -------------------------------------------------------------------------------------------------------- */

    /// <summary>Records the end of a composition as a step boundary (Arc's compositionend).</summary>
    public void MarkBoundary() => _lastInput = InputKind.None;

    public bool Undo() => Restore(_history.Undo());

    public bool Redo() => Restore(_history.Redo());

    private bool Restore(RichTextEditorHistory.Snapshot? snapshot)
    {
        if (snapshot is not { } snap)
        {
            return false;
        }
        _blocks = snap.Blocks;
        _selection = Clamp(_blocks, snap.Selection);
        StoredMarks = null;
        _lastInput = InputKind.None;
        Changed?.Invoke(true);
        return true;
    }
}
