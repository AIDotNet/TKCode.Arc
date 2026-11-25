// Ported from vscode (src/vs/editor/common/diff/defaultLinesDiffComputer/heuristicSequenceOptimizations.ts, commit
// b57d5c9e), MIT license.
// Differences: lists instead of arrays (shift() is a moving index); "\s" and trim() follow JavaScript's whitespace.

using TKCode.Arc.Editor.Diff.Algorithms;

namespace TKCode.Arc.Editor.Diff;

/// <summary>Heuristics that make a computed diff read better.</summary>
internal static class HeuristicSequenceOptimizations
{
    public static List<SequenceDiff> OptimizeSequenceDiffs(ISequence sequence1, ISequence sequence2, List<SequenceDiff> sequenceDiffs)
    {
        var result = sequenceDiffs;
        result = JoinSequenceDiffsByShifting(sequence1, sequence2, result);
        // Sometimes, calling this function twice improves the result.
        result = JoinSequenceDiffsByShifting(sequence1, sequence2, result);
        result = ShiftSequenceDiffs(sequence1, sequence2, result);
        return result;
    }

    /// <summary>
    /// Fixes diffs like <c>import { Baz, Bar } from "foo";</c> → <c>import { Baz, Bar, Foo } from "foo";</c>: computed
    /// [{Add "," after Bar}, {Add "Foo " after space}], improved [{Add ", Foo" after Bar}].
    /// </summary>
    private static List<SequenceDiff> JoinSequenceDiffsByShifting(ISequence sequence1, ISequence sequence2, List<SequenceDiff> sequenceDiffs)
    {
        if (sequenceDiffs.Count == 0)
        {
            return sequenceDiffs;
        }

        var result = new List<SequenceDiff> { sequenceDiffs[0] };

        // First move them all to the left as much as possible and join them if possible
        for (var i = 1; i < sequenceDiffs.Count; i++)
        {
            var prevResult = result[^1];
            var cur = sequenceDiffs[i];

            if (cur.Seq1Range.IsEmpty || cur.Seq2Range.IsEmpty)
            {
                var length = cur.Seq1Range.Start - prevResult.Seq1Range.EndExclusive;
                int d;
                for (d = 1; d <= length; d++)
                {
                    if (sequence1.GetElement(cur.Seq1Range.Start - d) != sequence1.GetElement(cur.Seq1Range.EndExclusive - d)
                        || sequence2.GetElement(cur.Seq2Range.Start - d) != sequence2.GetElement(cur.Seq2Range.EndExclusive - d))
                    {
                        break;
                    }
                }

                d--;

                if (d == length)
                {
                    // Merge previous and current diff
                    result[^1] = new SequenceDiff(
                        new OffsetRange(prevResult.Seq1Range.Start, cur.Seq1Range.EndExclusive - length),
                        new OffsetRange(prevResult.Seq2Range.Start, cur.Seq2Range.EndExclusive - length));
                    continue;
                }

                cur = cur.Delta(-d);
            }

            result.Add(cur);
        }

        var result2 = new List<SequenceDiff>();
        // Then move them all to the right and join them again if possible
        for (var i = 0; i < result.Count - 1; i++)
        {
            var nextResult = result[i + 1];
            var cur = result[i];

            if (cur.Seq1Range.IsEmpty || cur.Seq2Range.IsEmpty)
            {
                var length = nextResult.Seq1Range.Start - cur.Seq1Range.EndExclusive;
                int d;
                for (d = 0; d < length; d++)
                {
                    if (!sequence1.IsStronglyEqual(cur.Seq1Range.Start + d, cur.Seq1Range.EndExclusive + d)
                        || !sequence2.IsStronglyEqual(cur.Seq2Range.Start + d, cur.Seq2Range.EndExclusive + d))
                    {
                        break;
                    }
                }

                if (d == length)
                {
                    // Merge previous and current diff, write to result!
                    result[i + 1] = new SequenceDiff(
                        new OffsetRange(cur.Seq1Range.Start + length, nextResult.Seq1Range.EndExclusive),
                        new OffsetRange(cur.Seq2Range.Start + length, nextResult.Seq2Range.EndExclusive));
                    continue;
                }

                if (d > 0)
                {
                    cur = cur.Delta(d);
                }
            }

            result2.Add(cur);
        }

        if (result.Count > 0)
        {
            result2.Add(result[^1]);
        }

        return result2;
    }

