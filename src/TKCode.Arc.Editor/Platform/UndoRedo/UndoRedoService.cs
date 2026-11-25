// Ported from vscode (src/vs/platform/undoRedo/common/undoRedoService.ts, commit b57d5c9e), MIT license.

namespace TKCode.Arc.Editor.Platform;

/// <summary>
/// The undo-redo stacks of every resource, with resource groups, sources, snapshots and confirmation.
/// <para>
/// Every operation returns a <see cref="ValueTask"/>. When the elements it calls return completed tasks (and the
/// dialogs answer synchronously), the whole operation completes synchronously: the returned task is already
/// completed, the stacks are already moved and the locks already released. Only a genuinely asynchronous element or
/// dialog makes the operation continue later.
/// </para>
/// </summary>
internal sealed partial class UndoRedoService : IUndoRedoService
{
    private readonly Dictionary<string, ResourceEditStack> _editStacks;
    private readonly List<UriComparisonKeyEntry> _uriComparisonKeyComputers;
    private readonly IUndoRedoDialogService _dialogService;
    private readonly IUndoRedoNotificationService _notificationService;

    public UndoRedoService(
        IUndoRedoDialogService dialogService,
        IUndoRedoNotificationService notificationService)
    {
        _dialogService = dialogService;
        _notificationService = notificationService;
        _editStacks = new Dictionary<string, ResourceEditStack>();
        _uriComparisonKeyComputers = new List<UriComparisonKeyEntry>();
    }

    private static string GetResourceLabel(Uri resource) => resource.IsFile ? resource.LocalPath : resource.AbsolutePath;

    public IDisposable RegisterUriComparisonKeyComputer(
        string scheme,
        UriComparisonKeyComputer uriComparisonKeyComputer)
    {
        var entry = new UriComparisonKeyEntry(scheme, uriComparisonKeyComputer);
        _uriComparisonKeyComputers.Add(entry);
        return new UriComparisonKeyRegistration(this, entry);
    }

    public string GetUriComparisonKey(Uri resource)
    {
        foreach (var uriComparisonKeyComputer in _uriComparisonKeyComputers)
        {
            if (uriComparisonKeyComputer.Scheme == resource.Scheme)
            {
                return uriComparisonKeyComputer.Computer.GetComparisonKey(resource);
            }
        }
        return resource.ToString();
    }

    public void PushElement(
        IUndoRedoElement element,
        UndoRedoGroup? group = null,
        UndoRedoSource? source = null)
    {
        group ??= UndoRedoGroup.None;
        source ??= UndoRedoSource.None;

        if (element.Type == UndoRedoElementType.Resource)
        {
            var resource = ((IResourceUndoRedoElement)element).Resource;
            var resourceLabel = GetResourceLabel(resource);
            var strResource = GetUriComparisonKey(resource);
            PushElementInternal(new ResourceStackElement(
                element,
                resourceLabel,
                strResource,
                group.Id,
                group.NextOrder(),
                source.Id,
                source.NextOrder()));
        }
        else
        {
            var seen = new HashSet<string>();
            var resourceLabels = new List<string>();
            var strResources = new List<string>();
            foreach (var resource in ((IWorkspaceUndoRedoElement)element).Resources)
            {
                var resourceLabel = GetResourceLabel(resource);
                var strResource = GetUriComparisonKey(resource);

                if (seen.Contains(strResource))
                {
                    continue;
                }
                seen.Add(strResource);
                resourceLabels.Add(resourceLabel);
                strResources.Add(strResource);
            }

            if (resourceLabels.Count == 1)
            {
                PushElementInternal(new ResourceStackElement(
                    element,
                    resourceLabels[0],
                    strResources[0],
                    group.Id,
                    group.NextOrder(),
                    source.Id,
                    source.NextOrder()));
            }
            else
            {
                PushElementInternal(new WorkspaceStackElement(
                    element,
                    resourceLabels.ToArray(),
                    strResources.ToArray(),
                    group.Id,
                    group.NextOrder(),
                    source.Id,
                    source.NextOrder()));
            }
        }
    }

