// Ported from vscode (src/vs/editor/common/model/pieceTreeTextBuffer/pieceTreeTextBuffer.ts, commit b57d5c9e), MIT license.

using System.Text;

namespace TKCode.Arc.Editor.Model;

/// <summary>An edit operation after validation against the buffer, with what the buffer needs to apply it.</summary>
internal sealed class ValidatedEditOperation
{
    public int SortIndex;
    public SingleEditOperationIdentifier? Identifier;
    public TextRange Range;
    public int RangeOffset;
    public int RangeLength;
    public string Text = string.Empty;
    public int EolCount;
    public int FirstLineLength;
    public int LastLineLength;
    public bool ForceMoveMarkers;
    public bool IsAutoWhitespaceEdit;
}

/// <summary>
/// The text buffer of a model: a <see cref="PieceTreeBase"/> plus the BOM, the facts about the content that the
/// editor asks for (right-to-left text, unusual line terminators, non-ASCII) and edits that produce their own undo.
/// </summary>
internal sealed class PieceTreeTextBuffer
{
    private readonly PieceTreeBase _pieceTree;
    private readonly string _bom;
    private bool _mightContainRTL;
    private bool _mightContainUnusualLineTerminators;
    private bool _mightContainNonBasicASCII;

    public PieceTreeTextBuffer(
        PieceTreeBase pieceTree,
        string bom,
        bool containsRTL,
        bool containsUnusualLineTerminators,
        bool isBasicASCII)
    {
        _pieceTree = pieceTree;
        _bom = bom;
        _mightContainNonBasicASCII = !isBasicASCII;
        _mightContainRTL = containsRTL;
        _mightContainUnusualLineTerminators = containsUnusualLineTerminators;
    }

    /// <summary>Creates a buffer from what the builder's factory produced.</summary>
    public PieceTreeTextBuffer(PieceTreeBufferResult result)
        : this(result.PieceTree, result.Bom, result.ContainsRtl, result.ContainsUnusualLineTerminators, result.IsBasicAscii)
    {
    }

    /// <summary>Creates a buffer holding <paramref name="text"/>.</summary>
    public static PieceTreeTextBuffer Create(string text, DefaultEndOfLine defaultEol = DefaultEndOfLine.LF)
    {
        var builder = new PieceTreeTextBufferBuilder();
        builder.AcceptChunk(text);
        return new PieceTreeTextBuffer(builder.Finish().Create(defaultEol));
    }

    /// <summary>Raised after <see cref="ApplyEdits"/> changed the content.</summary>
    public event Action? DidChangeContent;

    // #region TextBuffer

    public bool Equals(PieceTreeTextBuffer other)
    {
        if (_bom != other._bom)
        {
            return false;
        }
        if (GetEOL() != other.GetEOL())
        {
            return false;
        }
        return _pieceTree.Equal(other._pieceTree);
    }

    public bool MightContainRTL() => _mightContainRTL;

    public bool MightContainUnusualLineTerminators() => _mightContainUnusualLineTerminators;

    public void ResetMightContainUnusualLineTerminators() => _mightContainUnusualLineTerminators = false;

    public bool MightContainNonBasicASCII() => _mightContainNonBasicASCII;

    public string GetBOM() => _bom;

    public string GetEOL() => _pieceTree.GetEol();

    public ITextSnapshot CreateSnapshot(bool preserveBOM) => _pieceTree.CreateSnapshot(preserveBOM ? _bom : string.Empty);

    /// <summary>A snapshot of the text that other threads may read by line (see <see cref="TextSnapshot"/>).</summary>
    public TextSnapshot CreateTextSnapshot(int versionId = 0) => _pieceTree.CreateTextSnapshot(versionId);

    public int GetOffsetAt(int lineNumber, int column) => _pieceTree.GetOffsetAt(lineNumber, column);

    public TextPosition GetPositionAt(int offset) => _pieceTree.GetPositionAt(offset);

    public TextRange GetRangeAt(int start, int length)
    {
        var end = start + length;
        var startPosition = GetPositionAt(start);
        var endPosition = GetPositionAt(end);
        return new TextRange(startPosition.LineNumber, startPosition.Column, endPosition.LineNumber, endPosition.Column);
    }

