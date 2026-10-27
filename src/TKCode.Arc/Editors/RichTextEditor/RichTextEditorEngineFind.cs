using System.Collections.Immutable;

namespace TKCode.Arc;

/// <summary>A piece of a block's text that matches a search.</summary>
internal readonly record struct RichTextEditorMatch(int Block, int Start, int Length)
{
    public int End => Start + Length;
}

internal sealed partial class RichTextEditorEngine
{
    private ImmutableList<RichTextEditorBlock>? _hiddenFor;
    private bool[]? _hidden;

    /// <summary>The blocks folded away under collapsed toggles (true at their indices), or null when none are.</summary>
    public bool[]? HiddenMask
    {
        get
        {
            if (!ReferenceEquals(_hiddenFor, _blocks))
            {
                _hidden = RichTextEditorToggles.HiddenMask(_blocks);
                _hiddenFor = _blocks;
            }
            return _hidden;
        }
    }

    /// <summary>True when the block at <paramref name="index"/> is folded away under a collapsed toggle.</summary>
    public bool IsHidden(int index) => HiddenMask is { } mask && index >= 0 && index < mask.Length && mask[index];

    /// <summary>A position moved out of folded blocks: to the end of the collapsed toggle that hides it.</summary>
    private RichTextEditorPosition Shown(RichTextEditorPosition position)
    {
        var owner = RichTextEditorToggles.Owner(_blocks, HiddenMask, position.Block);
        return owner < 0 ? position : new RichTextEditorPosition(owner, _blocks[owner].Length);
    }

    /// <summary>
    /// Folds (or unfolds) the toggle at <paramref name="index"/>. A caret inside what folds away moves to the end of the toggle. Only
    /// the Docs profile has toggles. Returns false when nothing changed.
    /// </summary>
    public bool SetCollapsed(int index, bool collapsed)
    {
        if (!Docs || index < 0 || index >= _blocks.Count || !_blocks[index].IsToggle || _blocks[index].IsCollapsed == collapsed)
        {
            return false;
        }
        var blocks = _blocks.SetItem(index, _blocks[index].WithAttrs(_blocks[index].Attrs with { Collapsed = collapsed }));
        var hidden = RichTextEditorToggles.HiddenMask(blocks);
        var selection = _selection;
        if (hidden is not null && (hidden[Math.Min(selection.Focus.Block, hidden.Length - 1)] || hidden[Math.Min(selection.Anchor.Block, hidden.Length - 1)]))
        {
            var owner = RichTextEditorToggles.Owner(blocks, hidden, hidden[selection.Focus.Block] ? selection.Focus.Block : selection.Anchor.Block);
            selection = RichTextEditorSelection.Caret(owner < 0 ? index : owner, blocks[owner < 0 ? index : owner].Length);
        }
        Commit(blocks, selection, RichTextEditorHistoryKind.Command, true);
        _lastInput = InputKind.None;
        return true;
    }

    /// <summary>
    /// Every place <paramref name="query"/> occurs, in document order. Matches stay inside one block (a paragraph, a list item, a
    /// cell, a code block); images, dividers and the cells a merged cell covers hold no text to match.
    /// </summary>
    public List<RichTextEditorMatch> FindAll(string query, bool matchCase)
    {
        var matches = new List<RichTextEditorMatch>();
        if (string.IsNullOrEmpty(query))
        {
            return matches;
        }
        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        for (var i = 0; i < _blocks.Count; i++)
        {
            var block = _blocks[i];
            if (!block.IsTextual || block.Length < query.Length)
            {
                continue;
            }
            var at = block.Text.IndexOf(query, comparison);
            while (at >= 0)
            {
                matches.Add(new RichTextEditorMatch(i, at, query.Length));
                at = at + query.Length <= block.Length ? block.Text.IndexOf(query, at + query.Length, comparison) : -1;
            }
        }
        return matches;
    }

    /// <summary>
    /// The match to show first: the first one that starts at or after the selection's start (wrapping to the first), or -1 when there is none.
    /// </summary>
    public int MatchFrom(List<RichTextEditorMatch> matches, RichTextEditorPosition from)
    {
        if (matches.Count == 0)
        {
            return -1;
        }
        var index = matches.FindIndex(m => new RichTextEditorPosition(m.Block, m.Start) >= from);
        return index < 0 ? 0 : index;
    }

    /// <summary>The block with <paramref name="match"/> replaced by <paramref name="replacement"/>, which takes the style of the match's first character.</summary>
    private static RichTextEditorBlock ReplaceIn(RichTextEditorBlock block, RichTextEditorMatch match, string replacement)
    {
        var text = replacement.Replace("\r\n", "\n").Replace('\r', '\n');
        if (block.IsCode)
        {
            return block.Replace(match.Start, match.End, text.Length == 0 ? Array.Empty<RichTextEditorRun>() : new[] { new RichTextEditorRun(text) });
        }
        text = text.Replace('\n', ' ');
        var run = new RichTextEditorRun(text, block.MarksAt(match.Start), block.HrefAt(match.Start));
        return block.Replace(match.Start, match.End, text.Length == 0 ? Array.Empty<RichTextEditorRun>() : new[] { run });
    }

    /// <summary>True when the text at <paramref name="match"/> still reads <paramref name="query"/>.</summary>
    private bool StillMatches(RichTextEditorMatch match, string query, bool matchCase) =>
        match.Block < _blocks.Count && _blocks[match.Block].IsTextual && match.End <= _blocks[match.Block].Length
        && string.Compare(_blocks[match.Block].Text, match.Start, query, 0, query.Length, matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) == 0
        && match.Length == query.Length;

    /// <summary>Replaces one match (as one step) and selects the new text. False when the text there no longer matches.</summary>
    public bool ReplaceMatch(RichTextEditorMatch match, string query, string replacement, bool matchCase)
    {
        if (!StillMatches(match, query, matchCase))
        {
            return false;
        }
        var block = ReplaceIn(_blocks[match.Block], match, replacement);
        var length = block.Length - (_blocks[match.Block].Length - match.Length);
        Commit(_blocks.SetItem(match.Block, block), RichTextEditorSelection.Range(match.Block, match.Start, match.Start + length), RichTextEditorHistoryKind.Command, true);
        _lastInput = InputKind.None;
        return true;
    }

    /// <summary>Replaces every match as one step; returns how many there were. The caret goes to the end of the last replacement.</summary>
    public int ReplaceAll(string query, string replacement, bool matchCase)
    {
        var matches = FindAll(query, matchCase);
        if (matches.Count == 0)
        {
            return 0;
        }
        var blocks = _blocks;
        // Back to front, so the earlier offsets of a block stay valid.
        for (var k = matches.Count - 1; k >= 0; k--)
        {
            var match = matches[k];
            blocks = blocks.SetItem(match.Block, ReplaceIn(blocks[match.Block], match, replacement));
        }
        var last = matches[^1];
        var end = Math.Min(blocks[last.Block].Length, blocks[last.Block].Length - (_blocks[last.Block].Length - last.End));
        Commit(blocks, RichTextEditorSelection.Caret(last.Block, Math.Max(0, end)), RichTextEditorHistoryKind.Command, true);
        _lastInput = InputKind.None;
        return matches.Count;
    }
}
