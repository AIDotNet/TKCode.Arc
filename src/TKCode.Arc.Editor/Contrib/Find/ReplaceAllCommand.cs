// Ported from vscode (src/vs/editor/contrib/find/browser/replaceAllCommand.ts, commit b57d5c9e), MIT license.
// Differences: none beyond the names (TextRange / TextSelection).

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Find;

/// <summary>Replaces many ranges in one edit, merging the ones that touch, and keeps the editor's selection (upstream's ReplaceAllCommand).</summary>
internal sealed class ReplaceAllCommand : ICommand
{
    private readonly TextSelection _editorSelection;
    private readonly IReadOnlyList<TextRange> _ranges;
    private readonly IReadOnlyList<string> _replaceStrings;
    private string? _trackedEditorSelectionId;

    public ReplaceAllCommand(TextSelection editorSelection, IReadOnlyList<TextRange> ranges, IReadOnlyList<string> replaceStrings)
    {
        _editorSelection = editorSelection;
        _ranges = ranges;
        _replaceStrings = replaceStrings;
    }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        if (_ranges.Count > 0)
        {
            // Collect all edit operations
            var ops = new List<(TextRange Range, string Text)>(_ranges.Count);
            for (var i = 0; i < _ranges.Count; i++)
            {
                ops.Add((_ranges[i], _replaceStrings[i]));
            }

            // Sort them in ascending order by range starts (stable, as upstream's Array.sort)
            ops = ops.OrderBy(static o => o.Range, Comparer<TextRange>.Create(TextRange.CompareRangesUsingStarts)).ToList();

            // Merge operations that touch each other
            var resultOps = new List<(TextRange Range, string Text)>();
            var previousOp = ops[0];
            for (var i = 1; i < ops.Count; i++)
            {
                if (previousOp.Range.EndLineNumber == ops[i].Range.StartLineNumber && previousOp.Range.EndColumn == ops[i].Range.StartColumn)
                {
                    // These operations are one after another and can be merged
                    previousOp = (previousOp.Range.PlusRange(ops[i].Range), previousOp.Text + ops[i].Text);
                }
                else
                {
                    resultOps.Add(previousOp);
                    previousOp = ops[i];
                }
            }

            resultOps.Add(previousOp);

            foreach (var op in resultOps)
            {
                builder.AddEditOperation(op.Range, op.Text);
            }
        }

        _trackedEditorSelectionId = builder.TrackSelection(_editorSelection);
    }

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper) =>
        helper.GetTrackedSelection(_trackedEditorSelectionId!);
}