    private void PushElementInternal(StackElement element)
    {
        for (var i = 0; i < element.StrResources.Count; i++)
        {
            var resourceLabel = element.ResourceLabels[i];
            var strResource = element.StrResources[i];

            if (!_editStacks.TryGetValue(strResource, out var editStack))
            {
                editStack = new ResourceEditStack(resourceLabel, strResource);
                _editStacks.Add(strResource, editStack);
            }

            editStack.PushElement(element);
        }
    }

    public IUndoRedoElement? GetLastElement(Uri resource)
    {
        var strResource = GetUriComparisonKey(resource);
        if (_editStacks.TryGetValue(strResource, out var editStack))
        {
            if (editStack.HasFutureElements())
            {
                return null;
            }
            var closestPastElement = editStack.GetClosestPastElement();
            return closestPastElement?.Actual;
        }
        return null;
    }

    private void SplitPastWorkspaceElement(WorkspaceStackElement toRemove, RemovedResources? ignoreResources)
    {
        var individualArr = ((ISplittableWorkspaceUndoRedoElement)toRemove.Actual).Split();
        var individualMap = new Dictionary<string, ResourceStackElement>();
        foreach (var element in individualArr)
        {
            var resourceLabel = GetResourceLabel(element.Resource);
            var strResource = GetUriComparisonKey(element.Resource);
            var stackElement = new ResourceStackElement(element, resourceLabel, strResource, 0, 0, 0, 0);
            individualMap[stackElement.StrResource] = stackElement;
        }

        foreach (var strResource in toRemove.StrResources)
        {
            if (ignoreResources is not null && ignoreResources.Has(strResource))
            {
                continue;
            }
            _editStacks[strResource].SplitPastWorkspaceElement(toRemove, individualMap);
        }
    }

    private void SplitFutureWorkspaceElement(WorkspaceStackElement toRemove, RemovedResources? ignoreResources)
    {
        var individualArr = ((ISplittableWorkspaceUndoRedoElement)toRemove.Actual).Split();
        var individualMap = new Dictionary<string, ResourceStackElement>();
        foreach (var element in individualArr)
        {
            var resourceLabel = GetResourceLabel(element.Resource);
            var strResource = GetUriComparisonKey(element.Resource);
            var stackElement = new ResourceStackElement(element, resourceLabel, strResource, 0, 0, 0, 0);
            individualMap[stackElement.StrResource] = stackElement;
        }

        foreach (var strResource in toRemove.StrResources)
        {
            if (ignoreResources is not null && ignoreResources.Has(strResource))
            {
                continue;
            }
            _editStacks[strResource].SplitFutureWorkspaceElement(toRemove, individualMap);
        }
    }

    public void RemoveElements(Uri resource) => RemoveElements(GetUriComparisonKey(resource));

    public void RemoveElements(string strResource)
    {
        if (_editStacks.TryGetValue(strResource, out var editStack))
        {
            editStack.Dispose();
            _editStacks.Remove(strResource);
        }
    }

    public void SetElementsValidFlag(Uri resource, bool isValid, Func<IUndoRedoElement, bool> filter)
    {
        var strResource = GetUriComparisonKey(resource);
        if (_editStacks.TryGetValue(strResource, out var editStack))
        {
            editStack.SetElementsValidFlag(isValid, filter);
        }
    }

    public bool HasElements(Uri resource)
    {
        var strResource = GetUriComparisonKey(resource);
        if (_editStacks.TryGetValue(strResource, out var editStack))
        {
            return editStack.HasPastElements() || editStack.HasFutureElements();
        }
        return false;
    }

    public ResourceEditStackSnapshot CreateSnapshot(Uri resource)
    {
        var strResource = GetUriComparisonKey(resource);
        if (_editStacks.TryGetValue(strResource, out var editStack))
        {
            return editStack.CreateSnapshot(resource);
        }
        return new ResourceEditStackSnapshot(resource, Array.Empty<int>());
    }

