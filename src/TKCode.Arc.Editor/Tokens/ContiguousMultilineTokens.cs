// Ported from vscode (src/vs/editor/common/tokens/contiguousMultilineTokens.ts, commit b57d5c9e), MIT license.
// Differences from upstream: a line's tokens are a uint[] or null (see ContiguousTokensEditing.cs); serialize and
// deserialize (which move tokens between a web worker and the renderer) are left out, as nothing crosses a process here.

namespace TKCode.Arc.Editor.Tokens;

/// <summary>Represents contiguous tokens over a contiguous range of lines.</summary>
internal sealed class ContiguousMultilineTokens
{
    /// <summary>The start line number for this block of tokens.</summary>
    private int _startLineNumber;

    /// <summary>
    /// The tokens are stored in a binary format. There is an element for each line, so <c>_tokens[index]</c> contains all
    /// tokens on line <c>startLineNumber + index</c>. On a specific line, each token occupies two array indices. For token
    /// i: at offset 2*i its end offset, at offset 2*i + 1 its metadata.
    /// </summary>
    private List<uint[]?> _tokens;

    public ContiguousMultilineTokens(int startLineNumber, List<uint[]?> tokens)
    {
        _startLineNumber = startLineNumber;
        _tokens = tokens;
    }

    /// <summary>(Inclusive) start line number for these tokens.</summary>
    public int StartLineNumber => _startLineNumber;

    /// <summary>(Inclusive) end line number for these tokens.</summary>
    public int EndLineNumber => _startLineNumber + _tokens.Count - 1;

    public LineRange GetLineRange() => new(_startLineNumber, _startLineNumber + _tokens.Count);

    /// <summary>The tokens of a line of this block (see <see cref="_tokens"/>).</summary>
    public uint[]? GetLineTokens(int lineNumber) => _tokens[lineNumber - _startLineNumber];

    public void AppendLineTokens(uint[] lineTokens) => _tokens.Add(lineTokens);

    public void ApplyEdit(TextRange range, string text)
    {
        var eol = EolCounter.CountEOL(text);
        AcceptDeleteRange(range);
        AcceptInsertText(new TextPosition(range.StartLineNumber, range.StartColumn), eol.Count, eol.FirstLineLength);
    }

    private void AcceptDeleteRange(TextRange range)
    {
        if (range.StartLineNumber == range.EndLineNumber && range.StartColumn == range.EndColumn)
        {
            // Nothing to delete
            return;
        }

        var firstLineIndex = range.StartLineNumber - _startLineNumber;
        var lastLineIndex = range.EndLineNumber - _startLineNumber;

        if (lastLineIndex < 0)
        {
            // this deletion occurs entirely before this block, so we only need to adjust line numbers
            var deletedLinesCount = lastLineIndex - firstLineIndex;
            _startLineNumber -= deletedLinesCount;
            return;
        }

        if (firstLineIndex >= _tokens.Count)
        {
            // this deletion occurs entirely after this block, so there is nothing to do
            return;
        }

        if (firstLineIndex < 0 && lastLineIndex >= _tokens.Count)
        {
            // this deletion completely encompasses this block
            _startLineNumber = 0;
            _tokens = [];
            return;
        }

        if (firstLineIndex == lastLineIndex)
        {
            // a delete on a single line
            _tokens[firstLineIndex] = ContiguousTokensEditing.Delete(_tokens[firstLineIndex], range.StartColumn - 1, range.EndColumn - 1);
            return;
        }

        if (firstLineIndex >= 0)
        {
            // The first line survives
            _tokens[firstLineIndex] = ContiguousTokensEditing.DeleteEnding(_tokens[firstLineIndex], range.StartColumn - 1);

            if (lastLineIndex < _tokens.Count)
            {
                // The last line survives
                var lastLineTokens = ContiguousTokensEditing.DeleteBeginning(_tokens[lastLineIndex], range.EndColumn - 1);

                // Take remaining text on last line and append it to remaining text on first line
                _tokens[firstLineIndex] = ContiguousTokensEditing.Append(_tokens[firstLineIndex], lastLineTokens);

                // Delete middle lines
                _tokens.RemoveRange(firstLineIndex + 1, lastLineIndex - firstLineIndex);
            }
            else
            {
                // The last line does not survive

                // Take remaining text on last line and append it to remaining text on first line
                _tokens[firstLineIndex] = ContiguousTokensEditing.Append(_tokens[firstLineIndex], null);

                // Delete lines
                _tokens = _tokens.GetRange(0, firstLineIndex + 1);
            }
        }
        else
        {
            // The first line does not survive

            var deletedBefore = -firstLineIndex;
            _startLineNumber -= deletedBefore;

            // Remove beginning from last line
            _tokens[lastLineIndex] = ContiguousTokensEditing.DeleteBeginning(_tokens[lastLineIndex], range.EndColumn - 1);

            // Delete lines
            _tokens = _tokens.GetRange(lastLineIndex, _tokens.Count - lastLineIndex);
        }
    }

    private void AcceptInsertText(TextPosition position, int eolCount, int firstLineLength)
    {
        if (eolCount == 0 && firstLineLength == 0)
        {
            // Nothing to insert
            return;
        }

        var lineIndex = position.LineNumber - _startLineNumber;

        if (lineIndex < 0)
        {
            // this insertion occurs before this block, so we only need to adjust line numbers
            _startLineNumber += eolCount;
            return;
        }

        if (lineIndex >= _tokens.Count)
        {
            // this insertion occurs after this block, so there is nothing to do
            return;
        }

        if (eolCount == 0)
        {
            // Inserting text on one line
            _tokens[lineIndex] = ContiguousTokensEditing.Insert(_tokens[lineIndex], position.Column - 1, firstLineLength);
            return;
        }

        _tokens[lineIndex] = ContiguousTokensEditing.DeleteEnding(_tokens[lineIndex], position.Column - 1);
        _tokens[lineIndex] = ContiguousTokensEditing.Insert(_tokens[lineIndex], position.Column - 1, firstLineLength);

        InsertLines(position.LineNumber, eolCount);
    }

    private void InsertLines(int insertIndex, int insertCount)
    {
        if (insertCount == 0)
        {
            return;
        }

        var lineTokens = new uint[]?[insertCount];
        // Upstream's arrayInsert: past the end it appends.
        _tokens.InsertRange(Math.Min(insertIndex, _tokens.Count), lineTokens);
    }
}
