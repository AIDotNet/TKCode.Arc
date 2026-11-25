// Ported from vscode (src/vs/editor/common/services/editorWebWorker.ts: EditorWorker.$computeMoreMinimalEdits and
// _diffLimit; src/vs/editor/browser/services/editorWorkerService.ts: computeMoreMinimalEdits; commit b57d5c9e), MIT
// license.
// Differences:
//  - there is no web worker and no mirror model: the edits are computed against the text model itself, on the calling
//    thread (formatting edits are small; the diff is bounded by DiffLimit);
//  - an edit without a range (upstream: range undefined, only eol set) is a TextEdit whose Range is default(TextRange);
//    the EOL edit at the end has that range too (upstream: 0,0,0,0);
//  - the input edits are not mutated (upstream merges adjacent edits in place in a copy of the array, changing the
//    objects it was given).

using TKCode.Arc.Editor.Diff;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Services;

/// <summary>The computations upstream runs in the editor worker.</summary>
internal static partial class EditorWorker
{
    /// <summary>Above this many characters (old or new) an edit is kept as it is instead of being diffed.</summary>
    public const int DiffLimit = 100000;

    /// <summary>
    /// Makes formatting edits smaller (upstream's computeMoreMinimalEdits): merges touching edits, drops edits that change
    /// nothing, and replaces each edit by the character changes between the old and the new text, so that markers,
    /// decorations and the cursor outside the real changes stay where they were. Line breaks in the new text become the
    /// model's. Returns null for null edits, as upstream.
    /// </summary>
    public static IReadOnlyList<TextEdit>? ComputeMoreMinimalEdits(TextModel model, IReadOnlyList<TextEdit>? edits, bool pretty = false)
    {
        if (edits is null || edits.Count == 0)
        {
            return edits;
        }

        var result = new List<TextEdit>();
        EndOfLineSequence? lastEol = null;

        // eol only changes should go to the end
        var sorted = edits.Select((edit, index) => (Edit: edit, Index: index))
            .OrderBy(static e => HasRange(e.Edit) ? 0 : 1)
            .ThenBy(static e => e.Edit.Range, Comparer<TextRange>.Create(TextRange.CompareRangesUsingStarts))
            .ThenBy(static e => e.Index)
            .Select(static e => (Range: e.Edit.Range, Text: e.Edit.Text, Eol: e.Edit.Eol))
            .ToList();

        // merge adjacent edits
        var writeIndex = 0;
        for (var readIndex = 1; readIndex < sorted.Count; readIndex++)
        {
            if (sorted[writeIndex].Range.End == sorted[readIndex].Range.Start)
            {
                var merged = sorted[writeIndex];
                sorted[writeIndex] = (new TextRange(merged.Range.Start, sorted[readIndex].Range.End), merged.Text + sorted[readIndex].Text, merged.Eol);
            }
            else
            {
                writeIndex++;
                sorted[writeIndex] = sorted[readIndex];
            }
        }

        sorted.RemoveRange(writeIndex + 1, sorted.Count - writeIndex - 1);

        foreach (var (range, rawText, eol) in sorted)
        {
            if (eol is { } e)
            {
                lastEol = e;
            }

            if (!HasRange(range) || (range.IsEmpty && string.IsNullOrEmpty(rawText)))
            {
                // empty change
                continue;
            }

            var original = model.GetValueInRange(range);
            var text = NormalizeEol(rawText ?? string.Empty, model.GetEOL());

            if (original == text)
            {
                // noop
                continue;
            }

            // make sure diff won't take too long
            if (Math.Max(text.Length, original.Length) > DiffLimit)
            {
                result.Add(new TextEdit(range, text));
                continue;
            }

            // compute diff between original and edit.text
            var changes = StringDiff.Compute(original, text, pretty);
            var editOffset = model.GetOffsetAt(range.Start);
            var newEdits = new List<TextEdit>();
            var useOriginalEdit = false;

            foreach (var change in changes)
            {
                var originalEnd = change.OriginalStart + change.OriginalLength;
                var start = model.GetPositionAt(editOffset + change.OriginalStart);
                var end = model.GetPositionAt(editOffset + originalEnd);
                var startLineContent = model.GetLineContent(start.LineNumber);
                var endLineContent = model.GetLineContent(end.LineNumber);
                var startFollowsHighSurrogate = start.Column > 1 && Strings.IsHighSurrogate(startLineContent[start.Column - 2]);
                var endFollowsHighSurrogate = end.Column > 1 && end.Column <= endLineContent.Length && Strings.IsHighSurrogate(endLineContent[end.Column - 2]);
                if (startFollowsHighSurrogate || endFollowsHighSurrogate)
                {
                    // the model moved an offset inside a surrogate pair: replacing there would split it
                    useOriginalEdit = true;
                    break;
                }

                var newEdit = new TextEdit(
                    new TextRange(start.LineNumber, start.Column, end.LineNumber, end.Column),
                    text.Substring(change.ModifiedStart, change.ModifiedLength));

                if (model.GetValueInRange(newEdit.Range) != newEdit.Text)
                {
                    newEdits.Add(newEdit);
                }
            }

            if (useOriginalEdit)
            {
                result.Add(new TextEdit(range, text));
            }
            else
            {
                result.AddRange(newEdits);
            }
        }

        if (lastEol is { } lastEolValue)
        {
            result.Add(new TextEdit(default, string.Empty) { Eol = lastEolValue });
        }

        return result;
    }

    /// <summary>Whether an edit has a range (an EOL-only edit has <c>default(TextRange)</c>, upstream's undefined range).</summary>
    public static bool HasRange(TextEdit edit) => HasRange(edit.Range);

    private static bool HasRange(TextRange range) => range.StartLineNumber > 0;

    // upstream: text.replace(/\r\n|\n|\r/g, model.eol)
    private static string NormalizeEol(string text, string eol)
    {
        if (text.IndexOfAny(['\r', '\n']) < 0)
        {
            return text;
        }

        var builder = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\r')
            {
                builder.Append(eol);
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
            }
            else if (ch == '\n')
            {
                builder.Append(eol);
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}
