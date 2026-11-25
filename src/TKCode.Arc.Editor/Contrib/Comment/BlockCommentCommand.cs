// Ported from vscode (src/vs/editor/contrib/comment/browser/blockCommentCommand.ts, commit b57d5c9e), MIT license.
// Differences from upstream: String.prototype.lastIndexOf / indexOf with a start index clamp it as JavaScript does
// (JsLastIndexOf / JsIndexOf).

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Comment;

/// <summary>Adds or removes a block comment around a selection (Shift+Alt+A).</summary>
internal sealed class BlockCommentCommand : ICommand
{
    private readonly TextSelection _selection;
    private readonly bool _insertSpace;
    private readonly LanguageConfigurationService _languageConfigurationService;
    private string? _usedEndToken;

    public BlockCommentCommand(TextSelection selection, bool insertSpace, LanguageConfigurationService languageConfigurationService)
    {
        _selection = selection;
        _insertSpace = insertSpace;
        _languageConfigurationService = languageConfigurationService;
        _usedEndToken = null;
    }

    /// <summary>Whether <paramref name="needle"/> is at <paramref name="offset"/> in <paramref name="haystack"/>, ignoring ASCII case.</summary>
    public static bool HaystackHasNeedleAtOffset(string haystack, string needle, int offset)
    {
        if (offset < 0)
        {
            return false;
        }
        var needleLength = needle.Length;
        var haystackLength = haystack.Length;
        if (offset + needleLength > haystackLength)
        {
            return false;
        }

        for (var i = 0; i < needleLength; i++)
        {
            int codeA = haystack[offset + i];
            int codeB = needle[i];

            if (codeA == codeB)
            {
                continue;
            }
            if (codeA >= 'A' && codeA <= 'Z' && codeA + 32 == codeB)
            {
                // codeA is upper-case variant of codeB
                continue;
            }
            if (codeB >= 'A' && codeB <= 'Z' && codeB + 32 == codeA)
            {
                // codeB is upper-case variant of codeA
                continue;
            }

            return false;
        }
        return true;
    }

    private void CreateOperationsForBlockComment(TextRange selection, string startToken, string endToken, bool insertSpace, TextModel model, IEditOperationBuilder builder)
    {
        var startLineNumber = selection.StartLineNumber;
        var startColumn = selection.StartColumn;
        var endLineNumber = selection.EndLineNumber;
        var endColumn = selection.EndColumn;

        var startLineText = model.GetLineContent(startLineNumber);
        var endLineText = model.GetLineContent(endLineNumber);

        var startTokenIndex = JsLastIndexOf(startLineText, startToken, startColumn - 1 + startToken.Length);
        var endTokenIndex = JsIndexOf(endLineText, endToken, endColumn - 1 - endToken.Length);

        if (startTokenIndex != -1 && endTokenIndex != -1)
        {
            if (startLineNumber == endLineNumber)
            {
                var lineBetweenTokens = JsSubstring(startLineText, startTokenIndex + startToken.Length, endTokenIndex);

                if (lineBetweenTokens.IndexOf(endToken, StringComparison.Ordinal) >= 0)
                {
                    // force to add a block comment
                    startTokenIndex = -1;
                    endTokenIndex = -1;
                }
            }
            else
            {
                var startLineAfterStartToken = JsSubstring(startLineText, startTokenIndex + startToken.Length, startLineText.Length);
                var endLineBeforeEndToken = JsSubstring(endLineText, 0, endTokenIndex);

                if (startLineAfterStartToken.IndexOf(endToken, StringComparison.Ordinal) >= 0 || endLineBeforeEndToken.IndexOf(endToken, StringComparison.Ordinal) >= 0)
                {
                    // force to add a block comment
                    startTokenIndex = -1;
                    endTokenIndex = -1;
                }
            }
        }

        IReadOnlyList<SingleEditOperation> ops;

        if (startTokenIndex != -1 && endTokenIndex != -1)
        {
            // Consider spaces as part of the comment tokens
            if (insertSpace && startTokenIndex + startToken.Length < startLineText.Length && startLineText[startTokenIndex + startToken.Length] == ' ')
            {
                // Pretend the start token contains a trailing space
                startToken += " ";
            }

            if (insertSpace && endTokenIndex > 0 && endLineText[endTokenIndex - 1] == ' ')
            {
                // Pretend the end token contains a leading space
                endToken = " " + endToken;
                endTokenIndex -= 1;
            }
            ops = CreateRemoveBlockCommentOperations(
                new TextRange(startLineNumber, startTokenIndex + startToken.Length + 1, endLineNumber, endTokenIndex + 1), startToken, endToken);
        }
        else
        {
            ops = CreateAddBlockCommentOperations(selection, startToken, endToken, _insertSpace);
            _usedEndToken = ops.Count == 1 ? endToken : null;
        }

        foreach (var op in ops)
        {
            builder.AddTrackedEditOperation(op.Range, op.Text);
        }
    }

