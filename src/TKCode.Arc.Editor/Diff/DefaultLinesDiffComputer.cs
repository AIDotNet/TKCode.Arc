// Ported from vscode (src/vs/editor/common/diff/defaultLinesDiffComputer/defaultLinesDiffComputer.ts, commit b57d5c9e),
// MIT license.
// Differences:
// - The perfect hashes of trimmed lines use an ordinal dictionary local to the computation, as upstream.
// - The timeout also ends when options.CancellationToken is cancelled.
// - The position checks of upstream's assertFn run in Debug builds only.

using System.Diagnostics;
using TKCode.Arc.Editor.Diff.Algorithms;

namespace TKCode.Arc.Editor.Diff;

/// <summary>The default ("advanced") diff: line alignment, then character-level refinement, then optional move detection.</summary>
internal sealed class DefaultLinesDiffComputer : ILinesDiffComputer
{
    private readonly DynamicProgrammingDiffing _dynamicProgrammingDiffing = new();
    private readonly MyersDiffAlgorithm _myersDiffingAlgorithm = new();

    public LinesDiff ComputeDiff(IReadOnlyList<string> originalLines, IReadOnlyList<string> modifiedLines, LinesDiffComputerOptions options) =>
        ComputeDiffCore(originalLines, modifiedLines, options, null);

    /// <summary>Also returns unweighted character changes, reusing the character diff for large slices.</summary>
    public (LinesDiff Diff, IReadOnlyList<RangeMapping> RawChanges) ComputeDiffWithRawChanges(
        IReadOnlyList<string> originalLines, IReadOnlyList<string> modifiedLines, LinesDiffComputerOptions options)
    {
        var rawChanges = new List<RangeMapping>();
        var diff = ComputeDiffCore(originalLines, modifiedLines, options, rawChanges);
        return (diff, rawChanges);
    }

