// Ported from vscode (src/vs/platform/undoRedo/common/undoRedoService.ts, commit b57d5c9e), MIT license.
// The engine has no localization yet; the English strings live here so they can be localized later. Upstream's nls
// templates became format strings with the same placeholders ({0}, {1}).

namespace TKCode.Arc.Editor.Platform;

internal static class UndoRedoStrings
{
    public const string ExternalRemoval = "The following files have been closed and modified on disk: {0}.";

    public const string NoParallelUniverses = "The following files have been modified in an incompatible way: {0}.";

    public const string CannotWorkspaceUndo = "Could not undo '{0}' across all files. {1}";

    public const string CannotWorkspaceUndoDueToChanges =
        "Could not undo '{0}' across all files because changes were made to {1}";

    public const string CannotWorkspaceUndoDueToInProgressUndoRedo =
        "Could not undo '{0}' across all files because there is already an undo or redo operation running on {1}";

    public const string CannotWorkspaceUndoDueToInMeantimeUndoRedo =
        "Could not undo '{0}' across all files because an undo or redo operation occurred in the meantime";

    public const string ConfirmWorkspace = "Would you like to undo '{0}' across all files?";

    public const string UndoInFiles = "Undo in {0} Files";

    public const string UndoThisFile = "Undo this File";

    public const string CannotResourceUndoDueToInProgressUndoRedo =
        "Could not undo '{0}' because there is already an undo or redo operation running.";

    public const string ConfirmDifferentSource = "Would you like to undo '{0}'?";

    public const string ConfirmDifferentSourceYes = "Yes";

    public const string ConfirmDifferentSourceNo = "No";

    public const string CannotWorkspaceRedo = "Could not redo '{0}' across all files. {1}";

    public const string CannotWorkspaceRedoDueToChanges =
        "Could not redo '{0}' across all files because changes were made to {1}";

    public const string CannotWorkspaceRedoDueToInProgressUndoRedo =
        "Could not redo '{0}' across all files because there is already an undo or redo operation running on {1}";

    public const string CannotWorkspaceRedoDueToInMeantimeUndoRedo =
        "Could not redo '{0}' across all files because an undo or redo operation occurred in the meantime";

    public const string CannotResourceRedoDueToInProgressUndoRedo =
        "Could not redo '{0}' because there is already an undo or redo operation running.";
}
