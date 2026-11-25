// Ported from vscode (src/vs/editor/common/diff/defaultLinesDiffComputer/computeMovedLines.ts, commit b57d5c9e), MIT
// license.
// Differences: sorts are stable (as JavaScript's); upstream's SetMap of "h1:h2:h3" strings is a dictionary keyed by the
// three hashes; sets of changes compare by reference.

using TKCode.Arc.Editor.Diff.Algorithms;

namespace TKCode.Arc.Editor.Diff;

/// <summary>Finds blocks of lines that moved (upstream's computeMovedLines).</summary>
internal static class ComputeMovedLines
{
    public static List<LineRangeMapping> Compute(
        IReadOnlyList<DetailedLineRangeMapping> changes,
        IReadOnlyList<string> originalLines,
        IReadOnlyList<string> modifiedLines,
        int[] hashedOriginalLines,
        int[] hashedModifiedLines,
        ITimeout timeout)
    {
        var (moves, excludedChanges) = ComputeMovesFromSimpleDeletionsToSimpleInsertions(changes, originalLines, modifiedLines, timeout);

        if (!timeout.IsValid())
        {
            return [];
        }

        var filteredChanges = changes.Where(c => !excludedChanges.Contains(c)).ToList();
        var unchangedMoves = ComputeUnchangedMoves(filteredChanges, hashedOriginalLines, hashedModifiedLines, originalLines, modifiedLines, timeout);
        moves.AddRange(unchangedMoves);

        moves = JoinCloseConsecutiveMoves(moves);
        // Ignore too short moves
        moves = moves.Where(current =>
        {
            var lines = current.Original.ToOffsetRange().Slice(originalLines).Select(DiffUtils.JsTrim).ToArray();
            var originalText = string.Join('\n', lines);
            return originalText.Length >= 15 && lines.Count(l => l.Length >= 2) >= 2;
        }).ToList();
        moves = RemoveMovesInSameDiff(changes, moves);

        return moves;
    }

    private static (List<LineRangeMapping> Moves, HashSet<DetailedLineRangeMapping> ExcludedChanges) ComputeMovesFromSimpleDeletionsToSimpleInsertions(
        IReadOnlyList<DetailedLineRangeMapping> changes,
        IReadOnlyList<string> originalLines,
        IReadOnlyList<string> modifiedLines,
        ITimeout timeout)
    {
        var moves = new List<LineRangeMapping>();

        var deletions = changes
            .Where(c => c.Modified.IsEmpty && c.Original.Length >= 3)
            .Select(d => new LineRangeFragment(d.Original, originalLines, d))
            .ToList();
        // a set that keeps insertion order, as JavaScript's Set does
        var insertions = changes
            .Where(c => c.Original.IsEmpty && c.Modified.Length >= 3)
            .Select(d => new LineRangeFragment(d.Modified, modifiedLines, d))
            .ToList();

        var excludedChanges = new HashSet<DetailedLineRangeMapping>(ReferenceEqualityComparer.Instance);

        foreach (var deletion in deletions)
        {
            var highestSimilarity = -1.0;
            LineRangeFragment? best = null;
            foreach (var insertion in insertions)
            {
                var similarity = deletion.ComputeSimilarity(insertion);
                if (similarity > highestSimilarity)
                {
                    highestSimilarity = similarity;
                    best = insertion;
                }
            }

            if (highestSimilarity > 0.90 && best is not null)
            {
                insertions.Remove(best);
                moves.Add(new LineRangeMapping(deletion.Range, best.Range));
                excludedChanges.Add(deletion.Source);
                excludedChanges.Add(best.Source);
            }

            if (!timeout.IsValid())
            {
                return (moves, excludedChanges);
            }
        }

        return (moves, excludedChanges);
    }

    private sealed class PossibleMapping(LineRange modifiedLineRange, LineRange originalLineRange)
    {
        public LineRange ModifiedLineRange { get; set; } = modifiedLineRange;

