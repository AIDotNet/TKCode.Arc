using System.Diagnostics;

namespace TKCode.Arc;

/// <summary>What kind of edit <see cref="PromptUndoHistory.Record"/> receives.</summary>
internal enum PromptEditKind
{
    /// <summary>One typed character with nothing selected.</summary>
    Typing,

    /// <summary>One Backspace or Delete with nothing selected (a character or a whole chip).</summary>
    Deleting,

    /// <summary>Anything else: paste, cut, a selection replaced, a chip inserted from the list, a word deleted.</summary>
    Other
}

/// <summary>
/// The undo stack of <see cref="PromptTemplateEditor"/>, as text snapshots (chips come back with the text, since they
/// are parsed from it). Arc's grouping: consecutive typing joins one step while the pauses between keys stay within
/// <see cref="GroupDelay"/> (Arc measured between 0.8 and 1.1 s); a typed space or line break starts a new step, so
/// "hello world" undoes " world" first. Consecutive deletions group the same way; every other edit is a step of its own.
/// Undo and redo end the group in progress.
/// </summary>
internal sealed class PromptUndoHistory
{
    /// <summary>How many steps are kept.</summary>
    public const int Limit = 200;

    private static readonly Stopwatch s_clock = Stopwatch.StartNew();

    private readonly List<string> _undo = new();
    private readonly List<string> _redo = new();
    private PromptEditKind? _lastKind;
    private TimeSpan _lastTime;
    private int _lastStart;
    private int _lastEnd;

    /// <summary>The longest pause between keys that still joins one step (default 1 s).</summary>
    public TimeSpan GroupDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>The time source (tests replace it).</summary>
    public Func<TimeSpan> Clock { get; set; } = static () => s_clock.Elapsed;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>The number of steps that can be undone (tests).</summary>
    public int UndoCount => _undo.Count;

    /// <summary>
    /// An edit changed the text from <paramref name="before"/> to <paramref name="after"/>: starts a new step (remembering
    /// <paramref name="before"/>) unless it continues the group in progress. A new step clears the redo stack.
    /// </summary>
    public void Record(string before, string after, PromptEditKind kind)
    {
        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return;
        }
        var now = Clock();
        var (start, removed, inserted) = Change(before, after);
        var joins = _lastKind == kind && kind != PromptEditKind.Other && now - _lastTime <= GroupDelay && kind switch
        {
            // Typing continues where the last character went, unless it is a space or a line break.
            PromptEditKind.Typing => start == _lastEnd && inserted == 1 && !char.IsWhiteSpace(after[start]),
            // Backspace ends where the last deletion began; Delete keeps its place.
            _ => start + removed == _lastStart || start == _lastStart
        };
        if (!joins)
        {
            Push(_undo, before);
            _redo.Clear();
        }
        _lastKind = kind;
        _lastTime = now;
        _lastStart = start;
        _lastEnd = start + inserted;
    }

    /// <summary>The next edit starts a new step.</summary>
    public void Break() => _lastKind = null;

    /// <summary>Forgets every step.</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _lastKind = null;
    }

    /// <summary>The text before the last step, or null when there is none; <paramref name="current"/> becomes redoable.</summary>
    public string? Undo(string current) => Move(_undo, _redo, current);

    /// <summary>The text the last undo left, or null when there is none; <paramref name="current"/> becomes undoable.</summary>
    public string? Redo(string current) => Move(_redo, _undo, current);

    private string? Move(List<string> from, List<string> to, string current)
    {
        _lastKind = null;
        if (from.Count == 0)
        {
            return null;
        }
        var text = from[^1];
        from.RemoveAt(from.Count - 1);
        Push(to, current);
        return text;
    }

    private static void Push(List<string> stack, string text)
    {
        stack.Add(text);
        if (stack.Count > Limit)
        {
            stack.RemoveAt(0);
        }
    }

    /// <summary>Where the texts differ: the start, the characters removed from the first and inserted into the second.</summary>
    internal static (int Start, int Removed, int Inserted) Change(string before, string after)
    {
        var prefix = 0;
        var max = Math.Min(before.Length, after.Length);
        while (prefix < max && before[prefix] == after[prefix])
        {
            prefix++;
        }
        var suffix = 0;
        while (suffix < before.Length - prefix && suffix < after.Length - prefix &&
               before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix])
        {
            suffix++;
        }
        return (prefix, before.Length - prefix - suffix, after.Length - prefix - suffix);
    }
}
