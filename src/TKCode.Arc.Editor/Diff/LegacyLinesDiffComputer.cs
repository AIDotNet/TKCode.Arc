// Ported from vscode (src/vs/editor/common/diff/legacyLinesDiffComputer.ts and lineRangeMappingFromChange of
// rangeMapping.ts, commit b57d5c9e), MIT license.
// Differences: ILineChange / ICharChange are classes with settable members (upstream's LineChange / CharChange); the
// computation-time predicates measure with a Stopwatch; the assertFn check is not ported.

using System.Diagnostics;

namespace TKCode.Arc.Editor.Diff;

/// <summary>The legacy diff (the LCS diff of earlier Monaco versions) as an <see cref="ILinesDiffComputer"/>.</summary>
internal sealed class LegacyLinesDiffComputer : ILinesDiffComputer
{
    public LinesDiff ComputeDiff(IReadOnlyList<string> originalLines, IReadOnlyList<string> modifiedLines, LinesDiffComputerOptions options)
    {
        var diffComputer = new DiffComputer(originalLines, modifiedLines, new DiffComputerOpts
        {
            MaxComputationTime = options.MaxComputationTimeMs,
            ShouldIgnoreTrimWhitespace = options.IgnoreTrimWhitespace,
            ShouldComputeCharChanges = true,
            ShouldMakePrettyDiff = true,
            ShouldPostProcessCharChanges = true,
        });
        var result = diffComputer.ComputeDiff();
        var changes = new List<DetailedLineRangeMapping>();
        DetailedLineRangeMapping? lastChange = null;

        foreach (var c in result.Changes)
        {
            var originalRange = c.OriginalEndLineNumber == 0
                // Insertion
                ? new LineRange(c.OriginalStartLineNumber + 1, c.OriginalStartLineNumber + 1)
                : new LineRange(c.OriginalStartLineNumber, c.OriginalEndLineNumber + 1);

            var modifiedRange = c.ModifiedEndLineNumber == 0
                // Deletion
                ? new LineRange(c.ModifiedStartLineNumber + 1, c.ModifiedStartLineNumber + 1)
                : new LineRange(c.ModifiedStartLineNumber, c.ModifiedEndLineNumber + 1);

            var change = new DetailedLineRangeMapping(originalRange, modifiedRange, c.CharChanges?.Select(cc => new RangeMapping(
                new TextRange(cc.OriginalStartLineNumber, cc.OriginalStartColumn, cc.OriginalEndLineNumber, cc.OriginalEndColumn),
                new TextRange(cc.ModifiedStartLineNumber, cc.ModifiedStartColumn, cc.ModifiedEndLineNumber, cc.ModifiedEndColumn))).ToList());
            if (lastChange is not null)
            {
                if (lastChange.Modified.EndLineNumberExclusive == change.Modified.StartLineNumber
                    || lastChange.Original.EndLineNumberExclusive == change.Original.StartLineNumber)
                {
                    // join touching diffs. Probably moving diffs up/down in the algorithm causes touching diffs.
                    change = new DetailedLineRangeMapping(
                        lastChange.Original.Join(change.Original),
                        lastChange.Modified.Join(change.Modified),
                        lastChange.InnerChanges is not null && change.InnerChanges is not null
                            ? [.. lastChange.InnerChanges, .. change.InnerChanges]
                            : null);
                    changes.RemoveAt(changes.Count - 1);
                }
            }

            changes.Add(change);
            lastChange = change;
        }

        return new LinesDiff(changes, [], result.QuitEarly);
    }

    /// <summary>The line range mapping of a legacy change (upstream's lineRangeMappingFromChange).</summary>
    public static LineRangeMapping LineRangeMappingFromChange(LineChange change)
    {
        var originalRange = change.OriginalEndLineNumber == 0
            ? new LineRange(change.OriginalStartLineNumber + 1, change.OriginalStartLineNumber + 1)
            : new LineRange(change.OriginalStartLineNumber, change.OriginalEndLineNumber + 1);
        var modifiedRange = change.ModifiedEndLineNumber == 0
            ? new LineRange(change.ModifiedStartLineNumber + 1, change.ModifiedStartLineNumber + 1)
            : new LineRange(change.ModifiedStartLineNumber, change.ModifiedEndLineNumber + 1);
        return new LineRangeMapping(originalRange, modifiedRange);
    }
}

