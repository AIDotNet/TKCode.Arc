// Ported from vscode (src/vs/editor/common/diff/defaultLinesDiffComputer/algorithms/dynamicProgrammingDiffing.ts and
// the Array2D of defaultLinesDiffComputer/utils.ts, commit b57d5c9e), MIT license.
// Differences: the three tables are flat arrays (double scores, byte directions, int lengths) instead of Array2D.

namespace TKCode.Arc.Editor.Diff.Algorithms;

/// <summary>An O(MN) diffing algorithm that supports a score function.</summary>
internal sealed class DynamicProgrammingDiffing : IDiffAlgorithm
{
    public DiffAlgorithmResult Compute(ISequence sequence1, ISequence sequence2, ITimeout? timeout = null) =>
        Compute(sequence1, sequence2, timeout, null);

    public DiffAlgorithmResult Compute(ISequence sequence1, ISequence sequence2, ITimeout? timeout, Func<int, int, double>? equalityScore)
    {
        timeout ??= InfiniteTimeout.Instance;
        if (sequence1.Length == 0 || sequence2.Length == 0)
        {
            return DiffAlgorithmResult.Trivial(sequence1, sequence2);
        }

        var width = sequence1.Length;
        var height = sequence2.Length;
        // lcsLengths[i, j]: Length of the longest common subsequence of sequence1.substring(0, i + 1) and
        // sequence2.substring(0, j + 1).
        var lcsLengths = new double[width * height];
        var directions = new byte[width * height];
        var lengths = new int[width * height];

        // ==== Initializing lcsLengths ====
        for (var s1 = 0; s1 < width; s1++)
        {
            for (var s2 = 0; s2 < height; s2++)
            {
                if (!timeout.IsValid())
                {
                    return DiffAlgorithmResult.TrivialTimedOut(sequence1, sequence2);
                }

                var horizontalLen = s1 == 0 ? 0 : lcsLengths[s1 - 1 + s2 * width];
                var verticalLen = s2 == 0 ? 0 : lcsLengths[s1 + (s2 - 1) * width];

                double extendedSeqScore;
                if (sequence1.GetElement(s1) == sequence2.GetElement(s2))
                {
                    extendedSeqScore = s1 == 0 || s2 == 0 ? 0 : lcsLengths[s1 - 1 + (s2 - 1) * width];
                    if (s1 > 0 && s2 > 0 && directions[s1 - 1 + (s2 - 1) * width] == 3)
                    {
                        // Prefer consecutive diagonals
                        extendedSeqScore += lengths[s1 - 1 + (s2 - 1) * width];
                    }

                    extendedSeqScore += equalityScore is not null ? equalityScore(s1, s2) : 1;
                }
                else
                {
                    extendedSeqScore = -1;
                }

                var newValue = Math.Max(Math.Max(horizontalLen, verticalLen), extendedSeqScore);
                var index = s1 + s2 * width;

                if (newValue == extendedSeqScore)
                {
                    // Prefer diagonals
                    var prevLen = s1 > 0 && s2 > 0 ? lengths[s1 - 1 + (s2 - 1) * width] : 0;
                    lengths[index] = prevLen + 1;
                    directions[index] = 3;
                }
                else if (newValue == horizontalLen)
                {
                    lengths[index] = 0;
                    directions[index] = 1;
                }
                else if (newValue == verticalLen)
                {
                    lengths[index] = 0;
                    directions[index] = 2;
                }

                lcsLengths[index] = newValue;
            }
        }

        // ==== Backtracking ====
        var result = new List<SequenceDiff>();
        var lastAligningPosS1 = width;
        var lastAligningPosS2 = height;

        void ReportDecreasingAligningPositions(int s1, int s2)
        {
            if (s1 + 1 != lastAligningPosS1 || s2 + 1 != lastAligningPosS2)
            {
                result.Add(new SequenceDiff(new OffsetRange(s1 + 1, lastAligningPosS1), new OffsetRange(s2 + 1, lastAligningPosS2)));
            }

            lastAligningPosS1 = s1;
            lastAligningPosS2 = s2;
        }

        var i1 = width - 1;
        var i2 = height - 1;
        while (i1 >= 0 && i2 >= 0)
        {
            var direction = directions[i1 + i2 * width];
            if (direction == 3)
            {
                ReportDecreasingAligningPositions(i1, i2);
                i1--;
                i2--;
            }
            else if (direction == 1)
            {
                i1--;
            }
            else
            {
                i2--;
            }
        }

        ReportDecreasingAligningPositions(-1, -1);
        result.Reverse();
        return new DiffAlgorithmResult(result, false);
    }
}
