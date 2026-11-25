// Ported from vscode (src/vs/editor/common/cursorEvents.ts, commit b57d5c9e), MIT license.
// The two event interfaces are records.

namespace TKCode.Arc.Editor.Cursor;

/// <summary>Why the cursor changed its position.</summary>
internal enum CursorChangeReason
{
    /// <summary>Unknown or not set.</summary>
    NotSet = 0,

    /// <summary>The model's whole value was replaced (<c>SetValue</c>).</summary>
    ContentFlush = 1,

    /// <summary>The model changed outside this cursor and the cursor recovers its position from its markers.</summary>
    RecoverFromMarkers = 2,

    /// <summary>An explicit user gesture.</summary>
    Explicit = 3,

    /// <summary>A paste.</summary>
    Paste = 4,

    /// <summary>An undo.</summary>
    Undo = 5,

    /// <summary>A redo.</summary>
    Redo = 6,
}

/// <summary>The cursor position changed.</summary>
/// <param name="position">The primary cursor's position.</param>
/// <param name="secondaryPositions">The other cursors' positions.</param>
/// <param name="reason">Why it changed.</param>
/// <param name="source">What caused it, such as <c>mouse</c>, <c>keyboard</c> or <c>api</c>.</param>
internal sealed class CursorPositionChangedEvent(TextPosition position, IReadOnlyList<TextPosition> secondaryPositions, CursorChangeReason reason, string source)
{
    public TextPosition Position { get; } = position;
    public IReadOnlyList<TextPosition> SecondaryPositions { get; } = secondaryPositions;
    public CursorChangeReason Reason { get; } = reason;
    public string Source { get; } = source;
}

/// <summary>The cursor selection changed.</summary>
/// <param name="selection">The primary selection.</param>
/// <param name="secondarySelections">The other selections.</param>
/// <param name="modelVersionId">The model version the selections refer to.</param>
/// <param name="oldSelections">The selections before, if known.</param>
/// <param name="oldModelVersionId">The model version <paramref name="oldSelections"/> refer to.</param>
/// <param name="source">What caused it, such as <c>mouse</c>, <c>keyboard</c> or <c>api</c>.</param>
/// <param name="reason">Why it changed.</param>
internal sealed class CursorSelectionChangedEvent(TextSelection selection, IReadOnlyList<TextSelection> secondarySelections, int modelVersionId, IReadOnlyList<TextSelection>? oldSelections, int oldModelVersionId, string source, CursorChangeReason reason)
{
    public TextSelection Selection { get; } = selection;
    public IReadOnlyList<TextSelection> SecondarySelections { get; } = secondarySelections;
    public int ModelVersionId { get; } = modelVersionId;
    public IReadOnlyList<TextSelection>? OldSelections { get; } = oldSelections;
    public int OldModelVersionId { get; } = oldModelVersionId;
    public string Source { get; } = source;
    public CursorChangeReason Reason { get; } = reason;
}
