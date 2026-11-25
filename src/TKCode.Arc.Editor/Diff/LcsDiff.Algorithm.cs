// Ported from vscode (src/vs/base/common/diff/diff.ts: LcsDiff, commit b57d5c9e), MIT license.
// An implementation of the difference algorithm described in "An O(ND) Difference Algorithm and its variations" by
// Eugene W. Myers, with the linear space refinement and a bounded history for the trace. Differences: see LcsDiff.cs.

namespace TKCode.Arc.Editor.Diff;

/// <summary>The longest-common-subsequence diff of two sequences (upstream's LcsDiff).</summary>
internal sealed class LcsDiff
{
    // MaxDifferencesHistory: the size of the forward and reverse history arrays kept for the trace
    private const int MaxDifferencesHistory = 1447;

    private readonly ContinueProcessingPredicate? _continueProcessingPredicate;
    private readonly IDiffSequence _originalSequence;
    private readonly IDiffSequence _modifiedSequence;
    private readonly bool _hasStrings;
    private readonly string[] _originalStringElements;
    private readonly int[] _originalElementsOrHash;
    private readonly string[] _modifiedStringElements;
    private readonly int[] _modifiedElementsOrHash;
    private List<int[]> _forwardHistory = [];
    private List<int[]> _reverseHistory = [];

    public LcsDiff(IDiffSequence originalSequence, IDiffSequence modifiedSequence, ContinueProcessingPredicate? continueProcessingPredicate = null)
    {
        _continueProcessingPredicate = continueProcessingPredicate;
        _originalSequence = originalSequence;
        _modifiedSequence = modifiedSequence;
        var (originalStringElements, originalElementsOrHash, originalHasStrings) = GetElements(originalSequence);
        var (modifiedStringElements, modifiedElementsOrHash, modifiedHasStrings) = GetElements(modifiedSequence);
        _hasStrings = originalHasStrings && modifiedHasStrings;
        _originalStringElements = originalStringElements;
        _originalElementsOrHash = originalElementsOrHash;
        _modifiedStringElements = modifiedStringElements;
        _modifiedElementsOrHash = modifiedElementsOrHash;
    }

    private static (string[] Strings, int[] ElementsOrHash, bool HasStrings) GetElements(IDiffSequence sequence)
    {
        if (sequence.GetStringElements() is { Length: > 0 } strings)
        {
            var hashes = new int[strings.Length];
            for (var i = 0; i < strings.Length; i++)
            {
                hashes[i] = StringHash(strings[i]);
            }

            return (strings, hashes, true);
        }

        return ([], sequence.GetIntElements() ?? [], false);
    }

    // upstream's stringHash(s, 0) of hash.ts: ((hash << 5) - hash + charCode) | 0 per character, after a numberHash of 149417
    private static int StringHash(string s)
    {
        var hash = unchecked((((0 << 5) - 0) + 149417) | 0);
        foreach (var ch in s)
        {
            hash = unchecked(((hash << 5) - hash) + ch);
        }

        return hash;
    }

    private bool ElementsAreEqual(int originalIndex, int newIndex)
    {
        if (_originalElementsOrHash[originalIndex] != _modifiedElementsOrHash[newIndex])
        {
            return false;
        }

        return !_hasStrings || _originalStringElements[originalIndex] == _modifiedStringElements[newIndex];
    }

    private bool ElementsAreStrictEqual(int originalIndex, int newIndex)
    {
        if (!ElementsAreEqual(originalIndex, newIndex))
        {
            return false;
        }

        var originalElement = _originalSequence.GetStrictElement(originalIndex);
        var modifiedElement = _modifiedSequence.GetStrictElement(newIndex);
        return originalElement == modifiedElement;
    }

    private bool OriginalElementsAreEqual(int index1, int index2)
    {
        if (_originalElementsOrHash[index1] != _originalElementsOrHash[index2])
        {
            return false;
        }

        return !_hasStrings || _originalStringElements[index1] == _originalStringElements[index2];
    }

    private bool ModifiedElementsAreEqual(int index1, int index2)
    {
        if (_modifiedElementsOrHash[index1] != _modifiedElementsOrHash[index2])
        {
            return false;
        }

        return !_hasStrings || _modifiedStringElements[index1] == _modifiedStringElements[index2];
    }

    /// <summary>The differences between the two sequences; <paramref name="pretty"/> moves them to nicer boundaries.</summary>
    public DiffResult ComputeDiff(bool pretty) =>
        ComputeDiff(0, _originalElementsOrHash.Length - 1, 0, _modifiedElementsOrHash.Length - 1, pretty);

    private DiffResult ComputeDiff(int originalStart, int originalEnd, int modifiedStart, int modifiedEnd, bool pretty)
    {
        var quitEarly = false;
        var changes = ComputeDiffRecursive(originalStart, originalEnd, modifiedStart, modifiedEnd, ref quitEarly);
        if (pretty)
        {
            // We have to clean up the computed diff to be more intuitive but it turns out this cannot be done
            // correctly until the entire set of diffs have been computed
            changes = PrettifyChanges(changes);
        }

        return new DiffResult(quitEarly, changes);
    }