    public void RestoreSnapshot(ResourceEditStackSnapshot snapshot)
    {
        var strResource = GetUriComparisonKey(snapshot.Resource);
        if (_editStacks.TryGetValue(strResource, out var editStack))
        {
            editStack.RestoreSnapshot(snapshot);

            if (!editStack.HasPastElements() && !editStack.HasFutureElements())
            {
                // the edit stack is now empty, just remove it entirely
                editStack.Dispose();
                _editStacks.Remove(strResource);
            }
        }
    }

    public IPastFutureElements GetElements(Uri resource)
    {
        var strResource = GetUriComparisonKey(resource);
        if (_editStacks.TryGetValue(strResource, out var editStack))
        {
            return editStack.GetElements();
        }
        return new PastFutureElements(Array.Empty<IUndoRedoElement>(), Array.Empty<IUndoRedoElement>());
    }

    private (StackElement? Element, string? StrResource) FindClosestUndoElementWithSource(int sourceId)
    {
        if (sourceId == 0)
        {
            return (null, null);
        }

        // find an element with the sourceId and with the highest sourceOrder ready to be undone
        StackElement? matchedElement = null;
        string? matchedStrResource = null;

        foreach (var (strResource, editStack) in _editStacks)
        {
            var candidate = editStack.GetClosestPastElement();
            if (candidate is null)
            {
                continue;
            }
            if (candidate.SourceId == sourceId)
            {
                if (matchedElement is null || candidate.SourceOrder > matchedElement.SourceOrder)
                {
                    matchedElement = candidate;
                    matchedStrResource = strResource;
                }
            }
        }

        return (matchedElement, matchedStrResource);
    }

    public bool CanUndo(Uri resource)
    {
        var strResource = GetUriComparisonKey(resource);
        if (_editStacks.TryGetValue(strResource, out var editStack))
        {
            return editStack.HasPastElements();
        }
        return false;
    }

    public bool CanUndo(UndoRedoSource source)
    {
        var (_, matchedStrResource) = FindClosestUndoElementWithSource(source.Id);
        return matchedStrResource is not null;
    }

    private void OnError(Exception err, StackElement element)
    {
        // An error occurred while undoing or redoing => drop the undo/redo stack for all affected resources
        foreach (var strResource in element.StrResources)
        {
            RemoveElements(strResource);
        }
        _notificationService.Error(err);
    }

    private static Action AcquireLocks(EditStackSnapshot editStackSnapshot)
    {
        // first, check if all locks can be acquired
        foreach (var editStack in editStackSnapshot.EditStacks)
        {
            if (editStack.Locked)
            {
                throw new InvalidOperationException("Cannot acquire edit stack lock");
            }
        }

        // can acquire all locks
        foreach (var editStack in editStackSnapshot.EditStacks)
        {
            editStack.Locked = true;
        }

        return () =>
        {
            // release all locks
            foreach (var editStack in editStackSnapshot.EditStacks)
            {
                editStack.Locked = false;
            }
        };
    }

    private ValueTask SafeInvokeWithLocks(
        StackElement element,
        Func<ValueTask> invoke,
        EditStackSnapshot editStackSnapshot,
        IDisposable cleanup,
        Func<ValueTask> continuation)
    {
        var releaseLocks = AcquireLocks(editStackSnapshot);

        ValueTask result;
        try
        {
            result = invoke();
        }
        catch (Exception err)
        {
            releaseLocks();
            cleanup.Dispose();
            OnError(err, element);
            return ValueTask.CompletedTask;
        }

        if (result.IsCompletedSuccessfully)
        {
            // result is already done
            releaseLocks();
            cleanup.Dispose();
            return continuation();
        }

        return AwaitInvoke(result, element, releaseLocks, cleanup, continuation);
    }

