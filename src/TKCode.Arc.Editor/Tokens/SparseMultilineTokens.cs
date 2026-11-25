// Ported from vscode (src/vs/editor/common/tokens/sparseMultilineTokens.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the token arrays are uint[]; a SparseLineTokens is a view of a part of its storage's array (upstream's subarray);
//  - reportIfInvalid is left out: upstream only logs to the console there, through a rate limiter that is module state;
//  - the storage's split collects its two halves in Lists.

namespace TKCode.Arc.Editor.Tokens;

/// <summary>Represents sparse tokens over a contiguous range of lines.</summary>
internal sealed class SparseMultilineTokens
{
    private readonly SparseMultilineTokensStorage _tokens;
    private int _startLineNumber;
    private int _endLineNumber;

    private SparseMultilineTokens(int startLineNumber, SparseMultilineTokensStorage tokens)
    {
        _startLineNumber = startLineNumber;
        _tokens = tokens;
        _endLineNumber = _startLineNumber + _tokens.GetMaxDeltaLine();
    }

    /// <summary>(Inclusive) start line number for these tokens.</summary>
    public int StartLineNumber => _startLineNumber;

    /// <summary>(Inclusive) end line number for these tokens.</summary>
    public int EndLineNumber => _endLineNumber;

    /// <summary>
    /// Tokens as quadruples: delta line (from <paramref name="startLineNumber"/>), start character, end character,
    /// metadata.
    /// </summary>
    public static SparseMultilineTokens Create(int startLineNumber, uint[] tokens) =>
        new(startLineNumber, new SparseMultilineTokensStorage(tokens));

    public override string ToString() => _tokens.ToString(_startLineNumber);

    private void UpdateEndLineNumber() => _endLineNumber = _startLineNumber + _tokens.GetMaxDeltaLine();

    public bool IsEmpty() => _tokens.IsEmpty();

    public SparseLineTokens? GetLineTokens(int lineNumber)
    {
        if (_startLineNumber <= lineNumber && lineNumber <= _endLineNumber)
        {
            return _tokens.GetLineTokens(lineNumber - _startLineNumber);
        }

        return null;
    }

    public TextRange? GetRange()
    {
        var deltaRange = _tokens.GetRange();
        if (deltaRange is not { } range)
        {
            return null;
        }

        return new TextRange(_startLineNumber + range.StartLineNumber, range.StartColumn, _startLineNumber + range.EndLineNumber, range.EndColumn);
    }

    public void RemoveTokens(TextRange range)
    {
        var startLineIndex = range.StartLineNumber - _startLineNumber;
        var endLineIndex = range.EndLineNumber - _startLineNumber;

        _startLineNumber += _tokens.RemoveTokens(startLineIndex, range.StartColumn - 1, endLineIndex, range.EndColumn - 1);
        UpdateEndLineNumber();
    }

    public (SparseMultilineTokens A, SparseMultilineTokens B) Split(TextRange range)
    {
        // split tokens to two:
        // a) all the tokens before `range`
        // b) all the tokens after `range`
        var startLineIndex = range.StartLineNumber - _startLineNumber;
        var endLineIndex = range.EndLineNumber - _startLineNumber;

        var (a, b, bDeltaLine) = _tokens.Split(startLineIndex, range.StartColumn - 1, endLineIndex, range.EndColumn - 1);
        return (new SparseMultilineTokens(_startLineNumber, a), new SparseMultilineTokens(_startLineNumber + bDeltaLine, b));
    }

    public void ApplyEdit(TextRange range, string text)
    {
        var eol = EolCounter.CountEOL(text);
        AcceptEdit(range, eol.Count, eol.FirstLineLength, eol.LastLineLength, text.Length > 0 ? text[0] : '\0');
    }

    public void AcceptEdit(TextRange range, int eolCount, int firstLineLength, int lastLineLength, int firstCharCode)
    {
        AcceptDeleteRange(range);
        AcceptInsertText(new TextPosition(range.StartLineNumber, range.StartColumn), eolCount, firstLineLength, lastLineLength, firstCharCode);
        UpdateEndLineNumber();
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

        var tokenMaxDeltaLine = _tokens.GetMaxDeltaLine();

        if (firstLineIndex >= tokenMaxDeltaLine + 1)
        {
            // this deletion occurs entirely after this block, so there is nothing to do
            return;
        }

        if (firstLineIndex < 0 && lastLineIndex >= tokenMaxDeltaLine + 1)
        {
            // this deletion completely encompasses this block
            _startLineNumber = 0;
            _tokens.Clear();
            return;
        }

        if (firstLineIndex < 0)
        {
            var deletedBefore = -firstLineIndex;
            _startLineNumber -= deletedBefore;

            _tokens.AcceptDeleteRange(range.StartColumn - 1, 0, 0, lastLineIndex, range.EndColumn - 1);
        }
        else
        {
            _tokens.AcceptDeleteRange(0, firstLineIndex, range.StartColumn - 1, lastLineIndex, range.EndColumn - 1);
        }
    }