    private List<DiffChange> ComputeDiffRecursive(int originalStart, int originalEnd, int modifiedStart, int modifiedEnd, ref bool quitEarly)
    {
        quitEarly = false;

        // Find the start of the differences
        while (originalStart <= originalEnd && modifiedStart <= modifiedEnd && ElementsAreEqual(originalStart, modifiedStart))
        {
            originalStart++;
            modifiedStart++;
        }

        // Find the end of the differences
        while (originalEnd >= originalStart && modifiedEnd >= modifiedStart && ElementsAreEqual(originalEnd, modifiedEnd))
        {
            originalEnd--;
            modifiedEnd--;
        }

        // In the special case where we either have all insertions or all deletions or the sequences are identical
        if (originalStart > originalEnd || modifiedStart > modifiedEnd)
        {
            if (modifiedStart <= modifiedEnd)
            {
                Assert(originalStart == originalEnd + 1, "originalStart should only be one more than originalEnd");
                // All insertions
                return [new DiffChange(originalStart, 0, modifiedStart, modifiedEnd - modifiedStart + 1)];
            }

            if (originalStart <= originalEnd)
            {
                Assert(modifiedStart == modifiedEnd + 1, "modifiedStart should only be one more than modifiedEnd");
                // All deletions
                return [new DiffChange(originalStart, originalEnd - originalStart + 1, modifiedStart, 0)];
            }

            Assert(originalStart == originalEnd + 1, "originalStart should only be one more than originalEnd");
            Assert(modifiedStart == modifiedEnd + 1, "modifiedStart should only be one more than modifiedEnd");
            // Identical sequences - No differences
            return [];
        }

        // This problem can be solved using the Divide-And-Conquer technique.
        var midOriginal = 0;
        var midModified = 0;
        var result = ComputeRecursionPoint(originalStart, originalEnd, modifiedStart, modifiedEnd, ref midOriginal, ref midModified, ref quitEarly);
        if (result is not null)
        {
            // Result is not-null when there was enough memory to compute the changes while searching for the
            // recursion point
            return result;
        }

        if (!quitEarly)
        {
            // We can break the problem down recursively by finding the changes in the
            // First Half:   (originalStart, modifiedStart) to (midOriginal, midModified)
            // Second Half:  (midOriginal + 1, minModified + 1) to (originalEnd, modifiedEnd)
            // NOTE: ComputeDiff() is inclusive, therefore the second range starts on the next point
            var leftChanges = ComputeDiffRecursive(originalStart, midOriginal, modifiedStart, midModified, ref quitEarly);
            List<DiffChange> rightChanges;
            if (!quitEarly)
            {
                rightChanges = ComputeDiffRecursive(midOriginal + 1, originalEnd, midModified + 1, modifiedEnd, ref quitEarly);
            }
            else
            {
                // We didn't have time to finish the first half, so we don't have time to compute this half.
                // Consider the entire rest of the sequence different.
                rightChanges = [new DiffChange(midOriginal + 1, originalEnd - (midOriginal + 1) + 1, midModified + 1, modifiedEnd - (midModified + 1) + 1)];
            }

            return ConcatenateChanges(leftChanges, rightChanges);
        }

        // If we hit here, we quit early, and so can't return anything meaningful
        return [new DiffChange(originalStart, originalEnd - originalStart + 1, modifiedStart, modifiedEnd - modifiedStart + 1)];
    }