    public string GetValueInRange(TextRange range, EndOfLinePreference eol = EndOfLinePreference.TextDefined)
    {
        if (range.IsEmpty)
        {
            return string.Empty;
        }

        var lineEnding = GetEndOfLine(eol);
        return _pieceTree.GetValueInRange(range, lineEnding);
    }

    public int GetValueLengthInRange(TextRange range, EndOfLinePreference eol = EndOfLinePreference.TextDefined)
    {
        if (range.IsEmpty)
        {
            return 0;
        }

        if (range.StartLineNumber == range.EndLineNumber)
        {
            return range.EndColumn - range.StartColumn;
        }

        var startOffset = GetOffsetAt(range.StartLineNumber, range.StartColumn);
        var endOffset = GetOffsetAt(range.EndLineNumber, range.EndColumn);

        // offsets use the text EOL, so we need to compensate for length differences
        // if the requested EOL doesn't match the text EOL
        var eolOffsetCompensation = 0;
        var desiredEOL = GetEndOfLine(eol);
        var actualEOL = GetEOL();
        if (desiredEOL.Length != actualEOL.Length)
        {
            var delta = desiredEOL.Length - actualEOL.Length;
            var eolCount = range.EndLineNumber - range.StartLineNumber;
            eolOffsetCompensation = delta * eolCount;
        }

        return endOffset - startOffset + eolOffsetCompensation;
    }

    public int GetCharacterCountInRange(TextRange range, EndOfLinePreference eol = EndOfLinePreference.TextDefined)
    {
        if (_mightContainNonBasicASCII)
        {
            // we must count by iterating
            var result = 0;

            var fromLineNumber = range.StartLineNumber;
            var toLineNumber = range.EndLineNumber;
            for (var lineNumber = fromLineNumber; lineNumber <= toLineNumber; lineNumber++)
            {
                var lineContent = GetLineContent(lineNumber);
                var fromOffset = lineNumber == fromLineNumber ? range.StartColumn - 1 : 0;
                var toOffset = lineNumber == toLineNumber ? range.EndColumn - 1 : lineContent.Length;

                for (var offset = fromOffset; offset < toOffset; offset++)
                {
                    if (char.IsHighSurrogate(lineContent[offset]))
                    {
                        result++;
                        offset++;
                    }
                    else
                    {
                        result++;
                    }
                }
            }

            result += GetEndOfLine(eol).Length * (toLineNumber - fromLineNumber);

            return result;
        }

        return GetValueLengthInRange(range, eol);
    }

    public string GetNearestChunk(int offset) => _pieceTree.GetNearestChunk(offset);

    public int GetLength() => _pieceTree.GetLength();

    public int GetLineCount() => _pieceTree.GetLineCount();

    public string[] GetLinesContent() => _pieceTree.GetLinesContent();

    public string GetLineContent(int lineNumber) => _pieceTree.GetLineContent(lineNumber);

    /// <summary>
    /// The content of a line as a view of the buffer's storage where that is possible (see
    /// <see cref="PieceTreeBase.GetLineContentSpan"/>), for code that reads the line and does not keep it.
    /// </summary>
    public ReadOnlySpan<char> GetLineContentSpan(int lineNumber) => _pieceTree.GetLineContentSpan(lineNumber);

    public int GetLineCharCode(int lineNumber, int index) => _pieceTree.GetLineCharCode(lineNumber, index);

    public int GetCharCode(int offset) => _pieceTree.GetCharCode(offset);

    public int GetLineLength(int lineNumber) => _pieceTree.GetLineLength(lineNumber);

    public int GetLineMinColumn(int lineNumber) => 1;

    public int GetLineMaxColumn(int lineNumber) => GetLineLength(lineNumber) + 1;

    public int GetLineFirstNonWhitespaceColumn(int lineNumber)
    {
        var result = Strings.FirstNonWhitespaceIndex(GetLineContent(lineNumber));
        if (result == -1)
        {
            return 0;
        }
        return result + 1;
    }

    public int GetLineLastNonWhitespaceColumn(int lineNumber)
    {
        var result = Strings.LastNonWhitespaceIndex(GetLineContent(lineNumber));
        if (result == -1)
        {
            return 0;
        }
        return result + 2;
    }

