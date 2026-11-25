// Ported from vscode (src/vs/platform/undoRedo/common/undoRedoService.ts, commit b57d5c9e), MIT license.

namespace TKCode.Arc.Editor.Platform;

/// <summary>The past and future elements of one resource, plus its lock and a version that tells whether it changed.</summary>
internal sealed class ResourceEditStack
{
    private List<StackElement> _past;
    private List<StackElement> _future;

    public ResourceEditStack(string resourceLabel, string strResource)
    {
        ResourceLabel = resourceLabel;
        StrResource = strResource;
        _past = new List<StackElement>();
        _future = new List<StackElement>();
        Locked = false;
        VersionId = 1;
    }

    public string ResourceLabel { get; }

    public string StrResource { get; }

    public bool Locked { get; set; }

    public int VersionId { get; private set; }

    public void Dispose()
    {
        foreach (var element in _past)
        {
            if (element.Type == UndoRedoElementType.Workspace)
            {
                ((WorkspaceStackElement)element).RemoveResource(
                    ResourceLabel,
                    StrResource,
                    RemovedResourceReason.ExternalRemoval);
            }
        }
        foreach (var element in _future)
        {
            if (element.Type == UndoRedoElementType.Workspace)
            {
                ((WorkspaceStackElement)element).RemoveResource(
                    ResourceLabel,
                    StrResource,
                    RemovedResourceReason.ExternalRemoval);
            }
        }
        VersionId++;
    }

    public override string ToString()
    {
        var result = new List<string> { $"* {StrResource}:" };
        for (var i = 0; i < _past.Count; i++)
        {
            result.Add($"   * [UNDO] {_past[i]}");
        }
        for (var i = _future.Count - 1; i >= 0; i--)
        {
            result.Add($"   * [REDO] {_future[i]}");
        }
        return string.Join("\n", result);
    }

    public void FlushAllElements()
    {
        _past = new List<StackElement>();
        _future = new List<StackElement>();
        VersionId++;
    }

    public void SetElementsIsValid(bool isValid)
    {
        foreach (var element in _past)
        {
            SetElementValidFlag(element, isValid);
        }
        foreach (var element in _future)
        {
            SetElementValidFlag(element, isValid);
        }
    }

    private void SetElementValidFlag(StackElement element, bool isValid)
    {
        if (element.Type == UndoRedoElementType.Workspace)
        {
            ((WorkspaceStackElement)element).SetValid(ResourceLabel, StrResource, isValid);
        }
        else
        {
            ((ResourceStackElement)element).SetValid(isValid);
        }
    }

    public void SetElementsValidFlag(bool isValid, Func<IUndoRedoElement, bool> filter)
    {
        foreach (var element in _past)
        {
            if (filter(element.Actual))
            {
                SetElementValidFlag(element, isValid);
            }
        }
        foreach (var element in _future)
        {
            if (filter(element.Actual))
            {
                SetElementValidFlag(element, isValid);
            }
        }
    }

    public void PushElement(StackElement element)
    {
        // remove the future
        foreach (var futureElement in _future)
        {
            if (futureElement.Type == UndoRedoElementType.Workspace)
            {
                ((WorkspaceStackElement)futureElement).RemoveResource(
                    ResourceLabel,
                    StrResource,
                    RemovedResourceReason.NoParallelUniverses);
            }
        }
        _future = new List<StackElement>();
        _past.Add(element);
        VersionId++;
    }

    public ResourceEditStackSnapshot CreateSnapshot(Uri resource)
    {
        var elements = new List<int>(_past.Count + _future.Count);

        for (var i = 0; i < _past.Count; i++)
        {
            elements.Add(_past[i].Id);
        }
        for (var i = _future.Count - 1; i >= 0; i--)
        {
            elements.Add(_future[i].Id);
        }

        return new ResourceEditStackSnapshot(resource, elements);
    }

