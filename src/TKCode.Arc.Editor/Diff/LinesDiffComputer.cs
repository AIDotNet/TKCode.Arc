// Ported from vscode (src/vs/editor/common/diff/linesDiffComputer.ts and linesDiffComputers.ts, commit b57d5c9e), MIT
// license.
// Differences: the options are a record with upstream's defaults spelled out; a CancellationToken can end a computation
// like the timeout does (the diff editor cancels a diff of text that changed again); the external and wasm computers
// (separate npm packages upstream) are not ported.

namespace TKCode.Arc.Editor.Diff;

/// <summary>Computes the differences between two lists of lines.</summary>
internal interface ILinesDiffComputer
{
    LinesDiff ComputeDiff(IReadOnlyList<string> originalLines, IReadOnlyList<string> modifiedLines, LinesDiffComputerOptions options);
}

/// <summary>The options of a diff computation (upstream's ILinesDiffComputerOptions).</summary>
internal sealed record LinesDiffComputerOptions
{
    /// <summary>Whether leading and trailing whitespace changes are ignored.</summary>
    public bool IgnoreTrimWhitespace { get; init; }

    /// <summary>The time the computation may take, in milliseconds; 0 means no limit.</summary>
    public int MaxComputationTimeMs { get; init; }

    /// <summary>Whether moved blocks of lines are detected.</summary>
    public bool ComputeMoves { get; init; }

    /// <summary>Whether character changes are extended to whole sub-words (fooBar: foo, Bar).</summary>
    public bool ExtendToSubwords { get; init; }

    /// <summary>Ends the computation early, like the timeout (not upstream's).</summary>
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>The result of a diff: the changes, the moved blocks, and whether the time ran out.</summary>
internal sealed class LinesDiff(IReadOnlyList<DetailedLineRangeMapping> changes, IReadOnlyList<MovedText> moves, bool hitTimeout)
{
    public IReadOnlyList<DetailedLineRangeMapping> Changes { get; } = changes;

    /// <summary>
    /// Sorted by original line ranges. The original line ranges and the modified line ranges must be disjoint (but can
    /// be touching).
    /// </summary>
    public IReadOnlyList<MovedText> Moves { get; } = moves;

    /// <summary>
    /// Whether the time ran out. Then the diffs might be an approximation and the user should be asked to rerun the
    /// diff with more time.
    /// </summary>
    public bool HitTimeout { get; } = hitTimeout;
}

/// <summary>A block of lines that moved, with the changes inside it.</summary>
internal sealed class MovedText(LineRangeMapping lineRangeMapping, IReadOnlyList<DetailedLineRangeMapping> changes)
{
    public LineRangeMapping LineRangeMapping { get; } = lineRangeMapping;

    /// <summary>
    /// The diff from the original text to the moved text. Must be contained in the original / modified line range.
    /// Can be empty if the text didn't change (only moved).
    /// </summary>
    public IReadOnlyList<DetailedLineRangeMapping> Changes { get; } = changes;

    public MovedText Flip() => new(LineRangeMapping.Flip(), Changes.Select(c => c.Flip()).ToList());
}

/// <summary>The diff computers (upstream's linesDiffComputers).</summary>
internal static class LinesDiffComputers
{
    /// <summary>The legacy computer (the LCS diff of the earlier Monaco versions).</summary>
    public static ILinesDiffComputer GetLegacy() => new LegacyLinesDiffComputer();

    /// <summary>The default ("advanced") computer.</summary>
    public static ILinesDiffComputer GetDefault() => new DefaultLinesDiffComputer();
}