    private LinesDiff ComputeDiffCore(IReadOnlyList<string> originalLines, IReadOnlyList<string> modifiedLines, LinesDiffComputerOptions options, List<RangeMapping>? rawChanges)
    {
        if (originalLines.Count <= 1 && originalLines.SequenceEqual(modifiedLines, StringComparer.Ordinal))
        {
            return new LinesDiff([], [], false);
        }

        if ((originalLines.Count == 1 && originalLines[0].Length == 0) || (modifiedLines.Count == 1 && modifiedLines[0].Length == 0))
        {
            var rangeMapping = new RangeMapping(
                new TextRange(1, 1, originalLines.Count, originalLines[^1].Length + 1),
                new TextRange(1, 1, modifiedLines.Count, modifiedLines[^1].Length + 1));
            rawChanges?.Add(rangeMapping);
            return new LinesDiff(
            [
                new DetailedLineRangeMapping(
                    new LineRange(1, originalLines.Count + 1),
                    new LineRange(1, modifiedLines.Count + 1),
                    [rangeMapping]),
            ], [], false);
        }

        ITimeout timeout = options.MaxComputationTimeMs == 0
            ? options.CancellationToken.CanBeCanceled ? new CancellationTimeout(options.CancellationToken) : InfiniteTimeout.Instance
            : new DateTimeout(options.MaxComputationTimeMs, options.CancellationToken);
        var considerWhitespaceChanges = !options.IgnoreTrimWhitespace;

        var perfectHashes = new Dictionary<string, int>(StringComparer.Ordinal);

        int GetOrCreateHash(string text)
        {
            if (!perfectHashes.TryGetValue(text, out var hash))
            {
                hash = perfectHashes.Count;
                perfectHashes[text] = hash;
            }

            return hash;
        }

        var originalLinesHashes = new int[originalLines.Count];
        for (var i = 0; i < originalLinesHashes.Length; i++)
        {
            originalLinesHashes[i] = GetOrCreateHash(DiffUtils.JsTrim(originalLines[i]));
        }

        var modifiedLinesHashes = new int[modifiedLines.Count];
        for (var i = 0; i < modifiedLinesHashes.Length; i++)
        {
            modifiedLinesHashes[i] = GetOrCreateHash(DiffUtils.JsTrim(modifiedLines[i]));
        }

        var sequence1 = new LineSequence(originalLinesHashes, originalLines);
        var sequence2 = new LineSequence(modifiedLinesHashes, modifiedLines);

        DiffAlgorithmResult lineAlignmentResult;
        if (sequence1.Length + sequence2.Length < 1700)
        {
            // Use the improved algorithm for small files
            lineAlignmentResult = _dynamicProgrammingDiffing.Compute(
                sequence1,
                sequence2,
                timeout,
                (offset1, offset2) =>
                    string.Equals(originalLines[offset1], modifiedLines[offset2], StringComparison.Ordinal)
                        ? modifiedLines[offset2].Length == 0
                            ? 0.1
                            : 1 + Math.Log(1 + modifiedLines[offset2].Length)
                        : 0.99);
        }
        else
        {
            lineAlignmentResult = _myersDiffingAlgorithm.Compute(sequence1, sequence2, timeout);
        }

        var lineAlignments = lineAlignmentResult.Diffs;
        var hitTimeout = lineAlignmentResult.HitTimeout;
        lineAlignments = HeuristicSequenceOptimizations.OptimizeSequenceDiffs(sequence1, sequence2, lineAlignments);
        lineAlignments = HeuristicSequenceOptimizations.RemoveVeryShortMatchingLinesBetweenDiffs(sequence1, sequence2, lineAlignments);

        var alignments = new List<RangeMapping>();

        var seq1LastStart = 0;
        var seq2LastStart = 0;

        void ScanForWhitespaceChanges(int equalLinesCount)
        {
            if (!considerWhitespaceChanges)
            {
                return;
            }

            for (var i = 0; i < equalLinesCount; i++)
            {
                var seq1Offset = seq1LastStart + i;
                var seq2Offset = seq2LastStart + i;
                if (!string.Equals(originalLines[seq1Offset], modifiedLines[seq2Offset], StringComparison.Ordinal))
                {
                    // This is because of whitespace changes, diff these lines
                    var characterDiffs = RefineDiff(originalLines, modifiedLines, new SequenceDiff(
                        new OffsetRange(seq1Offset, seq1Offset + 1),
                        new OffsetRange(seq2Offset, seq2Offset + 1)), timeout, considerWhitespaceChanges, options, rawChanges);
                    alignments.AddRange(characterDiffs.Mappings);
                    if (characterDiffs.HitTimeout)
                    {
                        hitTimeout = true;
                    }
                }
            }
        }

        foreach (var diff in lineAlignments)
        {
            Debug.Assert(diff.Seq1Range.Start - seq1LastStart == diff.Seq2Range.Start - seq2LastStart);

            var equalLinesCount = diff.Seq1Range.Start - seq1LastStart;

            ScanForWhitespaceChanges(equalLinesCount);

            seq1LastStart = diff.Seq1Range.EndExclusive;
            seq2LastStart = diff.Seq2Range.EndExclusive;

            var characterDiffs = RefineDiff(originalLines, modifiedLines, diff, timeout, considerWhitespaceChanges, options, rawChanges);
            if (characterDiffs.HitTimeout)
            {
                hitTimeout = true;
            }

            alignments.AddRange(characterDiffs.Mappings);
        }

        ScanForWhitespaceChanges(originalLines.Count - seq1LastStart);

        var original = new DiffTextLines(originalLines);
        var modified = new DiffTextLines(modifiedLines);

        var changes = RangeMappings.LineRangeMappingFromRangeMappings(alignments, original, modified);

        IReadOnlyList<MovedText> moves = [];
        if (options.ComputeMoves)
        {
            moves = ComputeMoves(changes, originalLines, modifiedLines, originalLinesHashes, modifiedLinesHashes, timeout, considerWhitespaceChanges, options);
        }

        // Make sure all ranges are valid
        Debug.Assert(ValidateChanges(changes, originalLines, modifiedLines));

        return new LinesDiff(changes, moves, hitTimeout);
    }

    private static bool ValidateChanges(List<DetailedLineRangeMapping> changes, IReadOnlyList<string> originalLines, IReadOnlyList<string> modifiedLines)
    {
        static bool ValidatePosition(TextPosition pos, IReadOnlyList<string> lines)
        {
            if (pos.LineNumber < 1 || pos.LineNumber > lines.Count)
            {
                return false;
            }

            var line = lines[pos.LineNumber - 1];
            return pos.Column >= 1 && pos.Column <= line.Length + 1;
        }

        static bool ValidateRange(LineRange range, IReadOnlyList<string> lines) =>
            range.StartLineNumber >= 1 && range.StartLineNumber <= lines.Count + 1
            && range.EndLineNumberExclusive >= 1 && range.EndLineNumberExclusive <= lines.Count + 1;

        foreach (var c in changes)
        {
            if (c.InnerChanges is null)
            {
                return false;
            }

            foreach (var ic in c.InnerChanges)
            {
                var valid = ValidatePosition(ic.ModifiedRange.Start, modifiedLines) && ValidatePosition(ic.ModifiedRange.End, modifiedLines)
                    && ValidatePosition(ic.OriginalRange.Start, originalLines) && ValidatePosition(ic.OriginalRange.End, originalLines);
                if (!valid)
                {
                    return false;
                }
            }

            if (!ValidateRange(c.Modified, modifiedLines) || !ValidateRange(c.Original, originalLines))
            {
                return false;
            }
        }

        return true;
    }