    public void RestoreSnapshot(ResourceEditStackSnapshot snapshot)
    {
        var snapshotLength = snapshot.Elements.Count;
        var isOK = true;
        var snapshotIndex = 0;
        var removePastAfter = -1;
        for (var i = 0; i < _past.Count; i++, snapshotIndex++)
        {
            var element = _past[i];
            if (isOK && (snapshotIndex >= snapshotLength || element.Id != snapshot.Elements[snapshotIndex]))
            {
                isOK = false;
                removePastAfter = i;
            }
            if (!isOK && element.Type == UndoRedoElementType.Workspace)
            {
                ((WorkspaceStackElement)element).RemoveResource(
                    ResourceLabel,
                    StrResource,
                    RemovedResourceReason.ExternalRemoval);
            }
        }
        var removeFutureBefore = -1;
        for (var i = _future.Count - 1; i >= 0; i--, snapshotIndex++)
        {
            var element = _future[i];
            if (isOK && (snapshotIndex >= snapshotLength || element.Id != snapshot.Elements[snapshotIndex]))
            {
                isOK = false;
                removeFutureBefore = i;
            }
            if (!isOK && element.Type == UndoRedoElementType.Workspace)
            {
                ((WorkspaceStackElement)element).RemoveResource(
                    ResourceLabel,
                    StrResource,
                    RemovedResourceReason.ExternalRemoval);
            }
        }
        if (removePastAfter != -1)
        {
            _past = _past.GetRange(0, removePastAfter);
        }
        if (removeFutureBefore != -1)
        {
            _future = _future.GetRange(removeFutureBefore + 1, _future.Count - removeFutureBefore - 1);
        }
        VersionId++;
    }

    public IPastFutureElements GetElements()
    {
        var past = new List<IUndoRedoElement>(_past.Count);
        var future = new List<IUndoRedoElement>(_future.Count);

        foreach (var element in _past)
        {
            past.Add(element.Actual);
        }
        foreach (var element in _future)
        {
            future.Add(element.Actual);
        }

        return new PastFutureElements(past, future);
    }

    public StackElement? GetClosestPastElement()
    {
        if (_past.Count == 0)
        {
            return null;
        }
        return _past[^1];
    }

    public StackElement? GetSecondClosestPastElement()
    {
        if (_past.Count < 2)
        {
            return null;
        }
        return _past[^2];
    }

    public StackElement? GetClosestFutureElement()
    {
        if (_future.Count == 0)
        {
            return null;
        }
        return _future[^1];
    }

    public bool HasPastElements() => _past.Count > 0;

    public bool HasFutureElements() => _future.Count > 0;

    public void SplitPastWorkspaceElement(
        WorkspaceStackElement toRemove,
        IReadOnlyDictionary<string, ResourceStackElement> individualMap)
    {
        for (var j = _past.Count - 1; j >= 0; j--)
        {
            if (ReferenceEquals(_past[j], toRemove))
            {
                if (individualMap.TryGetValue(StrResource, out var individual))
                {
                    // gets replaced
                    _past[j] = individual;
                }
                else
                {
                    // gets deleted
                    _past.RemoveAt(j);
                }
                break;
            }
        }
        VersionId++;
    }

    public void SplitFutureWorkspaceElement(
        WorkspaceStackElement toRemove,
        IReadOnlyDictionary<string, ResourceStackElement> individualMap)
    {
        for (var j = _future.Count - 1; j >= 0; j--)
        {
            if (ReferenceEquals(_future[j], toRemove))
            {
                if (individualMap.TryGetValue(StrResource, out var individual))
                {
                    // gets replaced
                    _future[j] = individual;
                }
                else
                {
                    // gets deleted
                    _future.RemoveAt(j);
                }
                break;
            }
        }
        VersionId++;
    }

    public void MoveBackward(StackElement element)
    {
        _past.RemoveAt(_past.Count - 1);
        _future.Add(element);
        VersionId++;
    }

    public void MoveForward(StackElement element)
    {
        _future.RemoveAt(_future.Count - 1);
        _past.Add(element);
        VersionId++;
    }
}

/// <summary>The edit stacks affected by one operation, with the version each had when the operation started.</summary>
internal sealed class EditStackSnapshot
{
    private readonly int[] _versionIds;

    public EditStackSnapshot(IReadOnlyList<ResourceEditStack> editStacks)
    {
        EditStacks = editStacks;
        _versionIds = new int[editStacks.Count];
        for (var i = 0; i < editStacks.Count; i++)
        {
            _versionIds[i] = editStacks[i].VersionId;
        }
    }

    public IReadOnlyList<ResourceEditStack> EditStacks { get; }

    public bool IsValid()
    {
        for (var i = 0; i < EditStacks.Count; i++)
        {
            if (_versionIds[i] != EditStacks[i].VersionId)
            {
                return false;
            }
        }
        return true;
    }
}
