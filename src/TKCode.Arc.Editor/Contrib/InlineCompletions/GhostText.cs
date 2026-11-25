// Ported from vscode (src/vs/editor/contrib/inlineCompletions/browser/model/ghostText.ts, commit b57d5c9e), MIT
// license. Differences: GhostTextReplacement (the inline edits' replacement preview) and the parts' line decorations
// (syntax colors of the suggested text, from the tokenizer) are not ported: ghost text is drawn in one muted style.

namespace TKCode.Arc.Editor.Contrib.InlineSuggestions;

/// <summary>Text shown on a line that the document does not contain: one part per insertion point.</summary>
internal sealed class GhostText : IEquatable<GhostText>
{
    public GhostText(int lineNumber, IReadOnlyList<GhostTextPart> parts)
    {
        LineNumber = lineNumber;
        Parts = parts;
    }

    public int LineNumber { get; }

    public IReadOnlyList<GhostTextPart> Parts { get; }

    public bool Equals(GhostText? other) =>
        other is not null
        && LineNumber == other.LineNumber
        && Parts.Count == other.Parts.Count
        && Parts.Select((part, index) => part.Equals(other.Parts[index])).All(static equal => equal);

    public override bool Equals(object? obj) => obj is GhostText other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(LineNumber, Parts.Count);

    /// <summary>Only used for testing/debugging.</summary>
    public string Render(string documentText, bool debug = false) =>
        TextReplacement.ApplyToString(documentText, Parts.Select(p => new TextReplacement(
            new TextRange(LineNumber, p.Column, LineNumber, p.Column),
            debug ? $"[{string.Join('\n', p.Lines)}]" : string.Join('\n', p.Lines))));

    public string RenderForScreenReader(string lineText)
    {
        if (Parts.Count == 0)
        {
            return "";
        }

        var lastPart = Parts[^1];
        var cappedLineText = lineText[..Math.Min(lineText.Length, lastPart.Column - 1)];
        var text = TextReplacement.ApplyToString(cappedLineText, Parts.Select(p => new TextReplacement(
            new TextRange(1, p.Column, 1, p.Column),
            string.Join('\n', p.Lines))));
        return text[(Parts[0].Column - 1)..];
    }

    public bool IsEmpty() => Parts.All(static p => p.Lines.Count == 0);

    /// <summary>The lines the ghost text takes: its own line and those its parts add.</summary>
    public int LineCount => 1 + Parts.Sum(static p => p.Lines.Count - 1);
}

/// <summary>Text inserted at one column of a ghost text's line.</summary>
internal sealed class GhostTextPart : IEquatable<GhostTextPart>
{
    /// <param name="column">The column the text is inserted at.</param>
    /// <param name="text">The text, possibly several lines.</param>
    /// <param name="preview">Indicates if this part is a preview of an inline suggestion when a suggestion is previewed.</param>
    public GhostTextPart(int column, string text, bool preview)
    {
        Column = column;
        Text = text;
        Preview = preview;
        Lines = TextHelpers.SplitLines(text);
    }

    public int Column { get; }

    public string Text { get; }

    public bool Preview { get; }

    public IReadOnlyList<string> Lines { get; }

    public bool Equals(GhostTextPart? other) =>
        other is not null
        && Column == other.Column
        && Lines.Count == other.Lines.Count
        && Lines.SequenceEqual(other.Lines, StringComparer.Ordinal);

    public override bool Equals(object? obj) => obj is GhostTextPart other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Column, Lines.Count);
}