/// <summary>A character level change (upstream's ICharChange / CharChange).</summary>
internal sealed record CharChange(
    int OriginalStartLineNumber,
    int OriginalStartColumn,
    int OriginalEndLineNumber,
    int OriginalEndColumn,
    int ModifiedStartLineNumber,
    int ModifiedStartColumn,
    int ModifiedEndLineNumber,
    int ModifiedEndColumn)
{
    internal static CharChange CreateFromDiffChange(DiffChange diffChange, LegacyCharSequence originalCharSequence, LegacyCharSequence modifiedCharSequence)
    {
        var originalStartLineNumber = originalCharSequence.GetStartLineNumber(diffChange.OriginalStart);
        var originalStartColumn = originalCharSequence.GetStartColumn(diffChange.OriginalStart);
        var originalEndLineNumber = originalCharSequence.GetEndLineNumber(diffChange.OriginalStart + diffChange.OriginalLength - 1);
        var originalEndColumn = originalCharSequence.GetEndColumn(diffChange.OriginalStart + diffChange.OriginalLength - 1);

        var modifiedStartLineNumber = modifiedCharSequence.GetStartLineNumber(diffChange.ModifiedStart);
        var modifiedStartColumn = modifiedCharSequence.GetStartColumn(diffChange.ModifiedStart);
        var modifiedEndLineNumber = modifiedCharSequence.GetEndLineNumber(diffChange.ModifiedStart + diffChange.ModifiedLength - 1);
        var modifiedEndColumn = modifiedCharSequence.GetEndColumn(diffChange.ModifiedStart + diffChange.ModifiedLength - 1);

        return new CharChange(
            originalStartLineNumber, originalStartColumn, originalEndLineNumber, originalEndColumn,
            modifiedStartLineNumber, modifiedStartColumn, modifiedEndLineNumber, modifiedEndColumn);
    }
}

/// <summary>
/// A line change (upstream's ILineChange / LineChange). An end line number of 0 marks an insertion (original) or a
/// deletion (modified); the start line number is then the line before it.
/// </summary>
internal sealed class LineChange(int originalStartLineNumber, int originalEndLineNumber, int modifiedStartLineNumber, int modifiedEndLineNumber, List<CharChange>? charChanges)
{
    public int OriginalStartLineNumber { get; set; } = originalStartLineNumber;

    public int OriginalEndLineNumber { get; set; } = originalEndLineNumber;

    public int ModifiedStartLineNumber { get; set; } = modifiedStartLineNumber;

    public int ModifiedEndLineNumber { get; set; } = modifiedEndLineNumber;

    public List<CharChange>? CharChanges { get; set; } = charChanges;

    internal static LineChange CreateFromDiffResult(
        bool shouldIgnoreTrimWhitespace,
        DiffChange diffChange,
        LegacyLineSequence originalLineSequence,
        LegacyLineSequence modifiedLineSequence,
        Func<bool> continueCharDiff,
        bool shouldComputeCharChanges,
        bool shouldPostProcessCharChanges)
    {
        int originalStartLineNumber;
        int originalEndLineNumber;
        int modifiedStartLineNumber;
        int modifiedEndLineNumber;
        List<CharChange>? charChanges = null;

        if (diffChange.OriginalLength == 0)
        {
            originalStartLineNumber = LegacyLineSequence.GetStartLineNumber(diffChange.OriginalStart) - 1;
            originalEndLineNumber = 0;
        }
        else
        {
            originalStartLineNumber = LegacyLineSequence.GetStartLineNumber(diffChange.OriginalStart);
            originalEndLineNumber = LegacyLineSequence.GetEndLineNumber(diffChange.OriginalStart + diffChange.OriginalLength - 1);
        }

        if (diffChange.ModifiedLength == 0)
        {
            modifiedStartLineNumber = LegacyLineSequence.GetStartLineNumber(diffChange.ModifiedStart) - 1;
            modifiedEndLineNumber = 0;
        }
        else
        {
            modifiedStartLineNumber = LegacyLineSequence.GetStartLineNumber(diffChange.ModifiedStart);
            modifiedEndLineNumber = LegacyLineSequence.GetEndLineNumber(diffChange.ModifiedStart + diffChange.ModifiedLength - 1);
        }

        if (shouldComputeCharChanges && diffChange.OriginalLength > 0 && diffChange.OriginalLength < 20 && diffChange.ModifiedLength > 0
            && diffChange.ModifiedLength < 20 && continueCharDiff())
        {
            // Compute character changes for diff chunks of at most 20 lines...
            var originalCharSequence = originalLineSequence.CreateCharSequence(shouldIgnoreTrimWhitespace, diffChange.OriginalStart, diffChange.OriginalStart + diffChange.OriginalLength - 1);
            var modifiedCharSequence = modifiedLineSequence.CreateCharSequence(shouldIgnoreTrimWhitespace, diffChange.ModifiedStart, diffChange.ModifiedStart + diffChange.ModifiedLength - 1);

            if (originalCharSequence.GetIntElements()!.Length > 0 && modifiedCharSequence.GetIntElements()!.Length > 0)
            {
                var rawChanges = new LcsDiff(originalCharSequence, modifiedCharSequence, (_, _) => continueCharDiff()).ComputeDiff(true).Changes.ToList();

                if (shouldPostProcessCharChanges)
                {
                    rawChanges = PostProcessCharChanges(rawChanges);
                }

                charChanges = [];
                foreach (var rawChange in rawChanges)
                {
                    charChanges.Add(CharChange.CreateFromDiffChange(rawChange, originalCharSequence, modifiedCharSequence));
                }
            }
        }

        return new LineChange(originalStartLineNumber, originalEndLineNumber, modifiedStartLineNumber, modifiedEndLineNumber, charChanges);
    }