    // align character level diffs at whitespace characters
    // import { IBar } from "foo";
    // import { I[Arr, I]Bar } from "foo";
    // ->
    // import { [IArr, ]IBar } from "foo";
    private static List<SequenceDiff> ShiftSequenceDiffs(ISequence sequence1, ISequence sequence2, List<SequenceDiff> sequenceDiffs)
    {
        if (!sequence1.HasBoundaryScore || !sequence2.HasBoundaryScore)
        {
            return sequenceDiffs;
        }

        for (var i = 0; i < sequenceDiffs.Count; i++)
        {
            var prevDiff = i > 0 ? sequenceDiffs[i - 1] : null;
            var diff = sequenceDiffs[i];
            var nextDiff = i + 1 < sequenceDiffs.Count ? sequenceDiffs[i + 1] : null;

            var seq1ValidRange = new OffsetRange(prevDiff is not null ? prevDiff.Seq1Range.EndExclusive + 1 : 0, nextDiff is not null ? nextDiff.Seq1Range.Start - 1 : sequence1.Length);
            var seq2ValidRange = new OffsetRange(prevDiff is not null ? prevDiff.Seq2Range.EndExclusive + 1 : 0, nextDiff is not null ? nextDiff.Seq2Range.Start - 1 : sequence2.Length);

            if (diff.Seq1Range.IsEmpty)
            {
                sequenceDiffs[i] = ShiftDiffToBetterPosition(diff, sequence1, sequence2, seq1ValidRange, seq2ValidRange);
            }
            else if (diff.Seq2Range.IsEmpty)
            {
                sequenceDiffs[i] = ShiftDiffToBetterPosition(diff.Swap(), sequence2, sequence1, seq2ValidRange, seq1ValidRange).Swap();
            }
        }

        return sequenceDiffs;
    }

    private static SequenceDiff ShiftDiffToBetterPosition(SequenceDiff diff, ISequence sequence1, ISequence sequence2, OffsetRange seq1ValidRange, OffsetRange seq2ValidRange)
    {
        const int maxShiftLimit = 100; // To prevent performance issues

        // don't touch previous or next!
        var deltaBefore = 1;
        while (diff.Seq1Range.Start - deltaBefore >= seq1ValidRange.Start
               && diff.Seq2Range.Start - deltaBefore >= seq2ValidRange.Start
               && sequence2.IsStronglyEqual(diff.Seq2Range.Start - deltaBefore, diff.Seq2Range.EndExclusive - deltaBefore)
               && deltaBefore < maxShiftLimit)
        {
            deltaBefore++;
        }

        deltaBefore--;

        var deltaAfter = 0;
        while (diff.Seq1Range.Start + deltaAfter < seq1ValidRange.EndExclusive
               && diff.Seq2Range.EndExclusive + deltaAfter < seq2ValidRange.EndExclusive
               && sequence2.IsStronglyEqual(diff.Seq2Range.Start + deltaAfter, diff.Seq2Range.EndExclusive + deltaAfter)
               && deltaAfter < maxShiftLimit)
        {
            deltaAfter++;
        }

        if (deltaBefore == 0 && deltaAfter == 0)
        {
            return diff;
        }

        // Visualize `[sequence1.text, diff.seq1Range.start + deltaAfter]`
        // and `[sequence2.text, diff.seq2Range.start + deltaAfter, diff.seq2Range.endExclusive + deltaAfter]`
        var bestDelta = 0;
        var bestScore = -1;
        // find best scored delta
        for (var delta = -deltaBefore; delta <= deltaAfter; delta++)
        {
            var seq2OffsetStart = diff.Seq2Range.Start + delta;
            var seq2OffsetEndExclusive = diff.Seq2Range.EndExclusive + delta;
            var seq1Offset = diff.Seq1Range.Start + delta;

            var score = sequence1.GetBoundaryScore(seq1Offset) + sequence2.GetBoundaryScore(seq2OffsetStart) + sequence2.GetBoundaryScore(seq2OffsetEndExclusive);
            if (score > bestScore)
            {
                bestScore = score;
                bestDelta = delta;
            }
        }

        return diff.Delta(bestDelta);
    }

