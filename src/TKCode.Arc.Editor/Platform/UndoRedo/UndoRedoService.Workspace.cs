// Ported from vscode (src/vs/platform/undoRedo/common/undoRedoService.ts, commit b57d5c9e), MIT license.
// This part covers workspace elements: verification before an undo or redo and its execution.

namespace TKCode.Arc.Editor.Platform;

internal sealed partial class UndoRedoService
{
    private WorkspaceVerificationError TryToSplitAndUndo(
        string strResource,
        WorkspaceStackElement element,
        RemovedResources? ignoreResources,
        string message)
    {
        if (element.CanSplit())
        {
            SplitPastWorkspaceElement(element, ignoreResources);
            _notificationService.Warn(message);
            return new WorkspaceVerificationError(UndoCore(strResource, 0, true));
        }
        else
        {
            // Cannot safely split this workspace element => flush all undo/redo stacks
            foreach (var resource in element.StrResources)
            {
                RemoveElements(resource);
            }
            _notificationService.Warn(message);
            return new WorkspaceVerificationError();
        }
    }

    private WorkspaceVerificationError? CheckWorkspaceUndo(
        string strResource,
        WorkspaceStackElement element,
        EditStackSnapshot editStackSnapshot,
        bool checkInvalidatedResources)
    {
        if (element.RemovedResources is not null)
        {
            return TryToSplitAndUndo(
                strResource,
                element,
                element.RemovedResources,
                string.Format(
                    UndoRedoStrings.CannotWorkspaceUndo,
                    element.Label,
                    element.RemovedResources.CreateMessage()));
        }
        if (checkInvalidatedResources && element.InvalidatedResources is not null)
        {
            return TryToSplitAndUndo(
                strResource,
                element,
                element.InvalidatedResources,
                string.Format(
                    UndoRedoStrings.CannotWorkspaceUndo,
                    element.Label,
                    element.InvalidatedResources.CreateMessage()));
        }

        // this must be the last past element in all the impacted resources!
        var cannotUndoDueToResources = new List<string>();
        foreach (var editStack in editStackSnapshot.EditStacks)
        {
            if (!ReferenceEquals(editStack.GetClosestPastElement(), element))
            {
                cannotUndoDueToResources.Add(editStack.ResourceLabel);
            }
        }
        if (cannotUndoDueToResources.Count > 0)
        {
            return TryToSplitAndUndo(
                strResource,
                element,
                null,
                string.Format(
                    UndoRedoStrings.CannotWorkspaceUndoDueToChanges,
                    element.Label,
                    string.Join(", ", cannotUndoDueToResources)));
        }

        var cannotLockDueToResources = new List<string>();
        foreach (var editStack in editStackSnapshot.EditStacks)
        {
            if (editStack.Locked)
            {
                cannotLockDueToResources.Add(editStack.ResourceLabel);
            }
        }
        if (cannotLockDueToResources.Count > 0)
        {
            return TryToSplitAndUndo(
                strResource,
                element,
                null,
                string.Format(
                    UndoRedoStrings.CannotWorkspaceUndoDueToInProgressUndoRedo,
                    element.Label,
                    string.Join(", ", cannotLockDueToResources)));
        }

        // check if new stack elements were added in the meantime...
        if (!editStackSnapshot.IsValid())
        {
            return TryToSplitAndUndo(
                strResource,
                element,
                null,
                string.Format(UndoRedoStrings.CannotWorkspaceUndoDueToInMeantimeUndoRedo, element.Label));
        }

        return null;
    }

    private ValueTask WorkspaceUndo(string strResource, WorkspaceStackElement element, bool undoConfirmed)
    {
        var affectedEditStacks = GetAffectedEditStacks(element);
        var verificationError = CheckWorkspaceUndo(
            strResource,
            element,
            affectedEditStacks,
            // invalidated resources will be checked after the prepare call
            false);
        if (verificationError is not null)
        {
            return verificationError.ReturnValue;
        }
        return ConfirmAndExecuteWorkspaceUndo(strResource, element, affectedEditStacks, undoConfirmed);
    }

    private WorkspaceVerificationError TryToSplitAndRedo(
        string strResource,
        WorkspaceStackElement element,
        RemovedResources? ignoreResources,
        string message)
    {
        if (element.CanSplit())
        {
            SplitFutureWorkspaceElement(element, ignoreResources);
            _notificationService.Warn(message);
            return new WorkspaceVerificationError(RedoCore(strResource));
        }
        else
        {
            // Cannot safely split this workspace element => flush all undo/redo stacks
            foreach (var resource in element.StrResources)
            {
                RemoveElements(resource);
            }
            _notificationService.Warn(message);
            return new WorkspaceVerificationError();
        }
    }