    private const int MinimumMatchingCharacterLength = 3;

    private static List<DiffChange> PostProcessCharChanges(List<DiffChange> rawChanges)
    {
        if (rawChanges.Count <= 1)
        {
            return rawChanges;
        }

        var result = new List<DiffChange> { rawChanges[0] };
        var prevChange = result[0];

        for (var i = 1; i < rawChanges.Count; i++)
        {
            var currChange = rawChanges[i];

            var originalMatchingLength = currChange.OriginalStart - (prevChange.OriginalStart + prevChange.OriginalLength);
            var modifiedMatchingLength = currChange.ModifiedStart - (prevChange.ModifiedStart + prevChange.ModifiedLength);
            // Both of the above should be equal, but the continueProcessingPredicate may prevent this from being true
            var matchingLength = Math.Min(originalMatchingLength, modifiedMatchingLength);

            if (matchingLength < MinimumMatchingCharacterLength)
            {
                // Merge the current change into the previous one
                prevChange.OriginalLength = currChange.OriginalStart + currChange.OriginalLength - prevChange.OriginalStart;
                prevChange.ModifiedLength = currChange.ModifiedStart + currChange.ModifiedLength - prevChange.ModifiedStart;
            }
            else
            {
                // Add the current change
                result.Add(currChange);
                prevChange = currChange;
            }
        }

        return result;
    }
}

/// <summary>The options of <see cref="DiffComputer"/> (upstream's IDiffComputerOpts).</summary>
internal sealed record DiffComputerOpts
{
    public bool ShouldComputeCharChanges { get; init; }

    public bool ShouldPostProcessCharChanges { get; init; }

    public bool ShouldIgnoreTrimWhitespace { get; init; }

    public bool ShouldMakePrettyDiff { get; init; }

    /// <summary>The time the computation may take, in milliseconds; 0 means no limit.</summary>
    public int MaxComputationTime { get; init; }
}

/// <summary>The result of <see cref="DiffComputer"/>.</summary>
internal sealed record DiffComputerResult(bool QuitEarly, List<LineChange> Changes);

/// <summary>Lines as a sequence of their trimmed text (upstream's legacy LineSequence).</summary>
internal sealed class LegacyLineSequence : IDiffSequence
{
    private readonly int[] _startColumns;
    private readonly int[] _endColumns;

    public LegacyLineSequence(IReadOnlyList<string> lines)
    {
        _startColumns = new int[lines.Count];
        _endColumns = new int[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            _startColumns[i] = DiffComputer.GetFirstNonBlankColumn(lines[i], 1);
            _endColumns[i] = DiffComputer.GetLastNonBlankColumn(lines[i], 1);
        }

        Lines = lines;
    }

    public IReadOnlyList<string> Lines { get; }

    public string[]? GetStringElements()
    {
        var elements = new string[Lines.Count];
        for (var i = 0; i < Lines.Count; i++)
        {
            var start = _startColumns[i] - 1;
            var end = Math.Max(start, _endColumns[i] - 1);
            elements[i] = Lines[i].Substring(start, end - start);
        }

        return elements;
    }

    public int[]? GetIntElements() => null;

    public string? GetStrictElement(int index) => Lines[index];

    public static int GetStartLineNumber(int i) => i + 1;

    public static int GetEndLineNumber(int i) => i + 1;