    /// <summary>Finds matches in a range through the piece tree, line by line.</summary>
    public List<FindMatch> FindMatchesLineByLine(
        TextRange searchRange,
        SearchData searchData,
        bool captureMatches,
        int limitResultCount) =>
        _pieceTree.FindMatchesLineByLine(searchRange, searchData, captureMatches, limitResultCount, _mightContainNonBasicASCII);

    private string GetEndOfLine(EndOfLinePreference eol) => eol switch
    {
        EndOfLinePreference.LF => "\n",
        EndOfLinePreference.CRLF => "\r\n",
        EndOfLinePreference.TextDefined => GetEOL(),
        _ => throw new ArgumentOutOfRangeException(nameof(eol), eol, "Unknown EOL preference")
    };

    public void SetEOL(string newEOL) => _pieceTree.SetEol(newEOL);

    public ApplyEditsResult ApplyEdits(
        IReadOnlyList<ValidAnnotatedEditOperation> rawOperations,
        bool recordTrimAutoWhitespace,
        bool computeUndoEdits)
    {
        var mightContainRTL = _mightContainRTL;
        var mightContainUnusualLineTerminators = _mightContainUnusualLineTerminators;
        var mightContainNonBasicASCII = _mightContainNonBasicASCII;
        var canReduceOperations = true;

        var operations = new List<ValidatedEditOperation>(rawOperations.Count);
        for (var i = 0; i < rawOperations.Count; i++)
        {
            var op = rawOperations[i];
            if (canReduceOperations && op.IsTracked)
            {
                canReduceOperations = false;
            }
            var validatedRange = op.Range;
            var hasText = !string.IsNullOrEmpty(op.Text);
            if (hasText)
            {
                var textMightContainNonBasicASCII = true;
                if (!mightContainNonBasicASCII)
                {
                    textMightContainNonBasicASCII = !Strings.IsBasicASCII(op.Text);
                    mightContainNonBasicASCII = textMightContainNonBasicASCII;
                }
                if (!mightContainRTL && textMightContainNonBasicASCII)
                {
                    // check if the new inserted text contains RTL
                    mightContainRTL = PieceTreeStrings.ContainsRtl(op.Text!);
                }
                if (!mightContainUnusualLineTerminators && textMightContainNonBasicASCII)
                {
                    // check if the new inserted text contains unusual line terminators
                    mightContainUnusualLineTerminators = PieceTreeStrings.ContainsUnusualLineTerminators(op.Text!);
                }
            }

            var validText = string.Empty;
            var eolCount = 0;
            var firstLineLength = 0;
            var lastLineLength = 0;
            if (hasText)
            {
                var counted = EolCounter.CountEOL(op.Text);
                eolCount = counted.Count;
                firstLineLength = counted.FirstLineLength;
                lastLineLength = counted.LastLineLength;

                var bufferEOL = GetEOL();
                var expectedStrEOL = bufferEOL == "\r\n" ? StringEol.CRLF : StringEol.LF;
                if (counted.Eol == StringEol.Unknown || counted.Eol == expectedStrEOL)
                {
                    validText = op.Text!;
                }
                else
                {
                    validText = PieceTreeStrings.ReplaceLineBreaks(op.Text!, bufferEOL);
                }
            }

            operations.Add(new ValidatedEditOperation
            {
                SortIndex = i,
                Identifier = op.Identifier,
                Range = validatedRange,
                RangeOffset = GetOffsetAt(validatedRange.StartLineNumber, validatedRange.StartColumn),
                RangeLength = GetValueLengthInRange(validatedRange),
                Text = validText,
                EolCount = eolCount,
                FirstLineLength = firstLineLength,
                LastLineLength = lastLineLength,
                ForceMoveMarkers = op.ForceMoveMarkers,
                IsAutoWhitespaceEdit = op.IsAutoWhitespaceEdit
            });
        }

        // Sort operations ascending
        operations.Sort(SortOpsAscending);

        var hasTouchingRanges = false;
        for (int i = 0, count = operations.Count - 1; i < count; i++)
        {
            var rangeEnd = operations[i].Range.End;
            var nextRangeStart = operations[i + 1].Range.Start;

            if (nextRangeStart.IsBeforeOrEqual(rangeEnd))
            {
                if (nextRangeStart.IsBefore(rangeEnd))
                {
                    // overlapping ranges
                    throw new InvalidOperationException("Overlapping ranges are not allowed!");
                }
                hasTouchingRanges = true;
            }
        }

        if (canReduceOperations)
        {
            operations = ReduceOperations(operations);
        }

        // Delta encode operations
        var reverseRanges = computeUndoEdits || recordTrimAutoWhitespace
            ? GetInverseEditRanges(operations)
            : new List<TextRange>();
        var newTrimAutoWhitespaceCandidates = new List<(int LineNumber, string OldContent, int Order)>();
        if (recordTrimAutoWhitespace)
        {
            for (var i = 0; i < operations.Count; i++)
            {
                var op = operations[i];
                var reverseRange = reverseRanges[i];

                if (op.IsAutoWhitespaceEdit && op.Range.IsEmpty)
                {
                    // Record already the future line numbers that might be auto whitespace removal candidates on next edit
                    for (var lineNumber = reverseRange.StartLineNumber; lineNumber <= reverseRange.EndLineNumber; lineNumber++)
                    {
                        var currentLineContent = string.Empty;
                        if (lineNumber == reverseRange.StartLineNumber)
                        {
                            currentLineContent = GetLineContent(op.Range.StartLineNumber);
                            if (Strings.FirstNonWhitespaceIndex(currentLineContent) != -1)
                            {
                                continue;
                            }
                        }
                        newTrimAutoWhitespaceCandidates.Add((lineNumber, currentLineContent, newTrimAutoWhitespaceCandidates.Count));
                    }
                }
            }
        }

        List<(int SortIndex, ValidEditOperation Operation)>? reverseOperations = null;
        if (computeUndoEdits)
        {
            var reverseRangeDeltaOffset = 0;
            reverseOperations = new List<(int, ValidEditOperation)>(operations.Count);
            for (var i = 0; i < operations.Count; i++)
            {
                var op = operations[i];
                var reverseRange = reverseRanges[i];
                var bufferText = GetValueInRange(op.Range);
                var reverseRangeOffset = op.RangeOffset + reverseRangeDeltaOffset;
                reverseRangeDeltaOffset += op.Text.Length - bufferText.Length;

                reverseOperations.Add((op.SortIndex, new ValidEditOperation(
                    op.Identifier,
                    reverseRange,
                    bufferText,
                    new TextChange(op.RangeOffset, bufferText, reverseRangeOffset, op.Text))));
            }

            // Can only sort reverse operations when the order is not significant
            if (!hasTouchingRanges)
            {
                reverseOperations.Sort(static (a, b) => a.SortIndex.CompareTo(b.SortIndex));
            }
        }

        _mightContainRTL = mightContainRTL;
        _mightContainUnusualLineTerminators = mightContainUnusualLineTerminators;
        _mightContainNonBasicASCII = mightContainNonBasicASCII;

        var contentChanges = DoApplyEdits(operations);

        List<int>? trimAutoWhitespaceLineNumbers = null;
        if (recordTrimAutoWhitespace && newTrimAutoWhitespaceCandidates.Count > 0)
        {
            // sort line numbers auto whitespace removal candidates for next edit descending
            // (upstream relies on a stable sort; the order they were found in breaks ties here)
            newTrimAutoWhitespaceCandidates.Sort(static (a, b) =>
                a.LineNumber != b.LineNumber ? b.LineNumber.CompareTo(a.LineNumber) : a.Order.CompareTo(b.Order));

            trimAutoWhitespaceLineNumbers = new List<int>();
            for (int i = 0, len = newTrimAutoWhitespaceCandidates.Count; i < len; i++)
            {
                var lineNumber = newTrimAutoWhitespaceCandidates[i].LineNumber;
                if (i > 0 && newTrimAutoWhitespaceCandidates[i - 1].LineNumber == lineNumber)
                {
                    // Do not have the same line number twice
                    continue;
                }

                var prevContent = newTrimAutoWhitespaceCandidates[i].OldContent;
                var lineContent = GetLineContent(lineNumber);

                if (lineContent.Length == 0 || lineContent == prevContent || Strings.FirstNonWhitespaceIndex(lineContent) != -1)
                {
                    continue;
                }

                trimAutoWhitespaceLineNumbers.Add(lineNumber);
            }
        }

        DidChangeContent?.Invoke();

        List<ValidEditOperation>? reverseEdits = null;
        if (reverseOperations is not null)
        {
            reverseEdits = new List<ValidEditOperation>(reverseOperations.Count);
            foreach (var (_, operation) in reverseOperations)
            {
                reverseEdits.Add(operation);
            }
        }

        return new ApplyEditsResult(reverseEdits, contentChanges, trimAutoWhitespaceLineNumbers);
    }