    private void AcceptInsertText(TextPosition position, int eolCount, int firstLineLength, int lastLineLength, int firstCharCode)
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

        var tokenMaxDeltaLine = _tokens.GetMaxDeltaLine();

        if (lineIndex >= tokenMaxDeltaLine + 1)
        {
            // this insertion occurs after this block, so there is nothing to do
            return;
        }

        _tokens.AcceptInsertText(lineIndex, position.Column - 1, eolCount, firstLineLength, lastLineLength, firstCharCode);
    }
}

/// <summary>The tokens of a <see cref="SparseMultilineTokens"/>, as quadruples in one array.</summary>
internal sealed class SparseMultilineTokensStorage
{
    /// <summary>
    /// The encoding of tokens is: 4*i deltaLine (from <c>startLineNumber</c>), 4*i+1 startCharacter (from the line start),
    /// 4*i+2 endCharacter (from the line start), 4*i+3 metadata.
    /// </summary>
    private readonly uint[] _tokens;
    private int _tokenCount;

    public SparseMultilineTokensStorage(uint[] tokens)
    {
        _tokens = tokens;
        _tokenCount = tokens.Length / 4;
    }

    public string ToString(int startLineNumber)
    {
        var pieces = new List<string>();
        for (var i = 0; i < _tokenCount; i++)
        {
            pieces.Add($"({GetDeltaLine(i) + startLineNumber},{GetStartCharacter(i)}-{GetEndCharacter(i)})");
        }

        return "[" + string.Join(",", pieces) + "]";
    }

    public int GetMaxDeltaLine()
    {
        var tokenCount = _tokenCount;
        if (tokenCount == 0)
        {
            return -1;
        }

        return GetDeltaLine(tokenCount - 1);
    }

    /// <summary>The range the tokens cover, its lines relative to the start line (as upstream, line 0 is allowed here).</summary>
    public (int StartLineNumber, int StartColumn, int EndLineNumber, int EndColumn)? GetRange()
    {
        var tokenCount = _tokenCount;
        if (tokenCount == 0)
        {
            return null;
        }

        var startChar = GetStartCharacter(0);
        var maxDeltaLine = GetDeltaLine(tokenCount - 1);
        var endChar = GetEndCharacter(tokenCount - 1);
        return (0, startChar + 1, maxDeltaLine, endChar + 1);
    }

    private int GetDeltaLine(int tokenIndex) => (int)_tokens[4 * tokenIndex];

    private int GetStartCharacter(int tokenIndex) => (int)_tokens[(4 * tokenIndex) + 1];

    private int GetEndCharacter(int tokenIndex) => (int)_tokens[(4 * tokenIndex) + 2];

    public bool IsEmpty() => _tokenCount == 0;

    public SparseLineTokens? GetLineTokens(int deltaLine)
    {
        var low = 0;
        var high = _tokenCount - 1;

        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            var midDeltaLine = GetDeltaLine(mid);

            if (midDeltaLine < deltaLine)
            {
                low = mid + 1;
            }
            else if (midDeltaLine > deltaLine)
            {
                high = mid - 1;
            }
            else
            {
                var min = mid;
                while (min > low && GetDeltaLine(min - 1) == deltaLine)
                {
                    min--;
                }

                var max = mid;
                while (max < high && GetDeltaLine(max + 1) == deltaLine)
                {
                    max++;
                }

                return new SparseLineTokens(_tokens, 4 * min, (4 * max) + 4);
            }
        }

        if (low < _tokenCount && GetDeltaLine(low) == deltaLine)
        {
            return new SparseLineTokens(_tokens, 4 * low, (4 * low) + 4);
        }

