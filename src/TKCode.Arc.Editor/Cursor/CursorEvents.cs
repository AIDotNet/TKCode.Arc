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
/// <param name="Position">The primary cursor's position.</param>
/// <param name="SecondaryPositions">The other cursors' positions.</param>
/// <param name="Reason">Why it changed.</param>
/// <param name="Source">What caused it, such as <c>mouse</c>, <c>keyboard</c> or <c>api</c>.</param>
internal sealed record CursorPositionChangedEvent(
    TextPosition Position,
    IReadOnlyList<TextPosition> SecondaryPositions,
    CursorChangeReason Reason,
    string Source);

/// <summary>The cursor selection changed.</summary>
/// <param name="Selection">The primary selection.</param>
/// <param name="SecondarySelections">The other selections.</param>
/// <param name="ModelVersionId">The model version the selections refer to.</param>
/// <param name="OldSelections">The selections before, if known.</param>
/// <param name="OldModelVersionId">The model version <paramref name="OldSelections"/> refer to.</param>
/// <param name="Source">What caused it, such as <c>mouse</c>, <c>keyboard</c> or <c>api</c>.</param>
/// <param name="Reason">Why it changed.</param>
internal sealed record CursorSelectionChangedEvent(
    TextSelection Selection,
    IReadOnlyList<TextSelection> SecondarySelections,
    int ModelVersionId,
    IReadOnlyList<TextSelection>? OldSelections,
    int OldModelVersionId,
    string Source,
    CursorChangeReason Reason);