    /// <summary>
    /// Transform operations such that they represent the same logic edit,
    /// but that they also do not cause OOM crashes.
    /// </summary>
    private List<ValidatedEditOperation> ReduceOperations(List<ValidatedEditOperation> operations)
    {
        if (operations.Count < 1000)
        {
            // We know from empirical testing that a thousand edits work fine regardless of their shape.
            return operations;
        }

        // At one point, due to how events are emitted and how each operation is handled,
        // some operations can trigger a high amount of temporary string allocations,
        // that will immediately get edited again.
        // e.g. a formatter inserting ridiculous ammounts of \n on a model with a single line
        // Therefore, the strategy is to collapse all the operations into a huge single edit operation
        return new List<ValidatedEditOperation> { ToSingleEditOperation(operations) };
    }

    internal ValidatedEditOperation ToSingleEditOperation(List<ValidatedEditOperation> operations)
    {
        var forceMoveMarkers = false;
        var firstEditRange = operations[0].Range;
        var lastEditRange = operations[operations.Count - 1].Range;
        var entireEditRange = new TextRange(firstEditRange.StartLineNumber, firstEditRange.StartColumn, lastEditRange.EndLineNumber, lastEditRange.EndColumn);
        var lastEndLineNumber = firstEditRange.StartLineNumber;
        var lastEndColumn = firstEditRange.StartColumn;
        var result = new StringBuilder();

        for (int i = 0, len = operations.Count; i < len; i++)
        {
            var operation = operations[i];
            var range = operation.Range;

            forceMoveMarkers = forceMoveMarkers || operation.ForceMoveMarkers;

            // (1) -- Push old text
            result.Append(GetValueInRange(new TextRange(lastEndLineNumber, lastEndColumn, range.StartLineNumber, range.StartColumn)));

            // (2) -- Push new text
            if (operation.Text.Length > 0)
            {
                result.Append(operation.Text);
            }

            lastEndLineNumber = range.EndLineNumber;
            lastEndColumn = range.EndColumn;
        }

        var text = result.ToString();
        var counted = EolCounter.CountEOL(text);

        return new ValidatedEditOperation
        {
            SortIndex = 0,
            Identifier = operations[0].Identifier,
            Range = entireEditRange,
            RangeOffset = GetOffsetAt(entireEditRange.StartLineNumber, entireEditRange.StartColumn),
            RangeLength = GetValueLengthInRange(entireEditRange, EndOfLinePreference.TextDefined),
            Text = text,
            EolCount = counted.Count,
            FirstLineLength = counted.FirstLineLength,
            LastLineLength = counted.LastLineLength,
            ForceMoveMarkers = forceMoveMarkers,
            IsAutoWhitespaceEdit = false
        };
    }

