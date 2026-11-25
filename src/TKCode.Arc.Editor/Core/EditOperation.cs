// Ported from vscode (src/vs/editor/common/core/editOperation.ts and the IIdentifiedSingleEditOperation of
// src/vs/editor/common/model.ts, commit b57d5c9e), MIT license.
//
// Upstream has two interfaces (ISingleEditOperation and IIdentifiedSingleEditOperation, which adds the identifier and
// two flags). Here they are one record: the extra members are optional and default to what upstream's absent
// properties mean.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor;

/// <summary>A single edit: replace the text in <see cref="Range"/> with <see cref="Text"/>.</summary>
/// <param name="Range">The range to replace; empty for an insert.</param>
/// <param name="Text">The text to put there; null or empty for a delete.</param>
/// <param name="ForceMoveMarkers">
/// Insert semantics: when <paramref name="Range"/> is empty, every marker (decoration, cursor) at the position moves
/// after the inserted text.
/// </param>
internal sealed record SingleEditOperation(TextRange Range, string? Text, bool ForceMoveMarkers = false)
{
    /// <summary>Identifies the operation among those one command produced.</summary>
    public SingleEditOperationIdentifier? Identifier { get; init; }

    /// <summary>The edit inserts automatic whitespace that the next edit may remove again.</summary>
    public bool IsAutoWhitespaceEdit { get; init; }

    /// <summary>The edit belongs to a tracked set and must not be merged with the others.</summary>
    public bool IsTracked { get; init; }
}

/// <summary>Builds the usual edit operations (Monaco's <c>EditOperation</c>).</summary>
internal static class EditOperation
{
    /// <summary>Inserts <paramref name="text"/> at <paramref name="position"/>; markers there move after it.</summary>
    public static SingleEditOperation Insert(TextPosition position, string text) =>
        new(new TextRange(position.LineNumber, position.Column, position.LineNumber, position.Column), text, true);

    /// <summary>Deletes the text in <paramref name="range"/>.</summary>
    public static SingleEditOperation Delete(TextRange range) => new(range, null);

    /// <summary>Replaces the text in <paramref name="range"/>.</summary>
    public static SingleEditOperation Replace(TextRange range, string? text) => new(range, text);

    /// <summary>Replaces the text in <paramref name="range"/>; markers at an empty range move after the text.</summary>
    public static SingleEditOperation ReplaceMove(TextRange range, string? text) => new(range, text, true);
}