    private async ValueTask AwaitInvoke(
        ValueTask result,
        StackElement element,
        Action releaseLocks,
        IDisposable cleanup,
        Func<ValueTask> continuation)
    {
        try
        {
            await result.ConfigureAwait(false);
        }
        catch (Exception err)
        {
            releaseLocks();
            cleanup.Dispose();
            OnError(err, element);
            return;
        }

        releaseLocks();
        cleanup.Dispose();
        await continuation().ConfigureAwait(false);
    }

    private ValueTask<IDisposable> InvokeWorkspacePrepare(WorkspaceStackElement element)
    {
        var result = ((IWorkspaceUndoRedoElement)element.Actual).PrepareUndoRedo();
        if (result.IsCompletedSuccessfully)
        {
            return ValueTask.FromResult(result.Result ?? UndoRedoDisposable.None);
        }
        return AwaitWorkspacePrepare(result);
    }

    private static async ValueTask<IDisposable> AwaitWorkspacePrepare(ValueTask<IDisposable?> result)
    {
        var disposable = await result.ConfigureAwait(false);
        return disposable ?? UndoRedoDisposable.None;
    }

    private ValueTask InvokeResourcePrepare(
        ResourceStackElement element,
        Func<IDisposable, ValueTask> callback)
    {
        if (element.Actual is not IWorkspaceUndoRedoElement workspaceElement)
        {
            // no preparation needed
            return callback(UndoRedoDisposable.None);
        }

        var result = workspaceElement.PrepareUndoRedo();
        if (result.IsCompletedSuccessfully)
        {
            // nothing to clean up or already has the disposable
            return callback(result.Result ?? UndoRedoDisposable.None);
        }

        return AwaitResourcePrepare(result, callback);
    }

    private static async ValueTask AwaitResourcePrepare(
        ValueTask<IDisposable?> result,
        Func<IDisposable, ValueTask> callback)
    {
        var disposable = await result.ConfigureAwait(false);
        await callback(disposable ?? UndoRedoDisposable.None).ConfigureAwait(false);
    }

    private EditStackSnapshot GetAffectedEditStacks(WorkspaceStackElement element)
    {
        var affectedEditStacks = new List<ResourceEditStack>(element.StrResources.Count);
        foreach (var strResource in element.StrResources)
        {
            affectedEditStacks.Add(
                _editStacks.TryGetValue(strResource, out var editStack)
                    ? editStack
                    : new ResourceEditStack(string.Empty, string.Empty) { Locked = true });
        }
        return new EditStackSnapshot(affectedEditStacks);
    }

    private bool IsPartOfUndoGroup(WorkspaceStackElement element)
    {
        if (element.GroupId == 0)
        {
            return false;
        }
        // check that there is at least another element with the same groupId ready to be undone
        foreach (var editStack in _editStacks.Values)
        {
            var pastElement = editStack.GetClosestPastElement();
            if (pastElement is null)
            {
                continue;
            }
            if (ReferenceEquals(pastElement, element))
            {
                var secondPastElement = editStack.GetSecondClosestPastElement();
                if (secondPastElement is not null && secondPastElement.GroupId == element.GroupId)
                {
                    // there is another element with the same group id in the same stack!
                    return true;
                }
            }
            if (pastElement.GroupId == element.GroupId)
            {
                // there is another element with the same group id in another stack!
                return true;
            }
        }
        return false;
    }

    private ValueTask ConfirmAndContinueUndo(string strResource, int sourceId, StackElement element)
    {
        return ConfirmAndContinueUndoCore(strResource, sourceId, element);
    }

    private async ValueTask ConfirmAndContinueUndoCore(string strResource, int sourceId, StackElement element)
    {
        var confirmed = await _dialogService.ConfirmAsync(new UndoRedoConfirmOptions(
            string.Format(UndoRedoStrings.ConfirmDifferentSource, element.Label),
            UndoRedoStrings.ConfirmDifferentSourceYes,
            UndoRedoStrings.ConfirmDifferentSourceNo)).ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        await UndoCore(strResource, sourceId, true).ConfigureAwait(false);
    }