    private List<DiffChange> WalkTrace(
        int diagonalForwardBase, int diagonalForwardStart, int diagonalForwardEnd, int diagonalForwardOffset,
        int diagonalReverseBase, int diagonalReverseStart, int diagonalReverseEnd, int diagonalReverseOffset,
        int[] forwardPoints, int[] reversePoints,
        int originalIndex, int originalEnd, ref int midOriginal,
        int modifiedIndex, int modifiedEnd, ref int midModified,
        bool deltaIsEven, ref bool quitEarly)
    {
        List<DiffChange> forwardChanges;
        List<DiffChange> reverseChanges;

        // First, walk backward through the forward diagonals history
        var changeHelper = new DiffChangeHelper();
        var diagonalMin = diagonalForwardStart;
        var diagonalMax = diagonalForwardEnd;
        var diagonalRelative = (midOriginal - midModified) - diagonalForwardOffset;
        var lastOriginalIndex = int.MinValue;
        var historyIndex = _forwardHistory.Count - 1;

        do
        {
            // Get the diagonal index from the relative diagonal number
            var diagonal = diagonalRelative + diagonalForwardBase;

            // Figure out where we came from
            if (diagonal == diagonalMin || (diagonal < diagonalMax && forwardPoints[diagonal - 1] < forwardPoints[diagonal + 1]))
            {
                // Vertical line (the element is an insert)
                originalIndex = forwardPoints[diagonal + 1];
                modifiedIndex = originalIndex - diagonalRelative - diagonalForwardOffset;
                if (originalIndex < lastOriginalIndex)
                {
                    changeHelper.MarkNextChange();
                }

                lastOriginalIndex = originalIndex;
                changeHelper.AddModifiedElement(originalIndex + 1, modifiedIndex);
                diagonalRelative = (diagonal + 1) - diagonalForwardBase; // Setup for the next iteration
            }
            else
            {
                // Horizontal line (the element is a deletion)
                originalIndex = forwardPoints[diagonal - 1] + 1;
                modifiedIndex = originalIndex - diagonalRelative - diagonalForwardOffset;
                if (originalIndex < lastOriginalIndex)
                {
                    changeHelper.MarkNextChange();
                }

                lastOriginalIndex = originalIndex - 1;
                changeHelper.AddOriginalElement(originalIndex, modifiedIndex + 1);
                diagonalRelative = (diagonal - 1) - diagonalForwardBase; // Setup for the next iteration
            }

            if (historyIndex >= 0)
            {
                forwardPoints = _forwardHistory[historyIndex];
                diagonalForwardBase = forwardPoints[0]; // We stored this in the first spot
                diagonalMin = 1;
                diagonalMax = forwardPoints.Length - 1;
            }
        }
        while (--historyIndex >= -1);

        // Ironically, we get the forward changes as the reverse of the order we added them since we technically added
        // them backwards
        forwardChanges = changeHelper.GetReverseChanges();

        if (quitEarly)
        {
            // TODO: Calculate a partial from the reverse diagonals.
            //       For now, just assume everything after the midOriginal/midModified point is a diff
            var originalStartPoint = midOriginal + 1;
            var modifiedStartPoint = midModified + 1;
            if (forwardChanges.Count > 0)
            {
                var lastForwardChange = forwardChanges[^1];
                originalStartPoint = Math.Max(originalStartPoint, lastForwardChange.GetOriginalEnd());
                modifiedStartPoint = Math.Max(modifiedStartPoint, lastForwardChange.GetModifiedEnd());
            }

            reverseChanges =
            [
                new DiffChange(originalStartPoint, originalEnd - originalStartPoint + 1, modifiedStartPoint, modifiedEnd - modifiedStartPoint + 1),
            ];
        }
        else
        {
            // Now walk backward through the reverse diagonals history
            changeHelper = new DiffChangeHelper();
            diagonalMin = diagonalReverseStart;
            diagonalMax = diagonalReverseEnd;
            diagonalRelative = (midOriginal - midModified) - diagonalReverseOffset;
            lastOriginalIndex = int.MaxValue;
            historyIndex = deltaIsEven ? _reverseHistory.Count - 1 : _reverseHistory.Count - 2;

            do
            {
                // Get the diagonal index from the relative diagonal number
                var diagonal = diagonalRelative + diagonalReverseBase;

                // Figure out where we came from
                if (diagonal == diagonalMin || (diagonal < diagonalMax && reversePoints[diagonal - 1] >= reversePoints[diagonal + 1]))
                {
                    // Horizontal line (the element is a deletion))
                    originalIndex = reversePoints[diagonal + 1] - 1;
                    modifiedIndex = originalIndex - diagonalRelative - diagonalReverseOffset;
                    if (originalIndex > lastOriginalIndex)
                    {
                        changeHelper.MarkNextChange();
                    }

                    lastOriginalIndex = originalIndex + 1;
                    changeHelper.AddOriginalElement(originalIndex + 1, modifiedIndex + 1);
                    diagonalRelative = (diagonal + 1) - diagonalReverseBase; // Setup for the next iteration
                }
                else
                {
                    // Vertical line (the element is an insertion)
                    originalIndex = reversePoints[diagonal - 1];
                    modifiedIndex = originalIndex - diagonalRelative - diagonalReverseOffset;
                    if (originalIndex > lastOriginalIndex)
                    {
                        changeHelper.MarkNextChange();
                    }

                    lastOriginalIndex = originalIndex;
                    changeHelper.AddModifiedElement(originalIndex + 1, modifiedIndex + 1);
                    diagonalRelative = (diagonal - 1) - diagonalReverseBase; // Setup for the next iteration
                }

                if (historyIndex >= 0)
                {
                    reversePoints = _reverseHistory[historyIndex];
                    diagonalReverseBase = reversePoints[0]; // We stored this in the first spot
                    diagonalMin = 1;
                    diagonalMax = reversePoints.Length - 1;
                }
            }
            while (--historyIndex >= -1);

            // There are cases where the reverse history will find diffs that are correct, but not intuitive, so we
            // need shift them.
            reverseChanges = changeHelper.GetChanges();
        }

        return ConcatenateChanges(forwardChanges, reverseChanges);
    }

