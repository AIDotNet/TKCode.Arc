// Ported from vscode (src/vs/editor/common/diff/documentDiffProvider.ts: IDocumentDiff, IDocumentDiffProviderOptions,
// nullDocumentDiff; the diff part of src/vs/editor/common/services/editorWebWorker.ts: $computeDiff,
// _modelsAreIdentical; normalizeDocumentDiff of src/vs/editor/browser/widget/diffEditor/diffEditorViewModel.ts; and
// verifyLinesDiff.ts; commit b57d5c9e), MIT license.
// Differences: upstream computes in a web worker on mirror models; here the lines are read from TextSnapshots on the
// thread pool (the snapshots are immutable, design/architecture/code-editor.md section 2), and the computation stops
// when the cancellation token is cancelled. The diff algorithm is the default one ("advanced") or the legacy one.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Diff;

/// <summary>The options of a document diff (upstream's IDocumentDiffProviderOptions).</summary>
internal sealed record DocumentDiffProviderOptions
{
    /// <summary>Whether leading and trailing whitespace changes are ignored (Monaco's default: true).</summary>
    public bool IgnoreTrimWhitespace { get; init; } = true;

    /// <summary>The time the computation may take, in milliseconds; 0 means no limit (Monaco's default: 5000).</summary>
    public int MaxComputationTimeMs { get; init; } = 5000;

    /// <summary>Whether moved blocks of lines are detected.</summary>
    public bool ComputeMoves { get; init; }

    /// <summary>Whether the legacy algorithm is used instead of the default ("advanced") one.</summary>
    public bool UseLegacyAlgorithm { get; init; }
}

/// <summary>The diff of two documents (upstream's IDocumentDiff).</summary>
internal sealed record DocumentDiff(bool Identical, bool QuitEarly, IReadOnlyList<DetailedLineRangeMapping> Changes, IReadOnlyList<MovedText> Moves)
{
    /// <summary>The diff of two identical documents (upstream's nullDocumentDiff).</summary>
    public static readonly DocumentDiff Null = new(true, false, [], []);
}

/// <summary>Computes document diffs from snapshots, off the UI thread.</summary>
internal static class DocumentDiffComputer
{
    /// <summary>
    /// Computes the diff of two snapshots on the thread pool. A cancelled token ends the computation; the task then
    /// completes as cancelled.
    /// </summary>
    public static Task<DocumentDiff> ComputeDiffAsync(TextSnapshot original, TextSnapshot modified, DocumentDiffProviderOptions options, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            var originalLines = ReadLines(original);
            var modifiedLines = ReadLines(modified);
            cancellationToken.ThrowIfCancellationRequested();
            var result = ComputeDiff(originalLines, modifiedLines, options, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }, cancellationToken);

    /// <summary>The lines of a snapshot.</summary>
    public static string[] ReadLines(TextSnapshot snapshot)
    {
        var lines = new string[snapshot.LineCount];
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = snapshot.GetLineContent(i + 1);
        }

        return lines;
    }

    /// <summary>Computes the diff of two lists of lines, normalized as the diff editor wants it.</summary>
    public static DocumentDiff ComputeDiff(IReadOnlyList<string> originalLines, IReadOnlyList<string> modifiedLines, DocumentDiffProviderOptions options, CancellationToken cancellationToken = default)
    {
        var computer = options.UseLegacyAlgorithm ? LinesDiffComputers.GetLegacy() : LinesDiffComputers.GetDefault();
        var result = computer.ComputeDiff(originalLines, modifiedLines, new LinesDiffComputerOptions
        {
            IgnoreTrimWhitespace = options.IgnoreTrimWhitespace,
            MaxComputationTimeMs = options.MaxComputationTimeMs,
            ComputeMoves = options.ComputeMoves,
            CancellationToken = cancellationToken,
        });

        var identical = result.Changes.Count <= 0 && ModelsAreIdentical(originalLines, modifiedLines);
        var diff = new DocumentDiff(identical, result.HitTimeout, result.Changes, result.Moves);
        return Normalize(diff, originalLines, modifiedLines);
    }

    private static bool ModelsAreIdentical(IReadOnlyList<string> original, IReadOnlyList<string> modified)
    {
        if (original.Count != modified.Count)
        {
            return false;
        }

        for (var i = 0; i < original.Count; i++)
        {
            if (!string.Equals(original[i], modified[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Extends inner changes that cover whole lines to the start of the next line (upstream's normalizeDocumentDiff),
    /// so that a removed or inserted line is highlighted with its line break.
    /// </summary>
    public static DocumentDiff Normalize(DocumentDiff diff, IReadOnlyList<string> originalLines, IReadOnlyList<string> modifiedLines) =>
        diff with
        {
            Changes = diff.Changes.Select(c => new DetailedLineRangeMapping(
                c.Original,
                c.Modified,
                c.InnerChanges?.Select(i => NormalizeRangeMapping(i, originalLines, modifiedLines)).ToList())).ToList(),
        };

    private static RangeMapping NormalizeRangeMapping(RangeMapping rangeMapping, IReadOnlyList<string> original, IReadOnlyList<string> modified)
    {
        var originalRange = rangeMapping.OriginalRange;
        var modifiedRange = rangeMapping.ModifiedRange;
        if (originalRange.StartColumn == 1 && modifiedRange.StartColumn == 1
            && (originalRange.EndColumn != 1 || modifiedRange.EndColumn != 1)
            && originalRange.EndColumn == GetLineMaxColumn(original, originalRange.EndLineNumber)
            && modifiedRange.EndColumn == GetLineMaxColumn(modified, modifiedRange.EndLineNumber)
            && originalRange.EndLineNumber < original.Count
            && modifiedRange.EndLineNumber < modified.Count)
        {
            originalRange = originalRange.SetEndPosition(originalRange.EndLineNumber + 1, 1);
            modifiedRange = modifiedRange.SetEndPosition(modifiedRange.EndLineNumber + 1, 1);
        }

        return new RangeMapping(originalRange, modifiedRange);
    }

    private static int GetLineMaxColumn(IReadOnlyList<string> lines, int lineNumber) =>
        lineNumber >= 1 && lineNumber <= lines.Count ? lines[lineNumber - 1].Length + 1 : -1;

    /// <summary>
    /// Whether applying the diff's inner changes to the original lines gives the modified lines (upstream's
    /// verifyLinesDiff): "valid", "invalid", or "unverified" when a change has no inner changes.
    /// </summary>
    public static string VerifyLinesDiff(IReadOnlyList<DetailedLineRangeMapping> changes, IReadOnlyList<string> originalLines, IReadOnlyList<string> modifiedLines, bool ignoreTrimWhitespace)
    {
        if (changes.Any(c => c.InnerChanges is null))
        {
            return "unverified";
        }

        try
        {
            var actual = RangeMappings.ApplyToLines(changes, originalLines, modifiedLines);
            if (!ignoreTrimWhitespace)
            {
                return actual == string.Join('\n', modifiedLines) ? "valid" : "invalid";
            }

            var actualLines = actual.Split('\n');
            return actualLines.Length == modifiedLines.Count
                   && actualLines.Select((line, i) => DiffUtils.JsTrim(line) == DiffUtils.JsTrim(modifiedLines[i])).All(b => b)
                ? "valid"
                : "invalid";
        }
        catch (InvalidOperationException)
        {
            return "invalid";
        }
    }
}
