// Ported from vscode (src/vs/editor/common/diff/defaultLinesDiffComputer/algorithms/myersDiffAlgorithm.ts, commit
// b57d5c9e), MIT license.
// Differences: the negative-index arrays grow by doubling for both value types and paths (upstream's
// FastArrayNegativeIndices relies on JavaScript arrays growing by themselves).

namespace TKCode.Arc.Editor.Diff.Algorithms;

/// <summary>An O(ND) diff algorithm that has a quadratic space worst-case complexity.</summary>
internal sealed class MyersDiffAlgorithm : IDiffAlgorithm
{
    public DiffAlgorithmResult Compute(ISequence seq1, ISequence seq2, ITimeout? timeout = null)
    {
        timeout ??= InfiniteTimeout.Instance;
        // These are common special cases. The early return improves performance dramatically.
        if (seq1.Length == 0 || seq2.Length == 0)
        {
            return DiffAlgorithmResult.Trivial(seq1, seq2);
        }

        var seqX = seq1; // Text on the x axis
        var seqY = seq2; // Text on the y axis

        int GetXAfterSnake(int x, int y)
        {
            while (x < seqX.Length && y < seqY.Length && seqX.GetElement(x) == seqY.GetElement(y))
            {
                x++;
                y++;
            }

            return x;
        }

        var d = 0;
        // V[k]: X value of longest d-line that ends in diagonal k.
        // d-line: path from (0,0) to (x,y) that uses exactly d non-diagonals.
        // diagonal k: Set of points (x,y) with x-y = k.
        var v = new FastArrayNegativeIndices<int>();
        v.Set(0, GetXAfterSnake(0, 0));

        var paths = new FastArrayNegativeIndices<SnakePath?>();
        paths.Set(0, v.Get(0) == 0 ? null : new SnakePath(null, 0, 0, v.Get(0)));

        var k = 0;

        while (true)
        {
            d++;
            if (!timeout.IsValid())
            {
                return DiffAlgorithmResult.TrivialTimedOut(seqX, seqY);
            }

            // The paper has `for (k = -d; k <= d; k += 2)`, but we can ignore diagonals that cannot influence the result.
            var lowerBound = -Math.Min(d, seqY.Length + (d % 2));
            var upperBound = Math.Min(d, seqX.Length + (d % 2));
            var found = false;
            for (k = lowerBound; k <= upperBound; k += 2)
            {
                // We can use the X values of (d-1)-lines to compute X value of the longest d-lines.
                var maxXofDLineTop = k == upperBound ? -1 : v.Get(k + 1); // We take a vertical non-diagonal (add a symbol in seqX)
                var maxXofDLineLeft = k == lowerBound ? -1 : v.Get(k - 1) + 1; // We take a horizontal non-diagonal (+1 x) (delete a symbol in seqX)
                var x = Math.Min(Math.Max(maxXofDLineTop, maxXofDLineLeft), seqX.Length);
                var y = x - k;
                if (x > seqX.Length || y > seqY.Length)
                {
                    // This diagonal is irrelevant for the result.
                    continue;
                }

                var newMaxX = GetXAfterSnake(x, y);
                v.Set(k, newMaxX);
                var lastPath = x == maxXofDLineTop ? paths.Get(k + 1) : paths.Get(k - 1);
                paths.Set(k, newMaxX != x ? new SnakePath(lastPath, x, y, newMaxX - x) : lastPath);

                if (v.Get(k) == seqX.Length && v.Get(k) - k == seqY.Length)
                {
                    found = true;
                    break;
                }
            }

            if (found)
            {
                break;
            }
        }

        var path = paths.Get(k);
        var result = new List<SequenceDiff>();
        var lastAligningPosS1 = seqX.Length;
        var lastAligningPosS2 = seqY.Length;

        while (true)
        {
            var endX = path is not null ? path.X + path.Length : 0;
            var endY = path is not null ? path.Y + path.Length : 0;

            if (endX != lastAligningPosS1 || endY != lastAligningPosS2)
            {
                result.Add(new SequenceDiff(new OffsetRange(endX, lastAligningPosS1), new OffsetRange(endY, lastAligningPosS2)));
            }

            if (path is null)
            {
                break;
            }

            lastAligningPosS1 = path.X;
            lastAligningPosS2 = path.Y;
            path = path.Prev;
        }

        result.Reverse();
        return new DiffAlgorithmResult(result, false);
    }

    private sealed class SnakePath(SnakePath? prev, int x, int y, int length)
    {
        public SnakePath? Prev { get; } = prev;

        public int X { get; } = x;

        public int Y { get; } = y;

        public int Length { get; } = length;
    }

    /// <summary>An array that supports fast negative indices.</summary>
    private sealed class FastArrayNegativeIndices<T>
    {
        private T[] _positiveArr = new T[10];
        private T[] _negativeArr = new T[10];

        public T Get(int idx)
        {
            if (idx < 0)
            {
                idx = -idx - 1;
                return idx < _negativeArr.Length ? _negativeArr[idx] : default!;
            }

            return idx < _positiveArr.Length ? _positiveArr[idx] : default!;
        }

        public void Set(int idx, T value)
        {
            if (idx < 0)
            {
                idx = -idx - 1;
                EnsureCapacity(ref _negativeArr, idx);
                _negativeArr[idx] = value;
            }
            else
            {
                EnsureCapacity(ref _positiveArr, idx);
                _positiveArr[idx] = value;
            }
        }

        private static void EnsureCapacity(ref T[] array, int idx)
        {
            if (idx >= array.Length)
            {
                Array.Resize(ref array, Math.Max(array.Length * 2, idx + 1));
            }
        }
    }
}