    public LegacyCharSequence CreateCharSequence(bool shouldIgnoreTrimWhitespace, int startIndex, int endIndex)
    {
        var charCodes = new List<int>();
        var lineNumbers = new List<int>();
        var columns = new List<int>();
        for (var index = startIndex; index <= endIndex; index++)
        {
            var lineContent = Lines[index];
            var startColumn = shouldIgnoreTrimWhitespace ? _startColumns[index] : 1;
            var endColumn = shouldIgnoreTrimWhitespace ? _endColumns[index] : lineContent.Length + 1;
            for (var col = startColumn; col < endColumn; col++)
            {
                charCodes.Add(lineContent[col - 1]);
                lineNumbers.Add(index + 1);
                columns.Add(col);
            }

            if (!shouldIgnoreTrimWhitespace && index < endIndex)
            {
                // Add \n if trim whitespace is not ignored
                charCodes.Add('\n');
                lineNumbers.Add(index + 1);
                columns.Add(lineContent.Length + 1);
            }
        }

        return new LegacyCharSequence([.. charCodes], [.. lineNumbers], [.. columns]);
    }
}

/// <summary>Characters of lines with their line numbers and columns (upstream's legacy CharSequence).</summary>
internal sealed class LegacyCharSequence(int[] charCodes, int[] lineNumbers, int[] columns) : IDiffSequence
{
    public string[]? GetStringElements() => null;

    public int[]? GetIntElements() => charCodes;

    public override string ToString() =>
        "[" + string.Join(", ", charCodes.Select((s, idx) => (s == '\n' ? "\\n" : ((char)s).ToString()) + $"-({lineNumbers[idx]},{columns[idx]})")) + "]";

    private static void AssertIndex(int index, int[] arr)
    {
        if (index < 0 || index >= arr.Length)
        {
            throw new InvalidOperationException("Illegal index");
        }
    }

    public int GetStartLineNumber(int i)
    {
        if (i > 0 && i == lineNumbers.Length)
        {
            // the start line number of the element after the last element is the end line number of the last element
            return GetEndLineNumber(i - 1);
        }

        AssertIndex(i, lineNumbers);
        return lineNumbers[i];
    }

    public int GetEndLineNumber(int i)
    {
        if (i == -1)
        {
            // the end line number of the element before the first element is the start line number of the first element
            return GetStartLineNumber(i + 1);
        }

        AssertIndex(i, lineNumbers);
        if (charCodes[i] == '\n')
        {
            return lineNumbers[i] + 1;
        }

        return lineNumbers[i];
    }

    public int GetStartColumn(int i)
    {
        if (i > 0 && i == columns.Length)
        {
            // the start column of the element after the last element is the end column of the last element
            return GetEndColumn(i - 1);
        }

        AssertIndex(i, columns);
        return columns[i];
    }

    public int GetEndColumn(int i)
    {
        if (i == -1)
        {
            // the end column of the element before the first element is the start column of the first element
            return GetStartColumn(i + 1);
        }

        AssertIndex(i, columns);
        if (charCodes[i] == '\n')
        {
            return 1;
        }

        return columns[i] + 1;
    }
}

/// <summary>The legacy line diff with character changes (upstream's DiffComputer).</summary>
internal sealed class DiffComputer
{
    private readonly bool _shouldComputeCharChanges;
    private readonly bool _shouldPostProcessCharChanges;
    private readonly bool _shouldIgnoreTrimWhitespace;
    private readonly bool _shouldMakePrettyDiff;
    private readonly IReadOnlyList<string> _originalLines;
    private readonly IReadOnlyList<string> _modifiedLines;
    private readonly LegacyLineSequence _original;
    private readonly LegacyLineSequence _modified;
    private readonly Func<bool> _continueLineDiff;
    private readonly Func<bool> _continueCharDiff;

    public DiffComputer(IReadOnlyList<string> originalLines, IReadOnlyList<string> modifiedLines, DiffComputerOpts opts)
    {
        _shouldComputeCharChanges = opts.ShouldComputeCharChanges;
        _shouldPostProcessCharChanges = opts.ShouldPostProcessCharChanges;
        _shouldIgnoreTrimWhitespace = opts.ShouldIgnoreTrimWhitespace;
        _shouldMakePrettyDiff = opts.ShouldMakePrettyDiff;
        _originalLines = originalLines;
        _modifiedLines = modifiedLines;
        _original = new LegacyLineSequence(originalLines);
        _modified = new LegacyLineSequence(modifiedLines);

        _continueLineDiff = CreateContinueProcessingPredicate(opts.MaxComputationTime);
        // never run after 5s for character changes...
        _continueCharDiff = CreateContinueProcessingPredicate(opts.MaxComputationTime == 0 ? 0 : Math.Min(opts.MaxComputationTime, 5000));
    }