    // Given the range to compute the diff on, this method finds the point:
    // (midOriginal, midModified)
    // that exists in the middle of the LCS of the two sequences and
    // is the point at which the LCS problem may be broken down recursively.
    // This method will try to keep the LCS trace in memory. If the LCS recursion
    // point is calculated and the full trace is available in memory, then this method
    // will return the change list.
    private List<DiffChange>? ComputeRecursionPoint(int originalStart, int originalEnd, int modifiedStart, int modifiedEnd, ref int midOriginal, ref int midModified, ref bool quitEarly)
    {
        int originalIndex = 0, modifiedIndex = 0;
        int diagonalForwardStart = 0, diagonalForwardEnd = 0;
        int diagonalReverseStart = 0, diagonalReverseEnd = 0;

        // To traverse the edit graph and produce the proper LCS, our actual
        // start position is just outside the given boundary
        originalStart--;
        modifiedStart--;

        // We set these up to make the compiler happy, but they will
        // be replaced before we return with the actual recursion point
        midOriginal = 0;
        midModified = 0;

        // Clear out the history
        _forwardHistory = [];
        _reverseHistory = [];

        // Each cell in the two arrays corresponds to a diagonal in the edit graph.
        // The integer value in the cell represents the originalIndex of the furthest
        // reaching point found so far that ends in that diagonal.
        // The modifiedIndex can be computed mathematically from the originalIndex and the diagonal number.
        var maxDifferences = (originalEnd - originalStart) + (modifiedEnd - modifiedStart);
        var numDiagonals = maxDifferences + 1;
        var forwardPoints = new int[numDiagonals];
        var reversePoints = new int[numDiagonals];
        // diagonalForwardBase: Index into forwardPoints of the diagonal which passes through (originalStart, modifiedStart)
        // diagonalReverseBase: Index into reversePoints of the diagonal which passes through (originalEnd, modifiedEnd)
        var diagonalForwardBase = modifiedEnd - modifiedStart;
        var diagonalReverseBase = originalEnd - originalStart;
        // diagonalForwardOffset: Geometric offset which allows modifiedIndex to be computed from originalIndex and the
        //    diagonal number (relative to diagonalForwardBase)
        // diagonalReverseOffset: Geometric offset which allows modifiedIndex to be computed from originalIndex and the
        //    diagonal number (relative to diagonalReverseBase)
        var diagonalForwardOffset = originalStart - modifiedStart;
        var diagonalReverseOffset = originalEnd - modifiedEnd;

        // delta: The difference between the end diagonal and the start diagonal. This is used to relate diagonal numbers
        //   relative to the start diagonal with diagonal numbers relative to the end diagonal.
        // The Even/Oddn-ness of this delta is important for determining when we should check for overlap
        var delta = diagonalReverseBase - diagonalForwardBase;
        var deltaIsEven = delta % 2 == 0;

        // Here we set up the start and end points as the furthest points found so far
        // in both the forward and reverse directions, respectively
        forwardPoints[diagonalForwardBase] = originalStart;
        reversePoints[diagonalReverseBase] = originalEnd;

        // Remember if we quit early, and thus need to do a best-effort result instead of a real result.
        quitEarly = false;

        // A couple of points:
        // --With this method, we iterate on the number of differences between the two sequences.
        //   The more differences there actually are, the longer this will take.
        // --Also, as the number of differences increases, we have to search on diagonals further
        //   away from the reference diagonal (which is diagonalForwardBase for forward, diagonalReverseBase for reverse).
        // --We extend on even diagonals (relative to the reference diagonal) only when numDifferences
        //   is even and odd diagonals only when numDifferences is odd.
        for (var numDifferences = 1; numDifferences <= (maxDifferences / 2.0) + 1; numDifferences++)
        {
            var furthestOriginalIndex = 0;
            var furthestModifiedIndex = 0;

            // Run the algorithm in the forward direction
            diagonalForwardStart = ClipDiagonalBound(diagonalForwardBase - numDifferences, numDifferences, diagonalForwardBase, numDiagonals);
            diagonalForwardEnd = ClipDiagonalBound(diagonalForwardBase + numDifferences, numDifferences, diagonalForwardBase, numDiagonals);
            for (var diagonal = diagonalForwardStart; diagonal <= diagonalForwardEnd; diagonal += 2)
            {
                // STEP 1: We extend the furthest reaching point in the present diagonal
                // by looking at the diagonals above and below and picking the one whose point
                // is further away from the start point (originalStart, modifiedStart)
                if (diagonal == diagonalForwardStart || (diagonal < diagonalForwardEnd && forwardPoints[diagonal - 1] < forwardPoints[diagonal + 1]))
                {
                    originalIndex = forwardPoints[diagonal + 1];
                }
                else
                {
                    originalIndex = forwardPoints[diagonal - 1] + 1;
                }

                modifiedIndex = originalIndex - (diagonal - diagonalForwardBase) - diagonalForwardOffset;

                // Save the current originalIndex so we can test for false overlap in step 3
                var tempOriginalIndex = originalIndex;

                // STEP 2: We can continue to extend the furthest reaching point in the present diagonal
                // so long as the elements are equal.
                while (originalIndex < originalEnd && modifiedIndex < modifiedEnd && ElementsAreEqual(originalIndex + 1, modifiedIndex + 1))
                {
                    originalIndex++;
                    modifiedIndex++;
                }

                forwardPoints[diagonal] = originalIndex;

                if (originalIndex + modifiedIndex > furthestOriginalIndex + furthestModifiedIndex)
                {
                    furthestOriginalIndex = originalIndex;
                    furthestModifiedIndex = modifiedIndex;
                }

                // STEP 3: If delta is odd (overlap first happens on forward when delta is odd)
                // and diagonal is in the range of reverse diagonals computed for numDifferences-1
                // (the previous iteration; we haven't computed reverse diagonals for numDifferences yet)
                // then check for overlap.
                if (!deltaIsEven && Math.Abs(diagonal - diagonalReverseBase) <= (numDifferences - 1))
                {
                    if (originalIndex >= reversePoints[diagonal])
                    {
                        midOriginal = originalIndex;
                        midModified = modifiedIndex;

                        if (tempOriginalIndex <= reversePoints[diagonal] && MaxDifferencesHistory > 0 && numDifferences <= (MaxDifferencesHistory + 1))
                        {
                            // BINGO! We overlapped, and we have the full trace in memory!
                            return WalkTrace(
                                diagonalForwardBase, diagonalForwardStart, diagonalForwardEnd, diagonalForwardOffset,
                                diagonalReverseBase, diagonalReverseStart, diagonalReverseEnd, diagonalReverseOffset,
                                forwardPoints, reversePoints,
                                originalIndex, originalEnd, ref midOriginal,
                                modifiedIndex, modifiedEnd, ref midModified,
                                deltaIsEven, ref quitEarly);
                        }

                        // Either false overlap, or we didn't have enough memory for the full trace
                        // Just return the recursion point
                        return null;
                    }
                }
            }

            // Check to see if we should be quitting early, before moving on to the next iteration.
            var matchLengthOfLongest = ((furthestOriginalIndex - originalStart) + (furthestModifiedIndex - modifiedStart) - numDifferences) / 2;

            if (_continueProcessingPredicate is not null && !_continueProcessingPredicate(furthestOriginalIndex, matchLengthOfLongest))
            {
                // We can't finish, so skip ahead to generating a result from what we have.
                quitEarly = true;

                // Use the furthest distance we got in the forward direction.
                midOriginal = furthestOriginalIndex;
                midModified = furthestModifiedIndex;

                if (matchLengthOfLongest > 0 && MaxDifferencesHistory > 0 && numDifferences <= (MaxDifferencesHistory + 1))
                {
                    // Enough of the history is in memory to walk it backwards
                    return WalkTrace(
                        diagonalForwardBase, diagonalForwardStart, diagonalForwardEnd, diagonalForwardOffset,
                        diagonalReverseBase, diagonalReverseStart, diagonalReverseEnd, diagonalReverseOffset,
                        forwardPoints, reversePoints,
                        originalIndex, originalEnd, ref midOriginal,
                        modifiedIndex, modifiedEnd, ref midModified,
                        deltaIsEven, ref quitEarly);
                }

                // We didn't actually remember enough of the history.

                // Since we are quitting the diff early, we need to shift back the originalStart and modified start
                // back into the boundary limits since we decremented their value above beyond the boundary limit.
                originalStart++;
                modifiedStart++;

                return [new DiffChange(originalStart, originalEnd - originalStart + 1, modifiedStart, modifiedEnd - modifiedStart + 1)];
            }

            // Run the algorithm in the reverse direction
            diagonalReverseStart = ClipDiagonalBound(diagonalReverseBase - numDifferences, numDifferences, diagonalReverseBase, numDiagonals);
            diagonalReverseEnd = ClipDiagonalBound(diagonalReverseBase + numDifferences, numDifferences, diagonalReverseBase, numDiagonals);
            for (var diagonal = diagonalReverseStart; diagonal <= diagonalReverseEnd; diagonal += 2)
            {
                // STEP 1: We extend the furthest reaching point in the present diagonal
                // by looking at the diagonals above and below and picking the one whose point
                // is further away from the start point (originalEnd, modifiedEnd)
                if (diagonal == diagonalReverseStart || (diagonal < diagonalReverseEnd && reversePoints[diagonal - 1] >= reversePoints[diagonal + 1]))
                {
                    originalIndex = reversePoints[diagonal + 1] - 1;
                }
                else
                {
                    originalIndex = reversePoints[diagonal - 1];
                }

                modifiedIndex = originalIndex - (diagonal - diagonalReverseBase) - diagonalReverseOffset;

                // Save the current originalIndex so we can test for false overlap
                var tempOriginalIndex = originalIndex;

                // STEP 2: We can continue to extend the furthest reaching point in the present diagonal
                // as long as the elements are equal.
                while (originalIndex > originalStart && modifiedIndex > modifiedStart && ElementsAreEqual(originalIndex, modifiedIndex))
                {
                    originalIndex--;
                    modifiedIndex--;
                }

                reversePoints[diagonal] = originalIndex;

                // STEP 4: If delta is even (overlap first happens on reverse when delta is even)
                // and diagonal is in the range of forward diagonals computed for numDifferences
                // then check for overlap.
                if (deltaIsEven && Math.Abs(diagonal - diagonalForwardBase) <= numDifferences)
                {
                    if (originalIndex <= forwardPoints[diagonal])
                    {
                        midOriginal = originalIndex;
                        midModified = modifiedIndex;

                        if (tempOriginalIndex >= forwardPoints[diagonal] && MaxDifferencesHistory > 0 && numDifferences <= (MaxDifferencesHistory + 1))
                        {
                            // BINGO! We overlapped, and we have the full trace in memory!
                            return WalkTrace(
                                diagonalForwardBase, diagonalForwardStart, diagonalForwardEnd, diagonalForwardOffset,
                                diagonalReverseBase, diagonalReverseStart, diagonalReverseEnd, diagonalReverseOffset,
                                forwardPoints, reversePoints,
                                originalIndex, originalEnd, ref midOriginal,
                                modifiedIndex, modifiedEnd, ref midModified,
                                deltaIsEven, ref quitEarly);
                        }

                        // Either false overlap, or we didn't have enough memory for the full trace
                        // Just return the recursion point
                        return null;
                    }
                }
            }

            // Save current vectors to history before the next iteration
            if (numDifferences <= MaxDifferencesHistory)
            {
                // We are allocating space for one extra int, which we fill with
                // the index of the diagonal base index
                var temp = new int[diagonalForwardEnd - diagonalForwardStart + 2];
                temp[0] = diagonalForwardBase - diagonalForwardStart + 1;
                Array.Copy(forwardPoints, diagonalForwardStart, temp, 1, diagonalForwardEnd - diagonalForwardStart + 1);
                _forwardHistory.Add(temp);

                temp = new int[diagonalReverseEnd - diagonalReverseStart + 2];
                temp[0] = diagonalReverseBase - diagonalReverseStart + 1;
                Array.Copy(reversePoints, diagonalReverseStart, temp, 1, diagonalReverseEnd - diagonalReverseStart + 1);
                _reverseHistory.Add(temp);
            }
        }

        // If we got here, then we have the full trace in history. We just have to convert it to a change list
        // NOTE: This part is a bit messy
        return WalkTrace(
            diagonalForwardBase, diagonalForwardStart, diagonalForwardEnd, diagonalForwardOffset,
            diagonalReverseBase, diagonalReverseStart, diagonalReverseEnd, diagonalReverseOffset,
            forwardPoints, reversePoints,
            originalIndex, originalEnd, ref midOriginal,
            modifiedIndex, modifiedEnd, ref midModified,
            deltaIsEven, ref quitEarly);
    }