    private List<InternalModelContentChange> DoApplyEdits(List<ValidatedEditOperation> operations)
    {
        operations.Sort(SortOpsDescending);

        var contentChanges = new List<InternalModelContentChange>(operations.Count);

        // operations are from bottom to top
        for (var i = 0; i < operations.Count; i++)
        {
            var op = operations[i];

            var startLineNumber = op.Range.StartLineNumber;
            var startColumn = op.Range.StartColumn;
            var endLineNumber = op.Range.EndLineNumber;
            var endColumn = op.Range.EndColumn;

            if (startLineNumber == endLineNumber && startColumn == endColumn && op.Text.Length == 0)
            {
                // no-op
                continue;
            }

            if (op.Text.Length > 0)
            {
                // replacement
                _pieceTree.Delete(op.RangeOffset, op.RangeLength);
                _pieceTree.Insert(op.RangeOffset, op.Text, true);
            }
            else
            {
                // deletion
                _pieceTree.Delete(op.RangeOffset, op.RangeLength);
            }

            var contentChangeRange = new TextRange(startLineNumber, startColumn, endLineNumber, endColumn);
            contentChanges.Add(new InternalModelContentChange(
                contentChangeRange,
                op.RangeOffset,
                op.RangeLength,
                op.Text,
                op.ForceMoveMarkers));
        }
        return contentChanges;
    }