    /// <summary>The edits that remove the tokens around <paramref name="r"/>.</summary>
    public static List<SingleEditOperation> CreateRemoveBlockCommentOperations(TextRange r, string startToken, string endToken)
    {
        var res = new List<SingleEditOperation>();

        if (!r.IsEmpty)
        {
            // Remove block comment start
            res.Add(EditOperation.Delete(new TextRange(
                r.StartLineNumber, r.StartColumn - startToken.Length,
                r.StartLineNumber, r.StartColumn)));

            // Remove block comment end
            res.Add(EditOperation.Delete(new TextRange(
                r.EndLineNumber, r.EndColumn,
                r.EndLineNumber, r.EndColumn + endToken.Length)));
        }
        else
        {
            // Remove both continuously
            res.Add(EditOperation.Delete(new TextRange(
                r.StartLineNumber, r.StartColumn - startToken.Length,
                r.EndLineNumber, r.EndColumn + endToken.Length)));
        }

        return res;
    }

    /// <summary>The edits that put the tokens around <paramref name="r"/>.</summary>
    public static List<SingleEditOperation> CreateAddBlockCommentOperations(TextRange r, string startToken, string endToken, bool insertSpace)
    {
        var res = new List<SingleEditOperation>();

        if (!r.IsEmpty)
        {
            // Insert block comment start
            res.Add(EditOperation.Insert(new TextPosition(r.StartLineNumber, r.StartColumn), startToken + (insertSpace ? " " : "")));

            // Insert block comment end
            res.Add(EditOperation.Insert(new TextPosition(r.EndLineNumber, r.EndColumn), (insertSpace ? " " : "") + endToken));
        }
        else
        {
            // Insert both continuously
            res.Add(EditOperation.Replace(new TextRange(
                r.StartLineNumber, r.StartColumn,
                r.EndLineNumber, r.EndColumn), startToken + "  " + endToken));
        }

        return res;
    }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        var startLineNumber = _selection.StartLineNumber;
        var startColumn = _selection.StartColumn;

        model.Tokenization.TokenizeIfCheap(startLineNumber);
        var languageId = model.GetLanguageIdAtPosition(startLineNumber, startColumn);
        var config = _languageConfigurationService.GetLanguageConfiguration(languageId).Comments;
        if (config is null || string.IsNullOrEmpty(config.BlockCommentStartToken) || string.IsNullOrEmpty(config.BlockCommentEndToken))
        {
            // Mode does not support block comments
            return;
        }

        CreateOperationsForBlockComment(_selection, config.BlockCommentStartToken, config.BlockCommentEndToken, _insertSpace, model, builder);
    }

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper)
    {
        var inverseEditOperations = helper.GetInverseEditOperations();
        if (inverseEditOperations.Count == 2)
        {
            var startTokenEditOperation = inverseEditOperations[0];
            var endTokenEditOperation = inverseEditOperations[1];

            return new TextSelection(
                startTokenEditOperation.Range.EndLineNumber,
                startTokenEditOperation.Range.EndColumn,
                endTokenEditOperation.Range.StartLineNumber,
                endTokenEditOperation.Range.StartColumn);
        }
        else
        {
            var srcRange = inverseEditOperations[0].Range;
            var deltaColumn = _usedEndToken is not null ? -_usedEndToken.Length - 1 : 0; // minus 1 space before endToken
            return new TextSelection(
                srcRange.EndLineNumber,
                srcRange.EndColumn + deltaColumn,
                srcRange.EndLineNumber,
                srcRange.EndColumn + deltaColumn);
        }
    }

    /// <summary>String.prototype.indexOf(search, position): the position is clamped to [0, length].</summary>
    internal static int JsIndexOf(string str, string search, int position)
    {
        position = Math.Clamp(position, 0, str.Length);
        return str.IndexOf(search, position, StringComparison.Ordinal);
    }

    /// <summary>String.prototype.lastIndexOf(search, position): the last match that starts at or before position.</summary>
    internal static int JsLastIndexOf(string str, string search, int position)
    {
        position = Math.Clamp(position, 0, str.Length);
        if (search.Length == 0)
        {
            return position;
        }
        var lastStart = Math.Min(position, str.Length - search.Length);
        if (lastStart < 0)
        {
            return -1;
        }
        // .NET's LastIndexOf(value, startIndex) looks at matches that end at or before startIndex.
        return str.LastIndexOf(search, lastStart + search.Length - 1, StringComparison.Ordinal);
    }

    /// <summary>String.prototype.substring(start, end): clamped and swapped as JavaScript does.</summary>
    internal static string JsSubstring(string str, int start, int end)
    {
        start = Math.Clamp(start, 0, str.Length);
        end = Math.Clamp(end, 0, str.Length);
        if (start > end)
        {
            (start, end) = (end, start);
        }
        return str.Substring(start, end - start);
    }
}