    // Shifts the given changes to provide a more intuitive diff.
    // While the first element in a diff matches the first element after the diff,
    // we shift the diff down.
    private List<DiffChange> PrettifyChanges(List<DiffChange> changes)
    {
        // Shift all the changes down first
        for (var i = 0; i < changes.Count; i++)
        {
            var change = changes[i];
            var originalStop = i < changes.Count - 1 ? changes[i + 1].OriginalStart : _originalElementsOrHash.Length;
            var modifiedStop = i < changes.Count - 1 ? changes[i + 1].ModifiedStart : _modifiedElementsOrHash.Length;
            var checkOriginal = change.OriginalLength > 0;
            var checkModified = change.ModifiedLength > 0;

            while (change.OriginalStart + change.OriginalLength < originalStop
                && change.ModifiedStart + change.ModifiedLength < modifiedStop
                && (!checkOriginal || OriginalElementsAreEqual(change.OriginalStart, change.OriginalStart + change.OriginalLength))
                && (!checkModified || ModifiedElementsAreEqual(change.ModifiedStart, change.ModifiedStart + change.ModifiedLength)))
            {
                var startStrictEqual = ElementsAreStrictEqual(change.OriginalStart, change.ModifiedStart);
                var endStrictEqual = ElementsAreStrictEqual(change.OriginalStart + change.OriginalLength, change.ModifiedStart + change.ModifiedLength);
                if (endStrictEqual && !startStrictEqual)
                {
                    // moving the change down would create an equal change, but the elements are not strict equal
                    break;
                }

                change.OriginalStart++;
                change.ModifiedStart++;
            }

            if (i < changes.Count - 1 && ChangesOverlap(changes[i], changes[i + 1], out var merged))
            {
                changes[i] = merged!;
                changes.RemoveAt(i + 1);
                i--;
            }
        }

        // Shift changes back up until we hit empty or whitespace-only lines
        for (var i = changes.Count - 1; i >= 0; i--)
        {
            var change = changes[i];

            var originalStop = 0;
            var modifiedStop = 0;
            if (i > 0)
            {
                var prevChange = changes[i - 1];
                originalStop = prevChange.OriginalStart + prevChange.OriginalLength;
                modifiedStop = prevChange.ModifiedStart + prevChange.ModifiedLength;
            }

            var checkOriginal = change.OriginalLength > 0;
            var checkModified = change.ModifiedLength > 0;

            var bestDelta = 0;
            var bestScore = BoundaryScore(change.OriginalStart, change.OriginalLength, change.ModifiedStart, change.ModifiedLength);

            for (var delta = 1; ; delta++)
            {
                var originalStart = change.OriginalStart - delta;
                var modifiedStart = change.ModifiedStart - delta;

                if (originalStart < originalStop || modifiedStart < modifiedStop)
                {
                    break;
                }

                if (checkOriginal && !OriginalElementsAreEqual(originalStart, originalStart + change.OriginalLength))
                {
                    break;
                }

                if (checkModified && !ModifiedElementsAreEqual(modifiedStart, modifiedStart + change.ModifiedLength))
                {
                    break;
                }

                var touchingPreviousChange = originalStart == originalStop && modifiedStart == modifiedStop;
                var score = (touchingPreviousChange ? 5 : 0) + BoundaryScore(originalStart, change.OriginalLength, modifiedStart, change.ModifiedLength);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDelta = delta;
                }
            }

            change.OriginalStart -= bestDelta;
            change.ModifiedStart -= bestDelta;

            if (i > 0 && ChangesOverlap(changes[i - 1], changes[i], out var merged))
            {
                changes[i - 1] = merged!;
                changes.RemoveAt(i);
                i++;
            }
        }

