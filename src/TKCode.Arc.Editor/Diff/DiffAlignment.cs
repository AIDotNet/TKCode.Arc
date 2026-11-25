// Ported from vscode (src/vs/editor/browser/widget/diffEditor/components/diffEditorViewZones/diffEditorViewZones.ts:
// computeRangeAlignment, ILineRangeAlignment, allowsTrueInlineDiffRendering; commit b57d5c9e), MIT license.
// Differences: pure: the editors' extra line heights (wrapped lines, foreign view zones; upstream's
// getAdditionalLineHeights) and the original model's line lengths come in as arguments, so the engine computes the
// alignment without an editor. The extra heights are a sorted list consumed like upstream's ArrayQueue.

namespace TKCode.Arc.Editor.Diff;

/// <summary>Space a model line takes beyond one line height (a wrapped line, a view zone after it).</summary>
internal readonly record struct AdditionalLineHeight(int LineNumber, double HeightInPx);

/// <summary>
/// Lines of the original and the modified side that the diff editor shows at the same height (upstream's
/// ILineRangeAlignment); the heights account for wrapping and foreign view zones.
/// </summary>
internal sealed record LineRangeAlignment(
    LineRange OriginalRange,
    LineRange ModifiedRange,
    double OriginalHeightInPx,
    double ModifiedHeightInPx,
    DetailedLineRangeMapping? Diff);