    public static List<SequenceDiff> RemoveShortMatches(ISequence sequence1, ISequence sequence2, List<SequenceDiff> sequenceDiffs)
    {
        var result = new List<SequenceDiff>();
        foreach (var s in sequenceDiffs)
        {
            if (result.Count == 0)
            {
                result.Add(s);
                continue;
            }

            var last = result[^1];
            if (s.Seq1Range.Start - last.Seq1Range.EndExclusive <= 2 || s.Seq2Range.Start - last.Seq2Range.EndExclusive <= 2)
            {
                result[^1] = new SequenceDiff(last.Seq1Range.Join(s.Seq1Range), last.Seq2Range.Join(s.Seq2Range));
            }
            else
            {
                result.Add(s);
            }
        }

        return result;
    }

    public static List<SequenceDiff> ExtendDiffsToEntireWordIfAppropriate(
        LinesSliceCharSequence sequence1,
        LinesSliceCharSequence sequence2,
        List<SequenceDiff> sequenceDiffs,
        Func<LinesSliceCharSequence, int, OffsetRange?> findParent,
        bool force = false)
    {
        var equalMappings = SequenceDiff.Invert(sequenceDiffs, sequence1.Length);
        var equalIndex = 0; // equalMappings.shift() moves this index

        var additional = new List<SequenceDiff>();

        var lastPoint = new OffsetPair(0, 0);

        void ScanWord(OffsetPair pair, SequenceDiff equalMapping)
        {
            if (pair.Offset1 < lastPoint.Offset1 || pair.Offset2 < lastPoint.Offset2)
            {
                return;
            }

            var w1 = findParent(sequence1, pair.Offset1);
            var w2 = findParent(sequence2, pair.Offset2);
            if (w1 is null || w2 is null)
            {
                return;
            }

            var w = new SequenceDiff(w1.Value, w2.Value);
            var equalPart = w.Intersect(equalMapping)!;

            var equalChars1 = equalPart.Seq1Range.Length;
            var equalChars2 = equalPart.Seq2Range.Length;

            // The words do not touch previous equals mappings, as we would have processed them already.
            // But they might touch the next ones.
            while (equalIndex < equalMappings.Count)
            {
                var next = equalMappings[equalIndex];
                var intersects = next.Seq1Range.Intersects(w.Seq1Range) || next.Seq2Range.Intersects(w.Seq2Range);
                if (!intersects)
                {
                    break;
                }

                var v1 = findParent(sequence1, next.Seq1Range.Start);
                var v2 = findParent(sequence2, next.Seq2Range.Start);
                // Because there is an intersection, we know that the words are not empty.
                var v = new SequenceDiff(v1!.Value, v2!.Value);
                var nextEqualPart = v.Intersect(next)!;

                equalChars1 += nextEqualPart.Seq1Range.Length;
                equalChars2 += nextEqualPart.Seq2Range.Length;

                w = w.Join(v);

                if (w.Seq1Range.EndExclusive >= next.Seq1Range.EndExclusive)
                {
                    // The word extends beyond the next equal mapping.
                    equalIndex++;
                }
                else
                {
                    break;
                }
            }

            if ((force && equalChars1 + equalChars2 < w.Seq1Range.Length + w.Seq2Range.Length)
                || equalChars1 + equalChars2 < (w.Seq1Range.Length + w.Seq2Range.Length) * 2 / 3.0)
            {
                additional.Add(w);
            }

            lastPoint = w.GetEndExclusives();
        }

        while (equalIndex < equalMappings.Count)
        {
            var next = equalMappings[equalIndex++];
            if (next.Seq1Range.IsEmpty)
            {
                continue;
            }

            ScanWord(next.GetStarts(), next);
            // The equal parts are not empty, so -1 gives us a character that is equal in both parts.
            ScanWord(next.GetEndExclusives().Delta(-1), next);
        }

        return MergeSequenceDiffs(sequenceDiffs, additional);
    }