    // #endregion

    // #region helper

    /// <summary>For tests.</summary>
    public PieceTreeBase GetPieceTree() => _pieceTree;

    public static TextRange GetInverseEditRange(TextRange range, string text)
    {
        var startLineNumber = range.StartLineNumber;
        var startColumn = range.StartColumn;
        var counted = EolCounter.CountEOL(text);
        TextRange resultRange;

        if (text.Length > 0)
        {
            // the operation inserts something
            var lineCount = counted.Count + 1;

            if (lineCount == 1)
            {
                // single line insert
                resultRange = new TextRange(startLineNumber, startColumn, startLineNumber, startColumn + counted.FirstLineLength);
            }
            else
            {
                // multi line insert
                resultRange = new TextRange(startLineNumber, startColumn, startLineNumber + lineCount - 1, counted.LastLineLength + 1);
            }
        }
        else
        {
            // There is nothing to insert
            resultRange = new TextRange(startLineNumber, startColumn, startLineNumber, startColumn);
        }

        return resultRange;
    }

    /// <summary>Assumes <paramref name="operations"/> are validated and sorted ascending.</summary>
    public static List<TextRange> GetInverseEditRanges(List<ValidatedEditOperation> operations)
    {
        var result = new List<TextRange>(operations.Count);

        var prevOpEndLineNumber = 0;
        var prevOpEndColumn = 0;
        ValidatedEditOperation? prevOp = null;
        for (int i = 0, len = operations.Count; i < len; i++)
        {
            var op = operations[i];

            int startLineNumber;
            int startColumn;

            if (prevOp is not null)
            {
                if (prevOp.Range.EndLineNumber == op.Range.StartLineNumber)
                {
                    startLineNumber = prevOpEndLineNumber;
                    startColumn = prevOpEndColumn + (op.Range.StartColumn - prevOp.Range.EndColumn);
                }
                else
                {
                    startLineNumber = prevOpEndLineNumber + (op.Range.StartLineNumber - prevOp.Range.EndLineNumber);
                    startColumn = op.Range.StartColumn;
                }
            }
            else
            {
                startLineNumber = op.Range.StartLineNumber;
                startColumn = op.Range.StartColumn;
            }

            TextRange resultRange;

            if (op.Text.Length > 0)
            {
                // the operation inserts something
                var lineCount = op.EolCount + 1;

                if (lineCount == 1)
                {
                    // single line insert
                    resultRange = new TextRange(startLineNumber, startColumn, startLineNumber, startColumn + op.FirstLineLength);
                }
                else
                {
                    // multi line insert
                    resultRange = new TextRange(startLineNumber, startColumn, startLineNumber + lineCount - 1, op.LastLineLength + 1);
                }
            }
            else
            {
                // There is nothing to insert
                resultRange = new TextRange(startLineNumber, startColumn, startLineNumber, startColumn);
            }

            prevOpEndLineNumber = resultRange.EndLineNumber;
            prevOpEndColumn = resultRange.EndColumn;

            result.Add(resultRange);
            prevOp = op;
        }

        return result;
    }

    // The comparers break every tie by SortIndex, so List.Sort (not stable) gives the same order as upstream's stable sort.
    private static int SortOpsAscending(ValidatedEditOperation a, ValidatedEditOperation b)
    {
        var r = TextRange.CompareRangesUsingEnds(a.Range, b.Range);
        if (r == 0)
        {
            return a.SortIndex - b.SortIndex;
        }
        return r;
    }

    private static int SortOpsDescending(ValidatedEditOperation a, ValidatedEditOperation b)
    {
        var r = TextRange.CompareRangesUsingEnds(a.Range, b.Range);
        if (r == 0)
        {
            return b.SortIndex - a.SortIndex;
        }
        return -r;
    }

    // #endregion
}