        return null;
    }

    public void Clear() => _tokenCount = 0;

    public int RemoveTokens(int startDeltaLine, int startChar, int endDeltaLine, int endChar)
    {
        var tokens = _tokens;
        var tokenCount = _tokenCount;
        var newTokenCount = 0;
        var hasDeletedTokens = false;
        var firstDeltaLine = 0;
        for (var i = 0; i < tokenCount; i++)
        {
            var srcOffset = 4 * i;
            var tokenDeltaLine = (int)tokens[srcOffset];
            var tokenStartCharacter = (int)tokens[srcOffset + 1];
            var tokenEndCharacter = (int)tokens[srcOffset + 2];
            var tokenMetadata = tokens[srcOffset + 3];

            if ((tokenDeltaLine > startDeltaLine || (tokenDeltaLine == startDeltaLine && tokenEndCharacter >= startChar))
                && (tokenDeltaLine < endDeltaLine || (tokenDeltaLine == endDeltaLine && tokenStartCharacter <= endChar)))
            {
                hasDeletedTokens = true;
            }
            else
            {
                if (newTokenCount == 0)
                {
                    firstDeltaLine = tokenDeltaLine;
                }

                if (hasDeletedTokens)
                {
                    // must move the token to the left
                    var destOffset = 4 * newTokenCount;
                    tokens[destOffset] = (uint)(tokenDeltaLine - firstDeltaLine);
                    tokens[destOffset + 1] = (uint)tokenStartCharacter;
                    tokens[destOffset + 2] = (uint)tokenEndCharacter;
                    tokens[destOffset + 3] = tokenMetadata;
                }
                else if (firstDeltaLine != 0)
                {
                    // must adjust the delta line in place
                    tokens[srcOffset] = (uint)(tokenDeltaLine - firstDeltaLine);
                }

                newTokenCount++;
            }
        }

        _tokenCount = newTokenCount;

        return firstDeltaLine;
    }

    public (SparseMultilineTokensStorage A, SparseMultilineTokensStorage B, int BDeltaLine) Split(int startDeltaLine, int startChar, int endDeltaLine, int endChar)
    {
        var tokens = _tokens;
        var tokenCount = _tokenCount;
        var aTokens = new List<uint>();
        var bTokens = new List<uint>();
        var destTokens = aTokens;
        var destFirstDeltaLine = 0;
        for (var i = 0; i < tokenCount; i++)
        {
            var srcOffset = 4 * i;
            var tokenDeltaLine = (int)tokens[srcOffset];
            var tokenStartCharacter = tokens[srcOffset + 1];
            var tokenEndCharacter = (int)tokens[srcOffset + 2];
            var tokenMetadata = tokens[srcOffset + 3];

            if (tokenDeltaLine > startDeltaLine || (tokenDeltaLine == startDeltaLine && tokenEndCharacter >= startChar))
            {
                if (tokenDeltaLine < endDeltaLine || (tokenDeltaLine == endDeltaLine && tokenStartCharacter <= endChar))
                {
                    // this token is touching the range
                    continue;
                }

                // this token is after the range
                if (!ReferenceEquals(destTokens, bTokens))
                {
                    // this token is the first token after the range
                    destTokens = bTokens;
                    destFirstDeltaLine = tokenDeltaLine;
                }
            }

            destTokens.Add((uint)(tokenDeltaLine - destFirstDeltaLine));
            destTokens.Add(tokenStartCharacter);
            destTokens.Add((uint)tokenEndCharacter);
            destTokens.Add(tokenMetadata);
        }

        return (new SparseMultilineTokensStorage(aTokens.ToArray()), new SparseMultilineTokensStorage(bTokens.ToArray()), destFirstDeltaLine);
    }

    public void AcceptDeleteRange(int horizontalShiftForFirstLineTokens, int startDeltaLine, int startCharacter, int endDeltaLine, int endCharacter)
    {
        // This is a bit complex, here are the cases I used to think about this:
        //
        // 1. The token starts before the deletion range
        // 1a. The token is completely before the deletion range
        //               -----------
        //                          xxxxxxxxxxx
        // 1b. The token starts before, the deletion range ends after the token
        //               -----------
        //                      xxxxxxxxxxx
        // 1c. The token starts before, the deletion range ends precisely with the token
        //               ---------------
        //                      xxxxxxxx
        // 1d. The token starts before, the deletion range is inside the token
        //               ---------------
        //                    xxxxx
        //
        // 2. The token starts at the same position with the deletion range
        // 2a. The token starts at the same position, and ends inside the deletion range
        //               -------
        //               xxxxxxxxxxx
        // 2b. The token starts at the same position, and ends at the same position as the deletion range
        //               ----------
        //               xxxxxxxxxx
        // 2c. The token starts at the same position, and ends after the deletion range
        //               -------------
        //               xxxxxxx
        //
        // 3. The token starts inside the deletion range
        // 3a. The token is inside the deletion range
        //                -------
        //             xxxxxxxxxxxxx
        // 3b. The token starts inside the deletion range, and ends at the same position as the deletion range
        //                ----------
        //             xxxxxxxxxxxxx
        // 3c. The token starts inside the deletion range, and ends after the deletion range
        //                ------------
        //             xxxxxxxxxxx
        //
        // 4. The token starts after the deletion range
        //                  -----------
        //          xxxxxxxx
        //
        var tokens = _tokens;
        var tokenCount = _tokenCount;
        var deletedLineCount = endDeltaLine - startDeltaLine;
        var newTokenCount = 0;
        var hasDeletedTokens = false;
        for (var i = 0; i < tokenCount; i++)
        {
            var srcOffset = 4 * i;
            var tokenDeltaLine = (int)tokens[srcOffset];
            var tokenStartCharacter = (int)tokens[srcOffset + 1];
            var tokenEndCharacter = (int)tokens[srcOffset + 2];
            var tokenMetadata = tokens[srcOffset + 3];

            if (tokenDeltaLine < startDeltaLine || (tokenDeltaLine == startDeltaLine && tokenEndCharacter <= startCharacter))
            {
                // 1a. The token is completely before the deletion range
                // => nothing to do
                newTokenCount++;
                continue;
            }
            else if (tokenDeltaLine == startDeltaLine && tokenStartCharacter < startCharacter)
            {
                // 1b, 1c, 1d
                // => the token survives, but it needs to shrink
                if (tokenDeltaLine == endDeltaLine && tokenEndCharacter > endCharacter)
                {
                    // 1d. The token starts before, the deletion range is inside the token
                    // => the token shrinks by the deletion character count
                    tokenEndCharacter -= endCharacter - startCharacter;
                }
                else
                {
                    // 1b. The token starts before, the deletion range ends after the token
                    // 1c. The token starts before, the deletion range ends precisely with the token
                    // => the token shrinks its ending to the deletion start
                    tokenEndCharacter = startCharacter;
                }
            }
            else if (tokenDeltaLine == startDeltaLine && tokenStartCharacter == startCharacter)
            {
                // 2a, 2b, 2c
                if (tokenDeltaLine == endDeltaLine && tokenEndCharacter > endCharacter)
                {
                    // 2c. The token starts at the same position, and ends after the deletion range
                    // => the token shrinks by the deletion character count
                    tokenEndCharacter -= endCharacter - startCharacter;
                }
                else
                {
                    // 2a. The token starts at the same position, and ends inside the deletion range
                    // 2b. The token starts at the same position, and ends at the same position as the deletion range
                    // => the token is deleted
                    hasDeletedTokens = true;
                    continue;
                }
            }
            else if (tokenDeltaLine < endDeltaLine || (tokenDeltaLine == endDeltaLine && tokenStartCharacter < endCharacter))
            {
                // 3a, 3b, 3c
                if (tokenDeltaLine == endDeltaLine && tokenEndCharacter > endCharacter)
                {
                    // 3c. The token starts inside the deletion range, and ends after the deletion range
                    // => the token moves to continue right after the deletion
                    tokenDeltaLine = startDeltaLine;
                    tokenStartCharacter = startCharacter;
                    tokenEndCharacter = tokenStartCharacter + (tokenEndCharacter - endCharacter);
                }
                else
                {
                    // 3a. The token is inside the deletion range
                    // 3b. The token starts inside the deletion range, and ends at the same position as the deletion range
                    // => the token is deleted
                    hasDeletedTokens = true;
                    continue;
                }
            }
            else if (tokenDeltaLine > endDeltaLine)
            {
                // 4. (partial) The token starts after the deletion range, on a line below...
                if (deletedLineCount == 0 && !hasDeletedTokens)
                {
                    // early stop, there is no need to walk all the tokens and do nothing...
                    newTokenCount = tokenCount;
                    break;
                }

                tokenDeltaLine -= deletedLineCount;
            }
            else if (tokenDeltaLine == endDeltaLine && tokenStartCharacter >= endCharacter)
            {
                // 4. (continued) The token starts after the deletion range, on the last line where a deletion occurs
                if (horizontalShiftForFirstLineTokens != 0 && tokenDeltaLine == 0)
                {
                    tokenStartCharacter += horizontalShiftForFirstLineTokens;
                    tokenEndCharacter += horizontalShiftForFirstLineTokens;
                }

                tokenDeltaLine -= deletedLineCount;
                tokenStartCharacter -= endCharacter - startCharacter;
                tokenEndCharacter -= endCharacter - startCharacter;
            }
            else
            {
                throw new InvalidOperationException("Not possible!");
            }

            var destOffset = 4 * newTokenCount;
            tokens[destOffset] = (uint)tokenDeltaLine;
            tokens[destOffset + 1] = (uint)tokenStartCharacter;
            tokens[destOffset + 2] = (uint)tokenEndCharacter;
            tokens[destOffset + 3] = tokenMetadata;
            newTokenCount++;
        }

        _tokenCount = newTokenCount;
    }

    public void AcceptInsertText(int deltaLine, int character, int eolCount, int firstLineLength, int lastLineLength, int firstCharCode)
    {
        // Here are the cases I used to think about this:
        //
        // 1. The token is completely before the insertion point
        //            -----------   |
        // 2. The token ends precisely at the insertion point
        //            -----------|
        // 3. The token contains the insertion point
        //            -----|------
        // 4. The token starts precisely at the insertion point
        //            |-----------
        // 5. The token is completely after the insertion point
        //            |   -----------
        //
        var isInsertingPreciselyOneWordCharacter =
            eolCount == 0
            && firstLineLength == 1
            && (firstCharCode is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z');
        var tokens = _tokens;
        var tokenCount = _tokenCount;
        for (var i = 0; i < tokenCount; i++)
        {
            var offset = 4 * i;
            var tokenDeltaLine = (int)tokens[offset];
            var tokenStartCharacter = (int)tokens[offset + 1];
            var tokenEndCharacter = (int)tokens[offset + 2];

            if (tokenDeltaLine < deltaLine || (tokenDeltaLine == deltaLine && tokenEndCharacter < character))
            {
                // 1. The token is completely before the insertion point
                // => nothing to do
                continue;
            }
            else if (tokenDeltaLine == deltaLine && tokenEndCharacter == character)
            {
                // 2. The token ends precisely at the insertion point
                // => expand the end character only if inserting precisely one character that is a word character
                if (isInsertingPreciselyOneWordCharacter)
                {
                    tokenEndCharacter += 1;
                }
                else
                {
                    continue;
                }
            }
            else if (tokenDeltaLine == deltaLine && tokenStartCharacter < character && character < tokenEndCharacter)
            {
                // 3. The token contains the insertion point
                if (eolCount == 0)
                {
                    // => just expand the end character
                    tokenEndCharacter += firstLineLength;
                }
                else
                {
                    // => cut off the token
                    tokenEndCharacter = character;
                }
            }
            else
            {
                // 4. or 5.
                if (tokenDeltaLine == deltaLine && tokenStartCharacter == character)
                {
                    // 4. The token starts precisely at the insertion point
                    // => grow the token (by keeping its start constant) only if inserting precisely one character that is a word character
                    // => otherwise behave as in case 5.
                    if (isInsertingPreciselyOneWordCharacter)
                    {
                        continue;
                    }
                }

                // => the token must move and keep its size constant
                if (tokenDeltaLine == deltaLine)
                {
                    tokenDeltaLine += eolCount;
                    // this token is on the line where the insertion is taking place
                    if (eolCount == 0)
                    {
                        tokenStartCharacter += firstLineLength;
                        tokenEndCharacter += firstLineLength;
                    }
                    else
                    {
                        var tokenLength = tokenEndCharacter - tokenStartCharacter;
                        tokenStartCharacter = lastLineLength + (tokenStartCharacter - character);
                        tokenEndCharacter = tokenStartCharacter + tokenLength;
                    }
                }
                else
                {
                    tokenDeltaLine += eolCount;
                }
            }

            tokens[offset] = (uint)tokenDeltaLine;
            tokens[offset + 1] = (uint)tokenStartCharacter;
            tokens[offset + 2] = (uint)tokenEndCharacter;
        }
    }
}

/// <summary>The sparse tokens of one line: a view of quadruples in a storage's array.</summary>
internal readonly struct SparseLineTokens
{
    private readonly uint[] _tokens;
    private readonly int _start;
    private readonly int _end;

    public SparseLineTokens(uint[] tokens, int start, int end)
    {
        _tokens = tokens;
        _start = start;
        _end = end;
    }

    public int GetCount() => (_end - _start) / 4;

    public int GetStartCharacter(int tokenIndex) => (int)_tokens[_start + (4 * tokenIndex) + 1];

    public int GetEndCharacter(int tokenIndex) => (int)_tokens[_start + (4 * tokenIndex) + 2];

    public uint GetMetadata(int tokenIndex) => _tokens[_start + (4 * tokenIndex) + 3];
}