        // There could be multiple longest common substrings.
        // Give preference to the ones containing longer lines
        if (_hasStrings)
        {
            for (var i = 1; i < changes.Count; i++)
            {
                var aChange = changes[i - 1];
                var bChange = changes[i];
                var matchedLength = bChange.OriginalStart - aChange.OriginalStart - aChange.OriginalLength;
                var aOriginalStart = aChange.OriginalStart;
                var bOriginalEnd = bChange.OriginalStart + bChange.OriginalLength;
                var abOriginalLength = bOriginalEnd - aOriginalStart;
                var aModifiedStart = aChange.ModifiedStart;
                var bModifiedEnd = bChange.ModifiedStart + bChange.ModifiedLength;
                var abModifiedLength = bModifiedEnd - aModifiedStart;
                // Avoid wasting a lot of time with these searches
                if (matchedLength < 5 && abOriginalLength < 20 && abModifiedLength < 20)
                {
                    var t = FindBetterContiguousSequence(aOriginalStart, abOriginalLength, aModifiedStart, abModifiedLength, matchedLength);
                    if (t is var (originalMatchStart, modifiedMatchStart))
                    {
                        if (originalMatchStart != aChange.OriginalStart + aChange.OriginalLength || modifiedMatchStart != aChange.ModifiedStart + aChange.ModifiedLength)
                        {
                            // switch to another sequence that has a better score
                            aChange.OriginalLength = originalMatchStart - aChange.OriginalStart;
                            aChange.ModifiedLength = modifiedMatchStart - aChange.ModifiedStart;
                            bChange.OriginalStart = originalMatchStart + matchedLength;
                            bChange.ModifiedStart = modifiedMatchStart + matchedLength;
                            bChange.OriginalLength = bOriginalEnd - bChange.OriginalStart;
                            bChange.ModifiedLength = bModifiedEnd - bChange.ModifiedStart;
                        }
                    }
                }
            }
        }

