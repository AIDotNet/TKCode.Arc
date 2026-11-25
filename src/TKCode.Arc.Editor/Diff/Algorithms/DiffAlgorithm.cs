// Ported from vscode (src/vs/editor/common/diff/defaultLinesDiffComputer/algorithms/diffAlgorithm.ts, commit b57d5c9e),
// MIT license.
// Differences:
// - ISequence.getBoundaryScore is optional upstream; here HasBoundaryScore says whether a sequence has one.
// - DateTimeout measures with a Stopwatch and also ends when a CancellationToken is cancelled, so a diff that is no
//   longer wanted (the text changed again) stops early.
// - SequenceDiff and OffsetPair are classes and records as upstream; "undefined" results are null.

using System.Diagnostics;

namespace TKCode.Arc.Editor.Diff.Algorithms;

/// <summary>A synchronous diff algorithm over two sequences.</summary>
internal interface IDiffAlgorithm
{
    DiffAlgorithmResult Compute(ISequence sequence1, ISequence sequence2, ITimeout? timeout = null);
}

/// <summary>The differences found, and whether the time ran out (then they are an approximation).</summary>
internal sealed class DiffAlgorithmResult(List<SequenceDiff> diffs, bool hitTimeout)
{
    public static DiffAlgorithmResult Trivial(ISequence seq1, ISequence seq2) =>
        new([new SequenceDiff(OffsetRange.OfLength(seq1.Length), OffsetRange.OfLength(seq2.Length))], false);

    public static DiffAlgorithmResult TrivialTimedOut(ISequence seq1, ISequence seq2) =>
        new([new SequenceDiff(OffsetRange.OfLength(seq1.Length), OffsetRange.OfLength(seq2.Length))], true);

    public List<SequenceDiff> Diffs { get; } = diffs;

    /// <summary>Whether the time ran out; the diffs might then be an approximation.</summary>
    public bool HitTimeout { get; } = hitTimeout;
}

/// <summary>A range of sequence 1 that differs from a range of sequence 2.</summary>
internal sealed class SequenceDiff(OffsetRange seq1Range, OffsetRange seq2Range)
{
    public static List<SequenceDiff> Invert(IReadOnlyList<SequenceDiff> sequenceDiffs, int doc1Length)
    {
        var result = new List<SequenceDiff>();
        for (var i = 0; i <= sequenceDiffs.Count; i++)
        {
            var a = i == 0 ? null : sequenceDiffs[i - 1];
            var b = i == sequenceDiffs.Count ? null : sequenceDiffs[i];
            result.Add(FromOffsetPairs(
                a is not null ? a.GetEndExclusives() : OffsetPair.Zero,
                b is not null ? b.GetStarts() : new OffsetPair(doc1Length, (a is not null ? a.Seq2Range.EndExclusive - a.Seq1Range.EndExclusive : 0) + doc1Length)));
        }

        return result;
    }

    public static SequenceDiff FromOffsetPairs(OffsetPair start, OffsetPair endExclusive) =>
        new(new OffsetRange(start.Offset1, endExclusive.Offset1), new OffsetRange(start.Offset2, endExclusive.Offset2));

    public static void AssertSorted(IReadOnlyList<SequenceDiff> sequenceDiffs)
    {
        SequenceDiff? last = null;
        foreach (var cur in sequenceDiffs)
        {
            if (last is not null && !(last.Seq1Range.EndExclusive <= cur.Seq1Range.Start && last.Seq2Range.EndExclusive <= cur.Seq2Range.Start))
            {
                throw new InvalidOperationException("Sequence diffs must be sorted");
            }

            last = cur;
        }
    }

    public OffsetRange Seq1Range { get; } = seq1Range;

    public OffsetRange Seq2Range { get; } = seq2Range;

    public SequenceDiff Swap() => new(Seq2Range, Seq1Range);

    public override string ToString() => $"{Seq1Range} <-> {Seq2Range}";

    public SequenceDiff Join(SequenceDiff other) => new(Seq1Range.Join(other.Seq1Range), Seq2Range.Join(other.Seq2Range));

    public SequenceDiff Delta(int offset) => offset == 0 ? this : new SequenceDiff(Seq1Range.Delta(offset), Seq2Range.Delta(offset));

