// Ported from vscode (src/vs/editor/contrib/insertFinalNewLine/browser/insertFinalNewLineCommand.ts and the run method
// of InsertFinalNewLineAction in insertFinalNewLine.ts, commit b57d5c9e), MIT license.
// Differences from upstream: the action's run is the static InsertFinalNewLineCommand.Run on an IEditorActionHost.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.InsertFinalNewLine;

/// <summary>Inserts the model's end of line at the end of the document.</summary>
internal sealed class InsertFinalNewLineCommand : ICommand
{
    private readonly TextSelection _selection;
    private string? _selectionId;

    public InsertFinalNewLineCommand(TextSelection selection)
    {
        _selection = selection;
        _selectionId = null;
    }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        var op = InsertFinalNewLine(model);
        if (op is not null)
        {
            builder.AddEditOperation(op.Range, op.Text);
        }
        _selectionId = builder.TrackSelection(_selection);
    }

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper) =>
        helper.GetTrackedSelection(_selectionId!);

    /// <summary>Generate edit operations for inserting a final new line if needed; null if no edit is needed.</summary>
    public static SingleEditOperation? InsertFinalNewLine(TextModel model)
    {
        var lineCount = model.GetLineCount();
        return EditOperation.Insert(
            new TextPosition(lineCount, model.GetLineMaxColumn(lineCount)),
            model.GetEOL());
    }

    /// <summary>InsertFinalNewLineAction.run.</summary>
    public static void Run(IEditorActionHost editor, string id)
    {
        if (editor.GetSelection() is not { } selection)
        {
            return;
        }

        var command = new InsertFinalNewLineCommand(selection);

        editor.PushUndoStop();
        editor.ExecuteCommands(id, [command]);
        editor.PushUndoStop();
    }
}
