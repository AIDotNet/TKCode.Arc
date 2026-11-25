// Ported from vscode (src/vs/editor/common/model.ts, commit b57d5c9e), MIT license.

namespace TKCode.Arc.Editor.Model;

/// <summary>Identifies one edit operation among those a command produced.</summary>
internal readonly record struct SingleEditOperationIdentifier(int Major, int Minor);

/// <summary>An edit operation whose range has been validated against the model.</summary>
internal sealed class ValidAnnotatedEditOperation
{
    public ValidAnnotatedEditOperation(
        SingleEditOperationIdentifier? identifier,
        TextRange range,
        string? text,
        bool forceMoveMarkers,
        bool isAutoWhitespaceEdit,
        bool isTracked)
    {
        Identifier = identifier;
        Range = range;
        Text = text;
        ForceMoveMarkers = forceMoveMarkers;
        IsAutoWhitespaceEdit = isAutoWhitespaceEdit;
        IsTracked = isTracked;
    }

    public SingleEditOperationIdentifier? Identifier { get; }

    /// <summary>The range to replace; empty for an insert.</summary>
    public TextRange Range { get; }

    /// <summary>The replacement; null or empty for a delete.</summary>
    public string? Text { get; }

    /// <summary>Insert semantics: when <see cref="Range"/> is empty, every marker at the position moves.</summary>
    public bool ForceMoveMarkers { get; }

    /// <summary>The edit inserts automatic whitespace that the next edit may remove again.</summary>
    public bool IsAutoWhitespaceEdit { get; }

    /// <summary>The edit belongs to a tracked set and must not be merged with the others.</summary>
    public bool IsTracked { get; }
}

/// <summary>An edit as it was applied (or its reverse): the range, the text now in it, and the change by offset.</summary>
internal sealed class ValidEditOperation
{
    public ValidEditOperation(SingleEditOperationIdentifier? identifier, TextRange range, string text, TextChange textChange)
    {
        Identifier = identifier;
        Range = range;
        Text = text;
        TextChange = textChange;
    }

    public SingleEditOperationIdentifier? Identifier { get; }

    public TextRange Range { get; }

    public string Text { get; }

    public TextChange TextChange { get; }
}

/// <summary>One change a set of edits made to the buffer, in the coordinates before that change.</summary>
internal readonly record struct InternalModelContentChange(
    TextRange Range,
    int RangeOffset,
    int RangeLength,
    string Text,
    bool ForceMoveMarkers);

/// <summary>What <see cref="PieceTreeTextBuffer.ApplyEdits"/> did.</summary>
internal sealed class ApplyEditsResult
{
    public ApplyEditsResult(
        List<ValidEditOperation>? reverseEdits,
        List<InternalModelContentChange> changes,
        List<int>? trimAutoWhitespaceLineNumbers)
    {
        ReverseEdits = reverseEdits;
        Changes = changes;
        TrimAutoWhitespaceLineNumbers = trimAutoWhitespaceLineNumbers;
    }

    /// <summary>The edits that undo this one; null when they were not asked for.</summary>
    public List<ValidEditOperation>? ReverseEdits { get; }

    /// <summary>The changes, from the bottom of the text to the top.</summary>
    public List<InternalModelContentChange> Changes { get; }

    /// <summary>Lines left holding only automatic whitespace, descending; null when there are none.</summary>
    public List<int>? TrimAutoWhitespaceLineNumbers { get; }
}