    private ValueTask ConfirmAndExecuteWorkspaceUndo(
        string strResource,
        WorkspaceStackElement element,
        EditStackSnapshot editStackSnapshot,
        bool undoConfirmed)
    {
        return ConfirmAndExecuteWorkspaceUndoCore(strResource, element, editStackSnapshot, undoConfirmed);
    }

    private async ValueTask ConfirmAndExecuteWorkspaceUndoCore(
        string strResource,
        WorkspaceStackElement element,
        EditStackSnapshot editStackSnapshot,
        bool undoConfirmed)
    {
        if (element.CanSplit() && !IsPartOfUndoGroup(element))
        {
            // this element can be split
            var result = await _dialogService.PromptAsync(new UndoRedoPromptOptions(
                string.Format(UndoRedoStrings.ConfirmWorkspace, element.Label),
                string.Format(UndoRedoStrings.UndoInFiles, editStackSnapshot.EditStacks.Count),
                UndoRedoStrings.UndoThisFile)).ConfigureAwait(false);

            if (result == UndoChoice.Cancel)
            {
                // choice: cancel
                return;
            }

            if (result == UndoChoice.This)
            {
                // choice: undo this file
                SplitPastWorkspaceElement(element, null);
                await UndoCore(strResource, 0, true).ConfigureAwait(false);
                return;
            }

            // choice: undo in all files

            // At this point, it is possible that the element has been made invalid in the meantime (due to the
            // confirmation await)
            var verificationError1 = CheckWorkspaceUndo(strResource, element, editStackSnapshot, false);
            if (verificationError1 is not null)
            {
                await verificationError1.ReturnValue.ConfigureAwait(false);
                return;
            }

            undoConfirmed = true;
        }

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
        var verificationError2 = CheckWorkspaceUndo(
            strResource,
            element,
            editStackSnapshot,
            // now also check that there are no more invalidated resources
            true);
        if (verificationError2 is not null)
        {
            cleanup.Dispose();
            await verificationError2.ReturnValue.ConfigureAwait(false);
            return;
        }

        foreach (var editStack in editStackSnapshot.EditStacks)
        {
            editStack.MoveBackward(element);
        }
        await SafeInvokeWithLocks(
            element,
            element.Actual.Undo,
            editStackSnapshot,
            cleanup,
            () => ContinueUndoInGroup(element.GroupId, undoConfirmed)).ConfigureAwait(false);
    }

    private ValueTask ResourceUndo(ResourceEditStack editStack, ResourceStackElement element, bool undoConfirmed)
    {
        if (!element.IsValid)
        {
            // invalid element => immediately flush edit stack!
            editStack.FlushAllElements();
            return ValueTask.CompletedTask;
        }
        if (editStack.Locked)
        {
            var message = string.Format(
                UndoRedoStrings.CannotResourceUndoDueToInProgressUndoRedo,
                element.Label);
            _notificationService.Warn(message);
            return ValueTask.CompletedTask;
        }
        return InvokeResourcePrepare(element, cleanup =>
        {
            editStack.MoveBackward(element);
            return SafeInvokeWithLocks(
                element,
                element.Actual.Undo,
                new EditStackSnapshot(new[] { editStack }),
                cleanup,
                () => ContinueUndoInGroup(element.GroupId, undoConfirmed));
        });
    }

    private (StackElement? Element, string? StrResource) FindClosestUndoElementInGroup(int groupId)
    {
        if (groupId == 0)
        {
            return (null, null);
        }

        // find another element with the same groupId and with the highest groupOrder ready to be undone
        StackElement? matchedElement = null;
        string? matchedStrResource = null;

        foreach (var (strResource, editStack) in _editStacks)
        {
            var candidate = editStack.GetClosestPastElement();
            if (candidate is null)
            {
                continue;
            }
            if (candidate.GroupId == groupId)
            {
                if (matchedElement is null || candidate.GroupOrder > matchedElement.GroupOrder)
                {
                    matchedElement = candidate;
                    matchedStrResource = strResource;
                }
            }
        }

        return (matchedElement, matchedStrResource);
    }