/// <summary>The alignment of the two sides of a diff editor.</summary>
internal static class DiffAlignment
{
    /// <summary>
    /// The ranges to align (upstream's computeRangeAlignment). With <paramref name="innerHunkAlignment"/> the lines
    /// inside a change are aligned at its inner changes too, so unchanged text in it lines up.
    /// </summary>
    public static List<LineRangeAlignment> ComputeRangeAlignment(
        IReadOnlyList<DetailedLineRangeMapping> diffs,
        IReadOnlyList<AdditionalLineHeight> originalAdditionalHeights,
        IReadOnlyList<AdditionalLineHeight> modifiedAdditionalHeights,
        double origLineHeight,
        double modLineHeight,
        bool innerHunkAlignment,
        Func<int, int> getOriginalLineMaxColumn,
        int originalLineCount)
    {
        var originalLineHeightOverrides = new Queue<AdditionalLineHeight>(originalAdditionalHeights);
        var modifiedLineHeightOverrides = new Queue<AdditionalLineHeight>(modifiedAdditionalHeights);

        var result = new List<LineRangeAlignment>();

        var lastOriginalLineNumber = 0;
        var lastModifiedLineNumber = 0;

        void HandleAlignmentsOutsideOfDiffs(int untilOriginalLineNumberExclusive, int untilModifiedLineNumberExclusive)
        {
            while (true)
            {
                AdditionalLineHeight? origNext = originalLineHeightOverrides.Count > 0 ? originalLineHeightOverrides.Peek() : null;
                AdditionalLineHeight? modNext = modifiedLineHeightOverrides.Count > 0 ? modifiedLineHeightOverrides.Peek() : null;
                if (origNext is { } o && o.LineNumber >= untilOriginalLineNumberExclusive)
                {
                    origNext = null;
                }

                if (modNext is { } m && m.LineNumber >= untilModifiedLineNumberExclusive)
                {
                    modNext = null;
                }

                if (origNext is null && modNext is null)
                {
                    break;
                }

                var distOrig = origNext is { } on ? on.LineNumber - lastOriginalLineNumber : int.MaxValue;
                var distNext = modNext is { } mn ? mn.LineNumber - lastModifiedLineNumber : int.MaxValue;

                if (distOrig < distNext)
                {
                    originalLineHeightOverrides.Dequeue();
                    modNext = new AdditionalLineHeight(origNext!.Value.LineNumber - lastOriginalLineNumber + lastModifiedLineNumber, 0);
                }
                else if (distOrig > distNext)
                {
                    modifiedLineHeightOverrides.Dequeue();
                    origNext = new AdditionalLineHeight(modNext!.Value.LineNumber - lastModifiedLineNumber + lastOriginalLineNumber, 0);
                }
                else
                {
                    originalLineHeightOverrides.Dequeue();
                    modifiedLineHeightOverrides.Dequeue();
                }

                result.Add(new LineRangeAlignment(
                    LineRange.OfLength(origNext!.Value.LineNumber, 1),
                    LineRange.OfLength(modNext!.Value.LineNumber, 1),
                    origLineHeight + origNext.Value.HeightInPx,
                    modLineHeight + modNext.Value.HeightInPx,
                    null));
            }
        }

        static double TakeWhile(Queue<AdditionalLineHeight> queue, int lineNumberExclusive)
        {
            var sum = 0.0;
            while (queue.Count > 0 && queue.Peek().LineNumber < lineNumberExclusive)
            {
                sum += queue.Dequeue().HeightInPx;
            }

            return sum;
        }

        foreach (var c in diffs)
        {
            HandleAlignmentsOutsideOfDiffs(c.Original.StartLineNumber, c.Modified.StartLineNumber);

            var first = true;
            var lastModLineNumber = c.Modified.StartLineNumber;
            var lastOrigLineNumber = c.Original.StartLineNumber;

            void EmitAlignment(int origLineNumberExclusive, int modLineNumberExclusive, bool forceAlignment = false)
            {
                if (origLineNumberExclusive < lastOrigLineNumber || modLineNumberExclusive < lastModLineNumber)
                {
                    return;
                }

                if (first)
                {
                    first = false;
                }
                else if (!forceAlignment && (origLineNumberExclusive == lastOrigLineNumber || modLineNumberExclusive == lastModLineNumber))
                {
                    // This causes a re-alignment of an already aligned line.
                    // However, we don't care for the final alignment.
                    return;
                }

                var originalRange = new LineRange(lastOrigLineNumber, origLineNumberExclusive);
                var modifiedRange = new LineRange(lastModLineNumber, modLineNumberExclusive);
                if (originalRange.IsEmpty && modifiedRange.IsEmpty)
                {
                    return;
                }

                var originalAdditionalHeight = TakeWhile(originalLineHeightOverrides, origLineNumberExclusive);
                var modifiedAdditionalHeight = TakeWhile(modifiedLineHeightOverrides, modLineNumberExclusive);

                result.Add(new LineRangeAlignment(
                    originalRange,
                    modifiedRange,
                    originalRange.Length * origLineHeight + originalAdditionalHeight,
                    modifiedRange.Length * modLineHeight + modifiedAdditionalHeight,
                    c));

                lastOrigLineNumber = origLineNumberExclusive;
                lastModLineNumber = modLineNumberExclusive;
            }

            if (innerHunkAlignment)
            {
                foreach (var i in c.InnerChanges ?? [])
                {
                    if (i.OriginalRange.StartColumn > 1 && i.ModifiedRange.StartColumn > 1)
                    {
                        // There is some unmodified text on this line before the diff
                        EmitAlignment(i.OriginalRange.StartLineNumber, i.ModifiedRange.StartLineNumber);
                    }

                    // When the diff is invalid, the ranges might be out of bounds.
                    var maxColumn = i.OriginalRange.EndLineNumber <= originalLineCount ? getOriginalLineMaxColumn(i.OriginalRange.EndLineNumber) : int.MaxValue;
                    if (i.OriginalRange.EndColumn < maxColumn)
                    {
                        // There is some unmodified text on this line after the diff
                        EmitAlignment(i.OriginalRange.EndLineNumber, i.ModifiedRange.EndLineNumber);
                    }
                }
            }

            EmitAlignment(c.Original.EndLineNumberExclusive, c.Modified.EndLineNumberExclusive, true);

            lastOriginalLineNumber = c.Original.EndLineNumberExclusive;
            lastModifiedLineNumber = c.Modified.EndLineNumberExclusive;
        }

        HandleAlignmentsOutsideOfDiffs(int.MaxValue, int.MaxValue);

        return result;
    }

    /// <summary>Whether a change can be shown inline in the modified line itself (upstream's allowsTrueInlineDiffRendering).</summary>
    public static bool AllowsTrueInlineDiffRendering(DetailedLineRangeMapping mapping)
    {
        if (mapping.InnerChanges is null)
        {
            return false;
        }

        return mapping.InnerChanges.All(c =>
            (c.ModifiedRange.IsSingleLine && c.OriginalRange.IsSingleLine)
            || c.OriginalRange == new TextRange(1, 1, 1, 1));
    }
}