    private static List<SequenceDiff> MergeSequenceDiffs(List<SequenceDiff> sequenceDiffs1, List<SequenceDiff> sequenceDiffs2)
    {
        var result = new List<SequenceDiff>();
        var i1 = 0;
        var i2 = 0;

        while (i1 < sequenceDiffs1.Count || i2 < sequenceDiffs2.Count)
        {
            var sd1 = i1 < sequenceDiffs1.Count ? sequenceDiffs1[i1] : null;
            var sd2 = i2 < sequenceDiffs2.Count ? sequenceDiffs2[i2] : null;

            SequenceDiff next;
            if (sd1 is not null && (sd2 is null || sd1.Seq1Range.Start < sd2.Seq1Range.Start))
            {
                next = sd1;
                i1++;
            }
            else
            {
                next = sd2!;
                i2++;
            }

            if (result.Count > 0 && result[^1].Seq1Range.EndExclusive >= next.Seq1Range.Start)
            {
                result[^1] = result[^1].Join(next);
            }
            else
            {
                result.Add(next);
            }
        }

        return result;
    }

    public static List<SequenceDiff> RemoveVeryShortMatchingLinesBetweenDiffs(LineSequence sequence1, LineSequence sequence2, List<SequenceDiff> sequenceDiffs)
    {
        var diffs = sequenceDiffs;
        if (diffs.Count == 0)
        {
            return diffs;
        }

        var counter = 0;
        bool shouldRepeat;
        do
        {
            shouldRepeat = false;

            var result = new List<SequenceDiff> { diffs[0] };

            for (var i = 1; i < diffs.Count; i++)
            {
                var cur = diffs[i];
                var lastResult = result[^1];

                bool ShouldJoinDiffs(SequenceDiff before, SequenceDiff after)
                {
                    var unchangedRange = new OffsetRange(lastResult.Seq1Range.EndExclusive, cur.Seq1Range.Start);

                    var unchangedText = sequence1.GetText(unchangedRange);
                    var unchangedTextWithoutWsLength = unchangedText.Count(c => !DiffUtils.IsJsWhitespace(c));
                    return unchangedTextWithoutWsLength <= 4
                           && (before.Seq1Range.Length + before.Seq2Range.Length > 5 || after.Seq1Range.Length + after.Seq2Range.Length > 5);
                }

                if (ShouldJoinDiffs(lastResult, cur))
                {
                    shouldRepeat = true;
                    result[^1] = result[^1].Join(cur);
                }
                else
                {
                    result.Add(cur);
                }
            }

            diffs = result;
        }
        while (counter++ < 10 && shouldRepeat);

        return diffs;
    }