    private WorkspaceVerificationError? CheckWorkspaceRedo(
        string strResource,
        WorkspaceStackElement element,
        EditStackSnapshot editStackSnapshot,
        bool checkInvalidatedResources)
    {
        if (element.RemovedResources is not null)
        {
            return TryToSplitAndRedo(
                strResource,
                element,
                element.RemovedResources,
                string.Format(
                    UndoRedoStrings.CannotWorkspaceRedo,
                    element.Label,
                    element.RemovedResources.CreateMessage()));
        }
        if (checkInvalidatedResources && element.InvalidatedResources is not null)
        {
            return TryToSplitAndRedo(
                strResource,
                element,
                element.InvalidatedResources,
                string.Format(
                    UndoRedoStrings.CannotWorkspaceRedo,
                    element.Label,
                    element.InvalidatedResources.CreateMessage()));
        }

        // this must be the last future element in all the impacted resources!
        var cannotRedoDueToResources = new List<string>();
        foreach (var editStack in editStackSnapshot.EditStacks)
        {
            if (!ReferenceEquals(editStack.GetClosestFutureElement(), element))
            {
                cannotRedoDueToResources.Add(editStack.ResourceLabel);
            }
        }
        if (cannotRedoDueToResources.Count > 0)
        {
            return TryToSplitAndRedo(
                strResource,
                element,
                null,
                string.Format(
                    UndoRedoStrings.CannotWorkspaceRedoDueToChanges,
                    element.Label,
                    string.Join(", ", cannotRedoDueToResources)));
        }

        var cannotLockDueToResources = new List<string>();
        foreach (var editStack in editStackSnapshot.EditStacks)
        {
            if (editStack.Locked)
            {
                cannotLockDueToResources.Add(editStack.ResourceLabel);
            }
        }
        if (cannotLockDueToResources.Count > 0)
        {
            return TryToSplitAndRedo(
                strResource,
                element,
                null,
                string.Format(
                    UndoRedoStrings.CannotWorkspaceRedoDueToInProgressUndoRedo,
                    element.Label,
                    string.Join(", ", cannotLockDueToResources)));
        }

        // check if new stack elements were added in the meantime...
        if (!editStackSnapshot.IsValid())
        {
            return TryToSplitAndRedo(
                strResource,
                element,
                null,
                string.Format(UndoRedoStrings.CannotWorkspaceRedoDueToInMeantimeUndoRedo, element.Label));
        }

        return null;
    }

    private ValueTask WorkspaceRedo(string strResource, WorkspaceStackElement element)
    {
        var affectedEditStacks = GetAffectedEditStacks(element);
        var verificationError = CheckWorkspaceRedo(
            strResource,
            element,
            affectedEditStacks,
            // invalidated resources will be checked after the prepare call
            false);
        if (verificationError is not null)
        {
            return verificationError.ReturnValue;
        }
        return ExecuteWorkspaceRedo(strResource, element, affectedEditStacks);
    }

    private ValueTask ExecuteWorkspaceRedo(
        string strResource,
        WorkspaceStackElement element,
        EditStackSnapshot editStackSnapshot)
    {
        return ExecuteWorkspaceRedoCore(strResource, element, editStackSnapshot);
    }

    private async ValueTask ExecuteWorkspaceRedoCore(
        string strResource,
        WorkspaceStackElement element,
        EditStackSnapshot editStackSnapshot)
    {
        // prepare
        IDisposable cleanup;
        try
        {
            cleanup = await InvokeWorkspacePrepare(element).ConfigureAwait(false);
        }
        catch (Exception err)
        {
            OnError(err, element);
            return;
        }

        // At this point, it is possible that the element has been made invalid in the meantime (due to the prepare
        // await)
        var verificationError = CheckWorkspaceRedo(
            strResource,
            element,
            editStackSnapshot,
            // now also check that there are no more invalidated resources
            true);
        if (verificationError is not null)
        {
            cleanup.Dispose();
            await verificationError.ReturnValue.ConfigureAwait(false);
            return;
        }

        foreach (var editStack in editStackSnapshot.EditStacks)
        {
            editStack.MoveForward(element);
        }
        await SafeInvokeWithLocks(
            element,
            element.Actual.Redo,
            editStackSnapshot,
            cleanup,
            () => ContinueRedoInGroup(element.GroupId)).ConfigureAwait(false);
    }
}