    private List<MovedText> ComputeMoves(
        List<DetailedLineRangeMapping> changes,
        IReadOnlyList<string> originalLines,
        IReadOnlyList<string> modifiedLines,
        int[] hashedOriginalLines,
        int[] hashedModifiedLines,
        ITimeout timeout,
        bool considerWhitespaceChanges,
        LinesDiffComputerOptions options)
    {
        var moves = ComputeMovedLines.Compute(changes, originalLines, modifiedLines, hashedOriginalLines, hashedModifiedLines, timeout);
        return moves.Select(m =>
        {
            var moveChanges = RefineDiff(originalLines, modifiedLines, new SequenceDiff(m.Original.ToOffsetRange(), m.Modified.ToOffsetRange()), timeout, considerWhitespaceChanges, options);
            var mappings = RangeMappings.LineRangeMappingFromRangeMappings(moveChanges.Mappings, new DiffTextLines(originalLines), new DiffTextLines(modifiedLines), true);
            return new MovedText(m, mappings);
        }).ToList();
    }

    private (List<RangeMapping> Mappings, bool HitTimeout) RefineDiff(
        IReadOnlyList<string> originalLines,
        IReadOnlyList<string> modifiedLines,
        SequenceDiff diff,
        ITimeout timeout,
        bool considerWhitespaceChanges,
        LinesDiffComputerOptions options,
        List<RangeMapping>? rawChanges = null)
    {
        var lineRangeMapping = ToLineRangeMapping(diff);
        var rangeMapping = lineRangeMapping.ToRangeMapping2(originalLines, modifiedLines);

        var slice1 = new LinesSliceCharSequence(originalLines, rangeMapping.OriginalRange, considerWhitespaceChanges);
        var slice2 = new LinesSliceCharSequence(modifiedLines, rangeMapping.ModifiedRange, considerWhitespaceChanges);

        var useDynamicProgramming = slice1.Length + slice2.Length < 500;
        var diffResult = useDynamicProgramming
            ? _dynamicProgrammingDiffing.Compute(slice1, slice2, timeout)
            : _myersDiffingAlgorithm.Compute(slice1, slice2, timeout);

        var hitTimeout = diffResult.HitTimeout;
        if (rawChanges is not null)
        {
            // The small-slice display algorithm favors consecutive matches over the longest common subsequence.
            var rawResult = useDynamicProgramming && !hitTimeout ? _myersDiffingAlgorithm.Compute(slice1, slice2, timeout) : diffResult;
            hitTimeout |= rawResult.HitTimeout;
            foreach (var change in rawResult.Diffs)
            {
                rawChanges.Add(new RangeMapping(slice1.TranslateRange(change.Seq1Range), slice2.TranslateRange(change.Seq2Range)));
            }
        }

        var diffs = diffResult.Diffs;
        diffs = HeuristicSequenceOptimizations.OptimizeSequenceDiffs(slice1, slice2, diffs);
        diffs = HeuristicSequenceOptimizations.ExtendDiffsToEntireWordIfAppropriate(slice1, slice2, diffs, (seq, idx) => seq.FindWordContaining(idx));

        if (options.ExtendToSubwords)
        {
            diffs = HeuristicSequenceOptimizations.ExtendDiffsToEntireWordIfAppropriate(slice1, slice2, diffs, (seq, idx) => seq.FindSubWordContaining(idx), true);
        }

        diffs = HeuristicSequenceOptimizations.RemoveShortMatches(slice1, slice2, diffs);
        diffs = HeuristicSequenceOptimizations.RemoveVeryShortMatchingTextBetweenLongDiffs(slice1, slice2, diffs);

        var result = diffs.Select(d => new RangeMapping(slice1.TranslateRange(d.Seq1Range), slice2.TranslateRange(d.Seq2Range))).ToList();
        return (result, hitTimeout);
    }

    private static LineRangeMapping ToLineRangeMapping(SequenceDiff sequenceDiff) =>
        new(
            new LineRange(sequenceDiff.Seq1Range.Start + 1, sequenceDiff.Seq1Range.EndExclusive + 1),
            new LineRange(sequenceDiff.Seq2Range.Start + 1, sequenceDiff.Seq2Range.EndExclusive + 1));
}