    public static List<SequenceDiff> RemoveVeryShortMatchingTextBetweenLongDiffs(LinesSliceCharSequence sequence1, LinesSliceCharSequence sequence2, List<SequenceDiff> sequenceDiffs)
    {
        var diffs = sequenceDiffs;
        if (diffs.Count == 0)
        {
            return diffs;
        }

        var counter = 0;
        bool shouldRepeat;
        do
        {
            shouldRepeat = false;

            var result = new List<SequenceDiff> { diffs[0] };

            for (var i = 1; i < diffs.Count; i++)
            {
                var cur = diffs[i];
                var lastResult = result[^1];

                bool ShouldJoinDiffs(SequenceDiff before, SequenceDiff after)
                {
                    var unchangedRange = new OffsetRange(lastResult.Seq1Range.EndExclusive, cur.Seq1Range.Start);

                    var unchangedLineCount = sequence1.CountLinesIn(unchangedRange);
                    if (unchangedLineCount > 5 || unchangedRange.Length > 500)
                    {
                        return false;
                    }

                    var unchangedText = DiffUtils.JsTrim(sequence1.GetText(unchangedRange));
                    if (unchangedText.Length > 20 || unchangedText.Contains('\n') || unchangedText.Contains('\r'))
                    {
                        return false;
                    }

                    var beforeLineCount1 = sequence1.CountLinesIn(before.Seq1Range);
                    var beforeSeq1Length = before.Seq1Range.Length;
                    var beforeLineCount2 = sequence2.CountLinesIn(before.Seq2Range);
                    var beforeSeq2Length = before.Seq2Range.Length;

                    var afterLineCount1 = sequence1.CountLinesIn(after.Seq1Range);
                    var afterSeq1Length = after.Seq1Range.Length;
                    var afterLineCount2 = sequence2.CountLinesIn(after.Seq2Range);
                    var afterSeq2Length = after.Seq2Range.Length;

                    const double max = 2 * 40 + 50;
                    static double Cap(double v) => Math.Min(v, max);

                    return Math.Pow(Math.Pow(Cap(beforeLineCount1 * 40 + beforeSeq1Length), 1.5) + Math.Pow(Cap(beforeLineCount2 * 40 + beforeSeq2Length), 1.5), 1.5)
                           + Math.Pow(Math.Pow(Cap(afterLineCount1 * 40 + afterSeq1Length), 1.5) + Math.Pow(Cap(afterLineCount2 * 40 + afterSeq2Length), 1.5), 1.5)
                           > Math.Pow(Math.Pow(max, 1.5), 1.5) * 1.3;
                }

                if (ShouldJoinDiffs(lastResult, cur))
                {
                    shouldRepeat = true;
                    result[^1] = result[^1].Join(cur);
                }
                else
                {
                    result.Add(cur);
                }
            }

            diffs = result;
        }
        while (counter++ < 10 && shouldRepeat);

        var newDiffs = new List<SequenceDiff>();

        // Remove short suffixes/prefixes
        for (var i = 0; i < diffs.Count; i++)
        {
            var prev = i == 0 ? null : diffs[i - 1];
            var cur = diffs[i];
            var next = i + 1 == diffs.Count ? null : diffs[i + 1];

            var newDiff = cur;

            bool ShouldMarkAsChanged(string text) =>
                text.Length > 0 && DiffUtils.JsTrim(text).Length <= 3 && cur.Seq1Range.Length + cur.Seq2Range.Length > 100;

            var fullRange1 = sequence1.ExtendToFullLines(cur.Seq1Range);
            var prefix = sequence1.GetText(new OffsetRange(fullRange1.Start, cur.Seq1Range.Start));
            if (ShouldMarkAsChanged(prefix))
            {
                newDiff = newDiff.DeltaStart(-prefix.Length);
            }

            var suffix = sequence1.GetText(new OffsetRange(cur.Seq1Range.EndExclusive, fullRange1.EndExclusive));
            if (ShouldMarkAsChanged(suffix))
            {
                newDiff = newDiff.DeltaEnd(suffix.Length);
            }

            var availableSpace = SequenceDiff.FromOffsetPairs(
                prev is not null ? prev.GetEndExclusives() : OffsetPair.Zero,
                next is not null ? next.GetStarts() : OffsetPair.Max);
            var result = newDiff.Intersect(availableSpace)!;
            if (newDiffs.Count > 0 && result.GetStarts().Equals(newDiffs[^1].GetEndExclusives()))
            {
                newDiffs[^1] = newDiffs[^1].Join(result);
            }
            else
            {
                newDiffs.Add(result);
            }
        }

        return newDiffs;
    }
}