    public DiffComputerResult ComputeDiff()
    {
        if (_original.Lines.Count == 1 && _original.Lines[0].Length == 0)
        {
            // empty original => fast path
            if (_modified.Lines.Count == 1 && _modified.Lines[0].Length == 0)
            {
                return new DiffComputerResult(false, []);
            }

            return new DiffComputerResult(false, [new LineChange(1, 1, 1, _modified.Lines.Count, null)]);
        }

        if (_modified.Lines.Count == 1 && _modified.Lines[0].Length == 0)
        {
            // empty modified => fast path
            return new DiffComputerResult(false, [new LineChange(1, _original.Lines.Count, 1, 1, null)]);
        }

        var diffResult = new LcsDiff(_original, _modified, (_, _) => _continueLineDiff()).ComputeDiff(_shouldMakePrettyDiff);
        var rawChanges = diffResult.Changes;
        var quitEarly = diffResult.QuitEarly;

        // The diff is always computed with ignoring trim whitespace
        // This ensures we get the prettiest diff

        if (_shouldIgnoreTrimWhitespace)
        {
            var lineChanges = new List<LineChange>();
            foreach (var rawChange in rawChanges)
            {
                lineChanges.Add(LineChange.CreateFromDiffResult(_shouldIgnoreTrimWhitespace, rawChange, _original, _modified, _continueCharDiff, _shouldComputeCharChanges, _shouldPostProcessCharChanges));
            }

            return new DiffComputerResult(quitEarly, lineChanges);
        }

        // Need to post-process and introduce changes where the trim whitespace is different
        // Note that we are looping starting at -1 to also cover the lines before the first change
        var result = new List<LineChange>();

        var originalLineIndex = 0;
        var modifiedLineIndex = 0;
        for (int i = -1 /* !!!! */, len = rawChanges.Count; i < len; i++)
        {
            var nextChange = i + 1 < len ? rawChanges[i + 1] : null;
            var originalStop = nextChange is not null ? nextChange.OriginalStart : _originalLines.Count;
            var modifiedStop = nextChange is not null ? nextChange.ModifiedStart : _modifiedLines.Count;

            while (originalLineIndex < originalStop && modifiedLineIndex < modifiedStop)
            {
                var originalLine = _originalLines[originalLineIndex];
                var modifiedLine = _modifiedLines[modifiedLineIndex];

                if (!string.Equals(originalLine, modifiedLine, StringComparison.Ordinal))
                {
                    // These lines differ only in trim whitespace

                    // Check the leading whitespace
                    {
                        var originalStartColumn = GetFirstNonBlankColumn(originalLine, 1);
                        var modifiedStartColumn = GetFirstNonBlankColumn(modifiedLine, 1);
                        while (originalStartColumn > 1 && modifiedStartColumn > 1)
                        {
                            var originalChar = originalLine[originalStartColumn - 2];
                            var modifiedChar = modifiedLine[modifiedStartColumn - 2];
                            if (originalChar != modifiedChar)
                            {
                                break;
                            }

                            originalStartColumn--;
                            modifiedStartColumn--;
                        }

                        if (originalStartColumn > 1 || modifiedStartColumn > 1)
                        {
                            PushTrimWhitespaceCharChange(result,
                                originalLineIndex + 1, 1, originalStartColumn,
                                modifiedLineIndex + 1, 1, modifiedStartColumn);
                        }
                    }

                    // Check the trailing whitespace
                    {
                        var originalEndColumn = GetLastNonBlankColumn(originalLine, 1);
                        var modifiedEndColumn = GetLastNonBlankColumn(modifiedLine, 1);
                        var originalMaxColumn = originalLine.Length + 1;
                        var modifiedMaxColumn = modifiedLine.Length + 1;
                        while (originalEndColumn < originalMaxColumn && modifiedEndColumn < modifiedMaxColumn)
                        {
                            var originalChar = originalLine[originalEndColumn - 1];
                            // upstream reads originalLine here too (sic); charCodeAt past the end is NaN
                            var modifiedChar = modifiedEndColumn - 1 < originalLine.Length ? originalLine[modifiedEndColumn - 1] : -1;
                            if (originalChar != modifiedChar)
                            {
                                break;
                            }

                            originalEndColumn++;
                            modifiedEndColumn++;
                        }

                        if (originalEndColumn < originalMaxColumn || modifiedEndColumn < modifiedMaxColumn)
                        {
                            PushTrimWhitespaceCharChange(result,
                                originalLineIndex + 1, originalEndColumn, originalMaxColumn,
                                modifiedLineIndex + 1, modifiedEndColumn, modifiedMaxColumn);
                        }
                    }
                }

                originalLineIndex++;
                modifiedLineIndex++;
            }

            if (nextChange is not null)
            {
                // Emit the actual change
                result.Add(LineChange.CreateFromDiffResult(_shouldIgnoreTrimWhitespace, nextChange, _original, _modified, _continueCharDiff, _shouldComputeCharChanges, _shouldPostProcessCharChanges));

                originalLineIndex += nextChange.OriginalLength;
                modifiedLineIndex += nextChange.ModifiedLength;
            }
        }

        return new DiffComputerResult(quitEarly, result);
    }