        public LineRange OriginalLineRange { get; set; } = originalLineRange;
    }

    private static List<LineRangeMapping> ComputeUnchangedMoves(
        List<DetailedLineRangeMapping> changes,
        int[] hashedOriginalLines,
        int[] hashedModifiedLines,
        IReadOnlyList<string> originalLines,
        IReadOnlyList<string> modifiedLines,
        ITimeout timeout)
    {
        var moves = new List<LineRangeMapping>();

        var original3LineHashes = new Dictionary<(int, int, int), List<LineRange>>();

        foreach (var change in changes)
        {
            for (var i = change.Original.StartLineNumber; i < change.Original.EndLineNumberExclusive - 2; i++)
            {
                var key = (hashedOriginalLines[i - 1], hashedOriginalLines[i + 1 - 1], hashedOriginalLines[i + 2 - 1]);
                if (!original3LineHashes.TryGetValue(key, out var list))
                {
                    list = [];
                    original3LineHashes[key] = list;
                }

                // upstream's SetMap holds a new { range } object per line, so nothing is ever deduplicated
                list.Add(new LineRange(i, i + 3));
            }
        }

        var possibleMappings = new List<PossibleMapping>();

        DiffArrays.StableSort(changes, (a, b) => a.Modified.StartLineNumber.CompareTo(b.Modified.StartLineNumber));

        foreach (var change in changes)
        {
            var lastMappings = new List<PossibleMapping>();
            for (var i = change.Modified.StartLineNumber; i < change.Modified.EndLineNumberExclusive - 2; i++)
            {
                var key = (hashedModifiedLines[i - 1], hashedModifiedLines[i + 1 - 1], hashedModifiedLines[i + 2 - 1]);
                var currentModifiedRange = new LineRange(i, i + 3);

                var nextMappings = new List<PossibleMapping>();
                if (original3LineHashes.TryGetValue(key, out var ranges))
                {
                    foreach (var range in ranges)
                    {
                        var extended = false;
                        foreach (var lastMapping in lastMappings)
                        {
                            // does this match extend some last match?
                            if (lastMapping.OriginalLineRange.EndLineNumberExclusive + 1 == range.EndLineNumberExclusive
                                && lastMapping.ModifiedLineRange.EndLineNumberExclusive + 1 == currentModifiedRange.EndLineNumberExclusive)
                            {
                                lastMapping.OriginalLineRange = new LineRange(lastMapping.OriginalLineRange.StartLineNumber, range.EndLineNumberExclusive);
                                lastMapping.ModifiedLineRange = new LineRange(lastMapping.ModifiedLineRange.StartLineNumber, currentModifiedRange.EndLineNumberExclusive);
                                nextMappings.Add(lastMapping);
                                extended = true;
                                break;
                            }
                        }

                        if (extended)
                        {
                            continue;
                        }

                        var mapping = new PossibleMapping(currentModifiedRange, range);
                        possibleMappings.Add(mapping);
                        nextMappings.Add(mapping);
                    }
                }

                lastMappings = nextMappings;
            }

            if (!timeout.IsValid())
            {
                return [];
            }
        }

        // longest first (reverseOrder(compareBy(length)))
        DiffArrays.StableSort(possibleMappings, (a, b) => b.ModifiedLineRange.Length.CompareTo(a.ModifiedLineRange.Length));

        var modifiedSet = new LineRangeSet();
        var originalSet = new LineRangeSet();

        foreach (var mapping in possibleMappings)
        {
            var diffOrigToMod = mapping.ModifiedLineRange.StartLineNumber - mapping.OriginalLineRange.StartLineNumber;
            var modifiedSections = modifiedSet.SubtractFrom(mapping.ModifiedLineRange);
            var originalTranslatedSections = originalSet.SubtractFrom(mapping.OriginalLineRange).GetWithDelta(diffOrigToMod);

            var modifiedIntersectedSections = modifiedSections.GetIntersection(originalTranslatedSections);

            foreach (var s in modifiedIntersectedSections.Ranges)
            {
                if (s.Length < 3)
                {
                    continue;
                }

                var modifiedLineRange = s;
                var originalLineRange = s.Delta(-diffOrigToMod);

                moves.Add(new LineRangeMapping(originalLineRange, modifiedLineRange));

                modifiedSet.AddRange(modifiedLineRange);
                originalSet.AddRange(originalLineRange);
            }
        }

        DiffArrays.StableSort(moves, (a, b) => a.Original.StartLineNumber.CompareTo(b.Original.StartLineNumber));

        var monotonousChanges = new MonotonousArray<DetailedLineRangeMapping>(changes);
        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var firstTouchingChangeOrig = monotonousChanges.FindLastMonotonous(c => c.Original.StartLineNumber <= move.Original.StartLineNumber)!;
            var firstTouchingChangeMod = DiffArrays.FindLastMonotonous(changes, c => c.Modified.StartLineNumber <= move.Modified.StartLineNumber)!;
            var linesAbove = Math.Max(
                move.Original.StartLineNumber - firstTouchingChangeOrig.Original.StartLineNumber,
                move.Modified.StartLineNumber - firstTouchingChangeMod.Modified.StartLineNumber);

            var lastTouchingChangeOrig = monotonousChanges.FindLastMonotonous(c => c.Original.StartLineNumber < move.Original.EndLineNumberExclusive)!;
            var lastTouchingChangeMod = DiffArrays.FindLastMonotonous(changes, c => c.Modified.StartLineNumber < move.Modified.EndLineNumberExclusive)!;
            var linesBelow = Math.Max(
                lastTouchingChangeOrig.Original.EndLineNumberExclusive - move.Original.EndLineNumberExclusive,
                lastTouchingChangeMod.Modified.EndLineNumberExclusive - move.Modified.EndLineNumberExclusive);

            int extendToTop;
            for (extendToTop = 0; extendToTop < linesAbove; extendToTop++)
            {
                var origLine = move.Original.StartLineNumber - extendToTop - 1;
                var modLine = move.Modified.StartLineNumber - extendToTop - 1;
                if (origLine < 1 || modLine < 1)
                {
                    break;
                }

                if (modifiedSet.Contains(modLine) || originalSet.Contains(origLine))
                {
                    break;
                }

                if (!AreLinesSimilar(originalLines[origLine - 1], modifiedLines[modLine - 1], timeout))
                {
                    break;
                }
            }

            if (extendToTop > 0)
            {
                originalSet.AddRange(new LineRange(move.Original.StartLineNumber - extendToTop, move.Original.StartLineNumber));
                modifiedSet.AddRange(new LineRange(move.Modified.StartLineNumber - extendToTop, move.Modified.StartLineNumber));
            }

            int extendToBottom;
            for (extendToBottom = 0; extendToBottom < linesBelow; extendToBottom++)
            {
                var origLine = move.Original.EndLineNumberExclusive + extendToBottom;
                var modLine = move.Modified.EndLineNumberExclusive + extendToBottom;
                if (origLine > originalLines.Count || modLine > modifiedLines.Count)
                {
                    break;
                }

                if (modifiedSet.Contains(modLine) || originalSet.Contains(origLine))
                {
                    break;
                }

                if (!AreLinesSimilar(originalLines[origLine - 1], modifiedLines[modLine - 1], timeout))
                {
                    break;
                }
            }

            if (extendToBottom > 0)
            {
                originalSet.AddRange(new LineRange(move.Original.EndLineNumberExclusive, move.Original.EndLineNumberExclusive + extendToBottom));
                modifiedSet.AddRange(new LineRange(move.Modified.EndLineNumberExclusive, move.Modified.EndLineNumberExclusive + extendToBottom));
            }

            if (extendToTop > 0 || extendToBottom > 0)
            {
                moves[i] = new LineRangeMapping(
                    new LineRange(move.Original.StartLineNumber - extendToTop, move.Original.EndLineNumberExclusive + extendToBottom),
                    new LineRange(move.Modified.StartLineNumber - extendToTop, move.Modified.EndLineNumberExclusive + extendToBottom));
            }
        }

