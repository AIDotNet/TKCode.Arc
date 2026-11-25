// Ported from vscode (src/vs/platform/undoRedo/common/undoRedoService.ts, commit b57d5c9e), MIT license.
// Only the members of IDialogService and INotificationService that the service calls are modelled here.

namespace TKCode.Arc.Editor.Platform;

internal enum UndoChoice
{
    All = 0,
    This = 1,
    Cancel = 2,
}

internal sealed record UndoRedoPromptOptions(string Message, string AllButtonLabel, string ThisButtonLabel);

internal sealed record UndoRedoConfirmOptions(string Message, string PrimaryButtonLabel, string CancelButtonLabel);

/// <summary>The dialog capabilities the undo-redo service needs; constructor-injected in place of vscode's DI.</summary>
internal interface IUndoRedoDialogService
{
    /// <summary>The three-choice prompt shown when a splittable workspace element can be undone.</summary>
    ValueTask<UndoChoice> PromptAsync(UndoRedoPromptOptions options);

    /// <summary>A yes/no confirmation, e.g. before undoing an element from another source.</summary>
    ValueTask<bool> ConfirmAsync(UndoRedoConfirmOptions options);
}

/// <summary>The notification capabilities the undo-redo service needs; constructor-injected in place of vscode's DI.</summary>
internal interface IUndoRedoNotificationService
{
    void Warn(string message);

    void Error(Exception error);
}
