// Ported from vscode (src/vs/editor/common/commands/trimTrailingWhitespaceCommand.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the module function trimTrailingWhitespace is a static method of the command;
//  - it sorts and reduces a copy of the cursors; upstream sorts and splices the caller's array in place.

using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Commands;

/// <summary>Removes the trailing whitespace of every line, except where a cursor sits in it.</summary>
internal sealed class TrimTrailingWhitespaceCommand : ICommand
{
    private readonly TextSelection _selection;
    private string? _selectionId;
    private readonly IReadOnlyList<TextPosition> _cursors;
    private readonly bool _trimInRegexesAndStrings;

    public TrimTrailingWhitespaceCommand(TextSelection selection, IReadOnlyList<TextPosition> cursors, bool trimInRegexesAndStrings)
    {
        _selection = selection;
        _cursors = cursors;
        _selectionId = null;
        _trimInRegexesAndStrings = trimInRegexesAndStrings;
    }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        var ops = TrimTrailingWhitespace(model, _cursors, _trimInRegexesAndStrings);
        for (int i = 0, len = ops.Count; i < len; i++)
        {
            var op = ops[i];

            builder.AddEditOperation(op.Range, op.Text);
        }

        _selectionId = builder.TrackSelection(_selection);
    }

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper)
    {
        return helper.GetTrackedSelection(_selectionId!);
    }

    /// <summary>
    /// Generate commands for trimming trailing whitespace on a model and ignore lines on which cursors are sitting.
    /// </summary>
    public static List<SingleEditOperation> TrimTrailingWhitespace(TextModel model, IReadOnlyList<TextPosition> cursorPositions, bool trimInRegexesAndStrings)
    {
        // Sort cursors ascending
        var cursors = cursorPositions.ToList();
        cursors.Sort((a, b) =>
        {
            if (a.LineNumber == b.LineNumber)
            {
                return a.Column - b.Column;
            }
            return a.LineNumber - b.LineNumber;
        });

        // Reduce multiple cursors on the same line and only keep the last one on the line
        for (var i = cursors.Count - 2; i >= 0; i--)
        {
            if (cursors[i].LineNumber == cursors[i + 1].LineNumber)
            {
                // Remove cursor at `i`
                cursors.RemoveAt(i);
            }
        }

        var r = new List<SingleEditOperation>();
        var cursorIndex = 0;
        var cursorLen = cursors.Count;

        for (int lineNumber = 1, lineCount = model.GetLineCount(); lineNumber <= lineCount; lineNumber++)
        {
            var lineContent = model.GetLineContent(lineNumber);
            var maxLineColumn = lineContent.Length + 1;
            var minEditColumn = 0;

            if (cursorIndex < cursorLen && cursors[cursorIndex].LineNumber == lineNumber)
            {
                minEditColumn = cursors[cursorIndex].Column;
                cursorIndex++;
                if (minEditColumn == maxLineColumn)
                {
                    // The cursor is at the end of the line => no edits for sure on this line
                    continue;
                }
            }

            if (lineContent.Length == 0)
            {
                continue;
            }

            var lastNonWhitespaceIndex = Strings.LastNonWhitespaceIndex(lineContent);

            int fromColumn;
            if (lastNonWhitespaceIndex == -1)
            {
                // Entire line is whitespace
                fromColumn = 1;
            }
            else if (lastNonWhitespaceIndex != lineContent.Length - 1)
            {
                // There is trailing whitespace
                fromColumn = lastNonWhitespaceIndex + 2;
            }
            else
            {
                // There is no trailing whitespace
                continue;
            }

            if (!trimInRegexesAndStrings)
            {
                if (!model.Tokenization.HasAccurateTokensForLine(lineNumber))
                {
                    // We don't want to force line tokenization, as that can be expensive, but we also don't want to trim
                    // trailing whitespace in lines that are not tokenized yet, as that can be wrong and trim whitespace from
                    // lines that the user requested we don't. So we bail out if the tokens are not accurate for this line.
                    continue;
                }

                var lineTokens = model.Tokenization.GetLineTokens(lineNumber);
                var fromColumnType = lineTokens.GetStandardTokenType(lineTokens.FindTokenIndexAtOffset(fromColumn));

                if (fromColumnType == StandardTokenType.String || fromColumnType == StandardTokenType.RegEx)
                {
                    continue;
                }
            }

            fromColumn = Math.Max(minEditColumn, fromColumn);
            r.Add(EditOperation.Delete(new TextRange(
                lineNumber, fromColumn,
                lineNumber, maxLineColumn)));
        }

        return r;
    }
}