    public SequenceDiff DeltaStart(int offset) => offset == 0 ? this : new SequenceDiff(Seq1Range.DeltaStart(offset), Seq2Range.DeltaStart(offset));

    public SequenceDiff DeltaEnd(int offset) => offset == 0 ? this : new SequenceDiff(Seq1Range.DeltaEnd(offset), Seq2Range.DeltaEnd(offset));

    public bool IntersectsOrTouches(SequenceDiff other) =>
        Seq1Range.IntersectsOrTouches(other.Seq1Range) || Seq2Range.IntersectsOrTouches(other.Seq2Range);

    public SequenceDiff? Intersect(SequenceDiff other)
    {
        var i1 = Seq1Range.Intersect(other.Seq1Range);
        var i2 = Seq2Range.Intersect(other.Seq2Range);
        if (i1 is null || i2 is null)
        {
            return null;
        }

        return new SequenceDiff(i1.Value, i2.Value);
    }

    public OffsetPair GetStarts() => new(Seq1Range.Start, Seq2Range.Start);

    public OffsetPair GetEndExclusives() => new(Seq1Range.EndExclusive, Seq2Range.EndExclusive);
}

/// <summary>A pair of offsets, one in each sequence.</summary>
internal readonly record struct OffsetPair(int Offset1, int Offset2)
{
    public static readonly OffsetPair Zero = new(0, 0);

    public static readonly OffsetPair Max = new(int.MaxValue, int.MaxValue);

    public OffsetPair Delta(int offset) => offset == 0 ? this : new OffsetPair(Offset1 + offset, Offset2 + offset);

    public override string ToString() => $"{Offset1} <-> {Offset2}";
}

/// <summary>A sequence of numbers to diff.</summary>
internal interface ISequence
{
    int GetElement(int offset);

    int Length { get; }

    /// <summary>Whether <see cref="GetBoundaryScore"/> is implemented (it is optional upstream).</summary>
    bool HasBoundaryScore => false;

    /// <summary>
    /// The higher the score, the better that offset can be used to split the sequence; used to optimize insertions.
    /// Must not be negative.
    /// </summary>
    int GetBoundaryScore(int length) => 0;

    /// <summary>
    /// For line sequences, <see cref="GetElement"/> represents trimmed lines; this checks equality of the original lines.
    /// It prevents shifting to less matching lines.
    /// </summary>
    bool IsStronglyEqual(int offset1, int offset2);
}

/// <summary>Says whether a computation may go on.</summary>
internal interface ITimeout
{
    bool IsValid();
}

/// <summary>A timeout that never ends.</summary>
internal sealed class InfiniteTimeout : ITimeout
{
    public static readonly InfiniteTimeout Instance = new();

    public bool IsValid() => true;
}

/// <summary>A timeout that ends after a time (and, here, when a cancellation token is cancelled).</summary>
internal sealed class DateTimeout : ITimeout
{
    private readonly long _startTimestamp = Stopwatch.GetTimestamp();
    private readonly double _timeoutMs;
    private readonly CancellationToken _cancellationToken;
    private bool _valid = true;
    private bool _disabled;

    public DateTimeout(double timeoutMs, CancellationToken cancellationToken = default)
    {
        if (timeoutMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), "timeout must be positive");
        }

        _timeoutMs = timeoutMs;
        _cancellationToken = cancellationToken;
    }

    public bool IsValid()
    {
        if (_disabled)
        {
            return true;
        }

        var valid = Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds < _timeoutMs && !_cancellationToken.IsCancellationRequested;
        if (!valid && _valid)
        {
            _valid = false; // timeout reached
        }

        return _valid;
    }

    /// <summary>Makes the timeout never end (upstream's debugging helper).</summary>
    public void Disable()
    {
        _disabled = true;
        _valid = true;
    }
}

/// <summary>A timeout that ends only when a cancellation token is cancelled (an infinite timeout that can be stopped).</summary>
internal sealed class CancellationTimeout(CancellationToken cancellationToken) : ITimeout
{
    public bool IsValid() => !cancellationToken.IsCancellationRequested;
}