        return changes;
    }

    private (int OriginalStart, int ModifiedStart)? FindBetterContiguousSequence(int originalStart, int originalLength, int modifiedStart, int modifiedLength, int desiredLength)
    {
        if (originalLength < desiredLength || modifiedLength < desiredLength)
        {
            return null;
        }

        var originalMax = originalStart + originalLength - desiredLength + 1;
        var modifiedMax = modifiedStart + modifiedLength - desiredLength + 1;
        var bestScore = 0;
        var bestOriginalStart = 0;
        var bestModifiedStart = 0;
        for (var i = originalStart; i < originalMax; i++)
        {
            for (var j = modifiedStart; j < modifiedMax; j++)
            {
                var score = ContiguousSequenceScore(i, j, desiredLength);
                if (score > 0 && score > bestScore)
                {
                    bestScore = score;
                    bestOriginalStart = i;
                    bestModifiedStart = j;
                }
            }
        }

        return bestScore > 0 ? (bestOriginalStart, bestModifiedStart) : null;
    }

    private int ContiguousSequenceScore(int originalStart, int modifiedStart, int length)
    {
        var score = 0;
        for (var l = 0; l < length; l++)
        {
            if (!ElementsAreEqual(originalStart + l, modifiedStart + l))
            {
                return 0;
            }

            score += _originalStringElements[originalStart + l].Length;
        }

        return score;
    }

    private bool OriginalIsBoundary(int index)
    {
        if (index <= 0 || index >= _originalElementsOrHash.Length - 1)
        {
            return true;
        }

        return _hasStrings && string.IsNullOrWhiteSpace(_originalStringElements[index]);
    }

    private bool OriginalRegionIsBoundary(int originalStart, int originalLength)
    {
        if (OriginalIsBoundary(originalStart) || OriginalIsBoundary(originalStart - 1))
        {
            return true;
        }

        if (originalLength > 0)
        {
            var originalEnd = originalStart + originalLength;
            if (OriginalIsBoundary(originalEnd - 1) || OriginalIsBoundary(originalEnd))
            {
                return true;
            }
        }

        return false;
    }

    private bool ModifiedIsBoundary(int index)
    {
        if (index <= 0 || index >= _modifiedElementsOrHash.Length - 1)
        {
            return true;
        }

        return _hasStrings && string.IsNullOrWhiteSpace(_modifiedStringElements[index]);
    }

    private bool ModifiedRegionIsBoundary(int modifiedStart, int modifiedLength)
    {
        if (ModifiedIsBoundary(modifiedStart) || ModifiedIsBoundary(modifiedStart - 1))
        {
            return true;
        }

        if (modifiedLength > 0)
        {
            var modifiedEnd = modifiedStart + modifiedLength;
            if (ModifiedIsBoundary(modifiedEnd - 1) || ModifiedIsBoundary(modifiedEnd))
            {
                return true;
            }
        }

        return false;
    }

    private int BoundaryScore(int originalStart, int originalLength, int modifiedStart, int modifiedLength)
    {
        var originalScore = OriginalRegionIsBoundary(originalStart, originalLength) ? 1 : 0;
        var modifiedScore = ModifiedRegionIsBoundary(modifiedStart, modifiedLength) ? 1 : 0;
        return originalScore + modifiedScore;
    }

    // Concatenates the two input DiffChange lists and returns the resulting list.
    private static List<DiffChange> ConcatenateChanges(List<DiffChange> left, List<DiffChange> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return right.Count > 0 ? right : left;
        }

        if (ChangesOverlap(left[^1], right[0], out var merged))
        {
            // Since we break the problem down recursively, it is possible that we
            // might recurse in the middle of a change thereby splitting it into
            // two changes. Here in the combining stage, we detect and fuse those
            // changes back together
            var result = new List<DiffChange>(left.Count + right.Count - 1);
            result.AddRange(left.Take(left.Count - 1));
            result.Add(merged!);
            result.AddRange(right.Skip(1));
            return result;
        }

        var all = new List<DiffChange>(left.Count + right.Count);
        all.AddRange(left);
        all.AddRange(right);
        return all;
    }

    // Returns true if the two changes overlap and can be merged into a single change
    private static bool ChangesOverlap(DiffChange left, DiffChange right, out DiffChange? merged)
    {
        Assert(left.OriginalStart <= right.OriginalStart, "Left change is not less than or equal to right change");
        Assert(left.ModifiedStart <= right.ModifiedStart, "Left change is not less than or equal to right change");

        if (left.OriginalStart + left.OriginalLength >= right.OriginalStart || left.ModifiedStart + left.ModifiedLength >= right.ModifiedStart)
        {
            var originalStart = left.OriginalStart;
            var originalLength = left.OriginalLength;
            var modifiedStart = left.ModifiedStart;
            var modifiedLength = left.ModifiedLength;

            if (left.OriginalStart + left.OriginalLength >= right.OriginalStart)
            {
                originalLength = right.OriginalStart + right.OriginalLength - left.OriginalStart;
            }

            if (left.ModifiedStart + left.ModifiedLength >= right.ModifiedStart)
            {
                modifiedLength = right.ModifiedStart + right.ModifiedLength - left.ModifiedStart;
            }

            merged = new DiffChange(originalStart, originalLength, modifiedStart, modifiedLength);
            return true;
        }

        merged = null;
        return false;
    }

    // Helper method used to clip a diagonal index to the range of valid
    // diagonals. This also decides whether or not the diagonal index,
    // if it exceeds the boundary, should be clipped to the boundary or clipped
    // one inside the boundary depending on the Even/Odd status of the boundary
    // and numDifferences.
    private static int ClipDiagonalBound(int diagonal, int numDifferences, int diagonalBaseIndex, int numDiagonals)
    {
        if (diagonal >= 0 && diagonal < numDiagonals)
        {
            // Nothing to clip, its in range
            return diagonal;
        }

        // diagonalsBelow: The number of diagonals below the reference diagonal
        // diagonalsAbove: The number of diagonals above the reference diagonal
        var diagonalsBelow = diagonalBaseIndex;
        var diagonalsAbove = numDiagonals - diagonalBaseIndex - 1;
        var diffEven = numDifferences % 2 == 0;

        if (diagonal < 0)
        {
            var lowerBoundEven = diagonalsBelow % 2 == 0;
            return diffEven == lowerBoundEven ? 0 : 1;
        }

        var upperBoundEven = diagonalsAbove % 2 == 0;
        return diffEven == upperBoundEven ? numDiagonals - 1 : numDiagonals - 2;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