    private ValueTask ContinueUndoInGroup(int groupId, bool undoConfirmed)
    {
        if (groupId == 0)
        {
            return ValueTask.CompletedTask;
        }

        var (_, matchedStrResource) = FindClosestUndoElementInGroup(groupId);
        if (matchedStrResource is not null)
        {
            return UndoCore(matchedStrResource, 0, undoConfirmed);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask Undo(Uri resource) => UndoCore(GetUriComparisonKey(resource), 0, false);

    public ValueTask Undo(UndoRedoSource source)
    {
        var (_, matchedStrResource) = FindClosestUndoElementWithSource(source.Id);
        return matchedStrResource is not null ? UndoCore(matchedStrResource, source.Id, false) : ValueTask.CompletedTask;
    }

    private ValueTask UndoCore(string strResource, int sourceId = 0, bool undoConfirmed = false)
    {
        if (!_editStacks.TryGetValue(strResource, out var editStack))
        {
            return ValueTask.CompletedTask;
        }

        var element = editStack.GetClosestPastElement();
        if (element is null)
        {
            return ValueTask.CompletedTask;
        }

        if (element.GroupId != 0)
        {
            // this element is a part of a group, we need to make sure undoing in a group is in order
            var (matchedElement, matchedStrResource) = FindClosestUndoElementInGroup(element.GroupId);
            if (!ReferenceEquals(element, matchedElement) && matchedStrResource is not null)
            {
                // there is an element in the same group that should be undone before this one
                return UndoCore(matchedStrResource, sourceId, undoConfirmed);
            }
        }

        var shouldPromptForConfirmation = element.SourceId != sourceId || element.ConfirmBeforeUndo;
        if (shouldPromptForConfirmation && !undoConfirmed)
        {
            // Hit a different source or the element asks for prompt before undo, prompt for confirmation
            return ConfirmAndContinueUndo(strResource, sourceId, element);
        }

        if (element.Type == UndoRedoElementType.Workspace)
        {
            return WorkspaceUndo(strResource, (WorkspaceStackElement)element, undoConfirmed);
        }
        return ResourceUndo(editStack, (ResourceStackElement)element, undoConfirmed);
    }

    private (StackElement? Element, string? StrResource) FindClosestRedoElementWithSource(int sourceId)
    {
        if (sourceId == 0)
        {
            return (null, null);
        }

        // find an element with sourceId and with the lowest sourceOrder ready to be redone
        StackElement? matchedElement = null;
        string? matchedStrResource = null;

        foreach (var (strResource, editStack) in _editStacks)
        {
            var candidate = editStack.GetClosestFutureElement();
            if (candidate is null)
            {
                continue;
            }
            if (candidate.SourceId == sourceId)
            {
                if (matchedElement is null || candidate.SourceOrder < matchedElement.SourceOrder)
                {
                    matchedElement = candidate;
                    matchedStrResource = strResource;
                }
            }
        }

        return (matchedElement, matchedStrResource);
    }

    public bool CanRedo(Uri resource)
    {
        var strResource = GetUriComparisonKey(resource);
        if (_editStacks.TryGetValue(strResource, out var editStack))
        {
            return editStack.HasFutureElements();
        }
        return false;
    }

    public bool CanRedo(UndoRedoSource source)
    {
        var (_, matchedStrResource) = FindClosestRedoElementWithSource(source.Id);
        return matchedStrResource is not null;
    }

    private ValueTask ResourceRedo(ResourceEditStack editStack, ResourceStackElement element)
    {
        if (!element.IsValid)
        {
            // invalid element => immediately flush edit stack!
            editStack.FlushAllElements();
            return ValueTask.CompletedTask;
        }
        if (editStack.Locked)
        {
            var message = string.Format(
                UndoRedoStrings.CannotResourceRedoDueToInProgressUndoRedo,
                element.Label);
            _notificationService.Warn(message);
            return ValueTask.CompletedTask;
        }

        return InvokeResourcePrepare(element, cleanup =>
        {
            editStack.MoveForward(element);
            return SafeInvokeWithLocks(
                element,
                element.Actual.Redo,
                new EditStackSnapshot(new[] { editStack }),
                cleanup,
                () => ContinueRedoInGroup(element.GroupId));
        });
    }

    private (StackElement? Element, string? StrResource) FindClosestRedoElementInGroup(int groupId)
    {
        if (groupId == 0)
        {
            return (null, null);
        }

        // find another element with the same groupId and with the lowest groupOrder ready to be redone
        StackElement? matchedElement = null;
        string? matchedStrResource = null;

        foreach (var (strResource, editStack) in _editStacks)
        {
            var candidate = editStack.GetClosestFutureElement();
            if (candidate is null)
            {
                continue;
            }
            if (candidate.GroupId == groupId)
            {
                if (matchedElement is null || candidate.GroupOrder < matchedElement.GroupOrder)
                {
                    matchedElement = candidate;
                    matchedStrResource = strResource;
                }
            }
        }

        return (matchedElement, matchedStrResource);
    }

    private ValueTask ContinueRedoInGroup(int groupId)
    {
        if (groupId == 0)
        {
            return ValueTask.CompletedTask;
        }

        var (_, matchedStrResource) = FindClosestRedoElementInGroup(groupId);
        if (matchedStrResource is not null)
        {
            return RedoCore(matchedStrResource);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask Redo(Uri resource) => RedoCore(GetUriComparisonKey(resource));

    public ValueTask Redo(UndoRedoSource source)
    {
        var (_, matchedStrResource) = FindClosestRedoElementWithSource(source.Id);
        return matchedStrResource is not null ? RedoCore(matchedStrResource) : ValueTask.CompletedTask;
    }

    public ValueTask Redo(string strResource) => RedoCore(strResource);

    private ValueTask RedoCore(string strResource)
    {
        if (!_editStacks.TryGetValue(strResource, out var editStack))
        {
            return ValueTask.CompletedTask;
        }

        var element = editStack.GetClosestFutureElement();
        if (element is null)
        {
            return ValueTask.CompletedTask;
        }

        if (element.GroupId != 0)
        {
            // this element is a part of a group, we need to make sure redoing in a group is in order
            var (matchedElement, matchedStrResource) = FindClosestRedoElementInGroup(element.GroupId);
            if (!ReferenceEquals(element, matchedElement) && matchedStrResource is not null)
            {
                // there is an element in the same group that should be redone before this one
                return RedoCore(matchedStrResource);
            }
        }

        if (element.Type == UndoRedoElementType.Workspace)
        {
            return WorkspaceRedo(strResource, (WorkspaceStackElement)element);
        }
        return ResourceRedo(editStack, (ResourceStackElement)element);
    }

    private sealed class UriComparisonKeyEntry
    {
        public UriComparisonKeyEntry(string scheme, UriComparisonKeyComputer computer)
        {
            Scheme = scheme;
            Computer = computer;
        }

        public string Scheme { get; }

        public UriComparisonKeyComputer Computer { get; }
    }

    private sealed class UriComparisonKeyRegistration : IDisposable
    {
        private readonly UndoRedoService _service;
        private readonly UriComparisonKeyEntry _entry;
        private bool _disposed;

        public UriComparisonKeyRegistration(UndoRedoService service, UriComparisonKeyEntry entry)
        {
            _service = service;
            _entry = entry;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _service._uriComparisonKeyComputers.Remove(_entry);
        }
    }
}