    private void PushTrimWhitespaceCharChange(
        List<LineChange> result,
        int originalLineNumber, int originalStartColumn, int originalEndColumn,
        int modifiedLineNumber, int modifiedStartColumn, int modifiedEndColumn)
    {
        if (MergeTrimWhitespaceCharChange(result, originalLineNumber, originalStartColumn, originalEndColumn, modifiedLineNumber, modifiedStartColumn, modifiedEndColumn))
        {
            // Merged into previous
            return;
        }

        List<CharChange>? charChanges = null;
        if (_shouldComputeCharChanges)
        {
            charChanges =
            [
                new CharChange(
                    originalLineNumber, originalStartColumn, originalLineNumber, originalEndColumn,
                    modifiedLineNumber, modifiedStartColumn, modifiedLineNumber, modifiedEndColumn),
            ];
        }

        result.Add(new LineChange(originalLineNumber, originalLineNumber, modifiedLineNumber, modifiedLineNumber, charChanges));
    }

    private bool MergeTrimWhitespaceCharChange(
        List<LineChange> result,
        int originalLineNumber, int originalStartColumn, int originalEndColumn,
        int modifiedLineNumber, int modifiedStartColumn, int modifiedEndColumn)
    {
        var len = result.Count;
        if (len == 0)
        {
            return false;
        }

        var prevChange = result[len - 1];

        if (prevChange.OriginalEndLineNumber == 0 || prevChange.ModifiedEndLineNumber == 0)
        {
            // Don't merge with inserts/deletes
            return false;
        }

        if (prevChange.OriginalEndLineNumber == originalLineNumber && prevChange.ModifiedEndLineNumber == modifiedLineNumber)
        {
            if (_shouldComputeCharChanges && prevChange.CharChanges is not null)
            {
                prevChange.CharChanges.Add(new CharChange(
                    originalLineNumber, originalStartColumn, originalLineNumber, originalEndColumn,
                    modifiedLineNumber, modifiedStartColumn, modifiedLineNumber, modifiedEndColumn));
            }

            return true;
        }

        if (prevChange.OriginalEndLineNumber + 1 == originalLineNumber && prevChange.ModifiedEndLineNumber + 1 == modifiedLineNumber)
        {
            prevChange.OriginalEndLineNumber = originalLineNumber;
            prevChange.ModifiedEndLineNumber = modifiedLineNumber;
            if (_shouldComputeCharChanges && prevChange.CharChanges is not null)
            {
                prevChange.CharChanges.Add(new CharChange(
                    originalLineNumber, originalStartColumn, originalLineNumber, originalEndColumn,
                    modifiedLineNumber, modifiedStartColumn, modifiedLineNumber, modifiedEndColumn));
            }

            return true;
        }

        return false;
    }

    internal static int GetFirstNonBlankColumn(string txt, int defaultValue)
    {
        var r = Strings.FirstNonWhitespaceIndex(txt);
        return r == -1 ? defaultValue : r + 1;
    }

    internal static int GetLastNonBlankColumn(string txt, int defaultValue)
    {
        var r = Strings.LastNonWhitespaceIndex(txt);
        return r == -1 ? defaultValue : r + 2;
    }

    private static Func<bool> CreateContinueProcessingPredicate(int maximumRuntime)
    {
        if (maximumRuntime == 0)
        {
            return static () => true;
        }

        var startTime = Stopwatch.GetTimestamp();
        return () => Stopwatch.GetElapsedTime(startTime).TotalMilliseconds < maximumRuntime;
    }
}
