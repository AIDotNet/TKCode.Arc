// Ported from vscode (src/vs/editor/contrib/linkedEditing/browser/linkedEditing.ts: getLinkedEditingRanges and the
// edits of LinkedEditingContribution._syncRanges; commit b57d5c9e), MIT license.
// Differences: the engine half of linked editing. The edits that mirror the reference range into the other ranges are a
// pure function here (ComputeSyncEdits); the contribution in the Controls package keeps the decorations, listens to the
// editor and applies the edits. A provider that throws is reported to UnexpectedErrors (upstream's
// onUnexpectedExternalError). The word pattern check takes the first match, as JavaScript's String#match with a
// non-global expression: the reference text must be that match, whole.

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.LinkedEditing;

/// <summary>Linked editing: ranges that change together, such as an HTML tag's name in its start and end tag.</summary>
internal static class LinkedEditingSupport
{
    /// <summary>
    /// The ranges of the best provider that has some at <paramref name="position"/> (upstream's getLinkedEditingRanges:
    /// in score order, the first answer with ranges).
    /// </summary>
    public static async Task<LinkedEditingRanges?> GetLinkedEditingRanges(
        LanguageFeatureRegistry<ILinkedEditingRangeProvider> providers,
        TextModel model,
        TextPosition position,
        CancellationToken token)
    {
        foreach (var provider in providers.Ordered(model))
        {
            LinkedEditingRanges? result;
            try
            {
                result = await provider.ProvideLinkedEditingRanges(model, position, token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                UnexpectedErrors.Report(e);
                result = null;
            }

            if (result is { Ranges.Count: > 0 })
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// The edits that make every mirror range read as the reference range (upstream's _syncRanges): only the part between
    /// the common prefix and suffix of each mirror is replaced. Null when linked editing should stop: the reference spans
    /// several lines or is no longer one match of <paramref name="wordPattern"/>.
    /// </summary>
    public static List<SingleEditOperation>? ComputeSyncEdits(TextModel model, TextRange referenceRange, IReadOnlyList<TextRange?> mirrorRanges, Regex? wordPattern)
    {
        if (referenceRange.StartLineNumber != referenceRange.EndLineNumber)
        {
            return null;
        }

        var referenceValue = model.GetValueInRange(referenceRange);
        if (wordPattern is not null)
        {
            var match = wordPattern.Match(referenceValue);
            var matchLength = match.Success ? match.Length : 0;
            if (matchLength != referenceValue.Length)
            {
                return null;
            }
        }

        var edits = new List<SingleEditOperation>();
        foreach (var mirror in mirrorRanges)
        {
            if (mirror is not { } mirrorRange)
            {
                continue;
            }

            if (mirrorRange.StartLineNumber != mirrorRange.EndLineNumber)
            {
                edits.Add(new SingleEditOperation(mirrorRange, referenceValue));
            }
            else
            {
                var oldValue = model.GetValueInRange(mirrorRange);
                var newValue = referenceValue;
                var rangeStartColumn = mirrorRange.StartColumn;
                var rangeEndColumn = mirrorRange.EndColumn;

                var commonPrefixLength = CommonPrefixLength(oldValue, newValue);
                rangeStartColumn += commonPrefixLength;
                oldValue = oldValue[commonPrefixLength..];
                newValue = newValue[commonPrefixLength..];

                var commonSuffixLength = CommonSuffixLength(oldValue, newValue);
                rangeEndColumn -= commonSuffixLength;
                oldValue = oldValue[..(oldValue.Length - commonSuffixLength)];
                newValue = newValue[..(newValue.Length - commonSuffixLength)];

                if (rangeStartColumn != rangeEndColumn || newValue.Length != 0)
                {
                    edits.Add(new SingleEditOperation(
                        new TextRange(mirrorRange.StartLineNumber, rangeStartColumn, mirrorRange.EndLineNumber, rangeEndColumn),
                        newValue));
                }
            }
        }

        return edits;
    }

    // upstream's strings.commonPrefixLength
    private static int CommonPrefixLength(string a, string b)
    {
        var len = Math.Min(a.Length, b.Length);
        for (var i = 0; i < len; i++)
        {
            if (a[i] != b[i])
            {
                return i;
            }
        }

        return len;
    }

    // upstream's strings.commonSuffixLength
    private static int CommonSuffixLength(string a, string b)
    {
        var len = Math.Min(a.Length, b.Length);
        var aLastIndex = a.Length - 1;
        var bLastIndex = b.Length - 1;
        for (var i = 0; i < len; i++)
        {
            if (a[aLastIndex - i] != b[bLastIndex - i])
            {
                return i;
            }
        }

        return len;
    }
}
