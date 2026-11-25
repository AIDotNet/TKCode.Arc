// Ported from vscode (src/vs/editor/browser/coreCommands.ts: namespace CoreEditingCommands, commit b57d5c9e), MIT
// license.
// The registration layer is not ported (see CoreCommands.cs); the ids, preconditions and keybindings are in
// CoreCommands.Keybindings.cs.
// Differences from upstream:
//  - the editor is ICoreCommandsCodeEditor (the members of ICodeEditor these commands call); each command is a static
//    readonly field holding an immutable command object, and the anonymous classes are named after their field;
//  - Undo and Redo implement the 'undo' and 'redo' commands of editorExtensions.ts (UndoCommand, RedoCommand) for the
//    editor; their native text input fallback (EditorOrNativeTextInputCommand.runDOMCommand) is left out, and
//    RunEditorCommand returns the model's ValueTask (upstream's promise);
//  - the arguments these commands ignore are an opaque object.

using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Commands;

/// <summary>The editor's core editing commands: line break, indent and outdent, delete left and right, undo and redo.</summary>
internal static class CoreEditingCommands
{
    /// <summary>A command that edits through an editor's view model (upstream's CoreEditingCommand).</summary>
    internal abstract class CoreEditingCommand : ICoreCommand
    {
        protected CoreEditingCommand(string id)
        {
            Id = id;
        }

        /// <summary>The command's id.</summary>
        public string Id { get; }

        /// <summary>Runs the command on the editor; an editor without a view has no cursors.</summary>
        public void RunEditorCommand(ICoreCommandsCodeEditor editor, object? args)
        {
            var viewModel = editor.GetViewModel();
            if (viewModel is null)
            {
                // the editor has no view => has no cursors
                return;
            }
            RunCoreEditingCommand(editor, viewModel, args);
        }

        object? ICoreCommand.Run(ICoreCommandsCodeEditor editor, object? args)
        {
            RunEditorCommand(editor, args);
            return null;
        }

        /// <summary>Runs the command on <paramref name="editor"/> and its <paramref name="viewModel"/>.</summary>
        public abstract void RunCoreEditingCommand(ICoreCommandsCodeEditor editor, IViewModel viewModel, object? args);

        // viewModel.getCursorStates().map(s => s.modelState.selection)
        protected static TextSelection[] GetSelections(IViewModel viewModel)
        {
            var states = viewModel.GetCursorStates();
            var result = new TextSelection[states.Count];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = states[i].ModelState.Selection;
            }
            return result;
        }
    }

    private sealed class LineBreakInsertImpl : CoreEditingCommand
    {
        public LineBreakInsertImpl()
            : base("lineBreakInsert")
        {
        }

        public override void RunCoreEditingCommand(ICoreCommandsCodeEditor editor, IViewModel viewModel, object? args)
        {
            editor.PushUndoStop();
            editor.ExecuteCommands(Id, EnterOperation.LineBreakInsert(viewModel.CursorConfig, viewModel.Model, GetSelections(viewModel)));
        }
    }

    public static readonly CoreEditingCommand LineBreakInsert = new LineBreakInsertImpl();

    private sealed class OutdentImpl : CoreEditingCommand
    {
        public OutdentImpl()
            : base("outdent")
        {
        }

        public override void RunCoreEditingCommand(ICoreCommandsCodeEditor editor, IViewModel viewModel, object? args)
        {
            editor.PushUndoStop();
            editor.ExecuteCommands(Id, TypeOperations.Outdent(viewModel.CursorConfig, viewModel.Model, GetSelections(viewModel)));
            editor.PushUndoStop();
        }
    }

    public static readonly CoreEditingCommand Outdent = new OutdentImpl();

    private sealed class TabImpl : CoreEditingCommand
    {
        public TabImpl()
            : base("tab")
        {
        }

        public override void RunCoreEditingCommand(ICoreCommandsCodeEditor editor, IViewModel viewModel, object? args)
        {
            editor.PushUndoStop();
            editor.ExecuteCommands(Id, TypeOperations.Tab(viewModel.CursorConfig, viewModel.Model, GetSelections(viewModel)));
            editor.PushUndoStop();
        }
    }

    public static readonly CoreEditingCommand Tab = new TabImpl();

    private sealed class DeleteLeftImpl : CoreEditingCommand
    {
        public DeleteLeftImpl()
            : base("deleteLeft")
        {
        }

        public override void RunCoreEditingCommand(ICoreCommandsCodeEditor editor, IViewModel viewModel, object? args)
        {
            var (shouldPushStackElementBefore, commands) = DeleteOperations.DeleteLeft(
                viewModel.GetPrevEditOperationType(),
                viewModel.CursorConfig,
                viewModel.Model,
                GetSelections(viewModel),
                viewModel.GetCursorAutoClosedCharacters());
            if (shouldPushStackElementBefore)
            {
                editor.PushUndoStop();
            }
            editor.ExecuteCommands(Id, commands);
            viewModel.SetPrevEditOperationType(EditOperationType.DeletingLeft);
        }
    }

    public static readonly CoreEditingCommand DeleteLeft = new DeleteLeftImpl();

    private sealed class DeleteRightImpl : CoreEditingCommand
    {
        public DeleteRightImpl()
            : base("deleteRight")
        {
        }

        public override void RunCoreEditingCommand(ICoreCommandsCodeEditor editor, IViewModel viewModel, object? args)
        {
            var (shouldPushStackElementBefore, commands) = DeleteOperations.DeleteRight(
                viewModel.GetPrevEditOperationType(),
                viewModel.CursorConfig,
                viewModel.Model,
                GetSelections(viewModel));
            if (shouldPushStackElementBefore)
            {
                editor.PushUndoStop();
            }
            editor.ExecuteCommands(Id, commands);
            viewModel.SetPrevEditOperationType(EditOperationType.DeletingRight);
        }
    }

    public static readonly CoreEditingCommand DeleteRight = new DeleteRightImpl();

    /// <summary>Undo and redo: the model's undo or redo, unless the editor is read-only or has no model.</summary>
    internal sealed class UndoRedoImpl : ICoreCommand
    {
        private readonly bool _isRedo;

        public UndoRedoImpl(string id, bool isRedo)
        {
            Id = id;
            _isRedo = isRedo;
        }

        /// <summary>The command's id.</summary>
        public string Id { get; }

        public ValueTask RunEditorCommand(ICoreCommandsCodeEditor editor, object? args)
        {
            if (editor.GetModel() is not { } model || editor.IsReadOnly)
            {
                return ValueTask.CompletedTask;
            }
            return _isRedo ? model.Redo() : model.Undo();
        }

        object? ICoreCommand.Run(ICoreCommandsCodeEditor editor, object? args)
        {
            var result = RunEditorCommand(editor, args);
            return result.IsCompletedSuccessfully ? null : result.AsTask();
        }
    }

    public static readonly UndoRedoImpl Undo = new("undo", isRedo: false);

    public static readonly UndoRedoImpl Redo = new("redo", isRedo: true);
}