        return moves;
    }

    private static bool AreLinesSimilar(string line1, string line2, ITimeout timeout)
    {
        if (DiffUtils.JsTrim(line1) == DiffUtils.JsTrim(line2))
        {
            return true;
        }

        if (line1.Length > 300 && line2.Length > 300)
        {
            return false;
        }

        var myersDiffingAlgorithm = new MyersDiffAlgorithm();
        var result = myersDiffingAlgorithm.Compute(
            new LinesSliceCharSequence([line1], new TextRange(1, 1, 1, line1.Length), false),
            new LinesSliceCharSequence([line2], new TextRange(1, 1, 1, line2.Length), false),
            timeout);
        var commonNonSpaceCharCount = 0;
        var inverted = SequenceDiff.Invert(result.Diffs, line1.Length);
        foreach (var seq in inverted)
        {
            for (var idx = seq.Seq1Range.Start; idx < seq.Seq1Range.EndExclusive; idx++)
            {
                // charCodeAt past the end is NaN upstream, which is not a space
                if (idx >= line1.Length || !DiffUtils.IsSpace(line1[idx]))
                {
                    commonNonSpaceCharCount++;
                }
            }
        }

        int CountNonWsChars(string str)
        {
            var count = 0;
            // upstream loops over line1.length (sic)
            for (var i = 0; i < line1.Length; i++)
            {
                if (i >= str.Length || !DiffUtils.IsSpace(str[i]))
                {
                    count++;
                }
            }

            return count;
        }

        var longerLineLength = CountNonWsChars(line1.Length > line2.Length ? line1 : line2);
        return (double)commonNonSpaceCharCount / longerLineLength > 0.6 && longerLineLength > 10;
    }

    private static List<LineRangeMapping> JoinCloseConsecutiveMoves(List<LineRangeMapping> moves)
    {
        if (moves.Count == 0)
        {
            return moves;
        }

        DiffArrays.StableSort(moves, (a, b) => a.Original.StartLineNumber.CompareTo(b.Original.StartLineNumber));

        var result = new List<LineRangeMapping> { moves[0] };
        for (var i = 1; i < moves.Count; i++)
        {
            var last = result[^1];
            var current = moves[i];

            var originalDist = current.Original.StartLineNumber - last.Original.EndLineNumberExclusive;
            var modifiedDist = current.Modified.StartLineNumber - last.Modified.EndLineNumberExclusive;
            var currentMoveAfterLast = originalDist >= 0 && modifiedDist >= 0;

            if (currentMoveAfterLast && originalDist + modifiedDist <= 2)
            {
                result[^1] = last.Join(current);
                continue;
            }

            result.Add(current);
        }

        return result;
    }

    private static List<LineRangeMapping> RemoveMovesInSameDiff(IReadOnlyList<DetailedLineRangeMapping> changes, List<LineRangeMapping> moves)
    {
        var changesMonotonous = new MonotonousArray<DetailedLineRangeMapping>(changes);
        return moves.Where(m =>
        {
            LineRangeMapping diffBeforeEndOfMoveOriginal = changesMonotonous.FindLastMonotonous(c => c.Original.StartLineNumber < m.Original.EndLineNumberExclusive)
                ?? new LineRangeMapping(new LineRange(1, 1), new LineRange(1, 1));
            var diffBeforeEndOfMoveModified = DiffArrays.FindLastMonotonous(changes, c => c.Modified.StartLineNumber < m.Modified.EndLineNumberExclusive);

            return !ReferenceEquals(diffBeforeEndOfMoveOriginal, diffBeforeEndOfMoveModified);
        }).ToList();
    }
}
