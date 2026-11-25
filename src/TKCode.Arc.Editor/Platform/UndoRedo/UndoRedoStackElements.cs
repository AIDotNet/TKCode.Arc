// Ported from vscode (src/vs/platform/undoRedo/common/undoRedoService.ts, commit b57d5c9e), MIT license.

namespace TKCode.Arc.Editor.Platform;

internal enum RemovedResourceReason
{
    ExternalRemoval = 0,
    NoParallelUniverses = 1,
}

internal readonly struct ResourceReasonPair
{
    public ResourceReasonPair(string resourceLabel, RemovedResourceReason reason)
    {
        ResourceLabel = resourceLabel;
        Reason = reason;
    }

    public string ResourceLabel { get; }

    public RemovedResourceReason Reason { get; }
}

internal sealed class RemovedResources
{
    private readonly Dictionary<string, ResourceReasonPair> _elements = new();

    public string CreateMessage()
    {
        var externalRemoval = new List<string>();
        var noParallelUniverses = new List<string>();
        foreach (var element in _elements.Values)
        {
            var dest = element.Reason == RemovedResourceReason.ExternalRemoval
                ? externalRemoval
                : noParallelUniverses;
            dest.Add(element.ResourceLabel);
        }

        var messages = new List<string>();
        if (externalRemoval.Count > 0)
        {
            messages.Add(string.Format(
                UndoRedoStrings.ExternalRemoval,
                string.Join(", ", externalRemoval)));
        }
        if (noParallelUniverses.Count > 0)
        {
            messages.Add(string.Format(
                UndoRedoStrings.NoParallelUniverses,
                string.Join(", ", noParallelUniverses)));
        }
        return string.Join("\n", messages);
    }

    public int Size => _elements.Count;

    public bool Has(string strResource) => _elements.ContainsKey(strResource);

    public void Set(string strResource, ResourceReasonPair value) => _elements[strResource] = value;

    public bool Delete(string strResource) => _elements.Remove(strResource);
}

/// <summary>
/// A stack element as the service stores it: the element an application pushed plus the facts the service derived
/// from it (the comparison keys, the group and source ordering, and whether it is still valid).
/// </summary>
internal abstract class StackElement
{
    protected StackElement(
        IUndoRedoElement actual,
        string[] resourceLabels,
        string[] strResources,
        int groupId,
        int groupOrder,
        int sourceId,
        int sourceOrder)
    {
        Id = Interlocked.Increment(ref s_stackElementCounter);
        Actual = actual;
        Label = actual.Label;
        ConfirmBeforeUndo = actual.ConfirmBeforeUndo;
        ResourceLabels = resourceLabels;
        StrResources = strResources;
        GroupId = groupId;
        GroupOrder = groupOrder;
        SourceId = sourceId;
        SourceOrder = sourceOrder;
    }

    private static int s_stackElementCounter;

    public int Id { get; }

    public abstract UndoRedoElementType Type { get; }

    public IUndoRedoElement Actual { get; }

    public string Label { get; }

    public bool ConfirmBeforeUndo { get; }

    public IReadOnlyList<string> ResourceLabels { get; }

    public IReadOnlyList<string> StrResources { get; }

    public int GroupId { get; }

    public int GroupOrder { get; }

    public int SourceId { get; }

    public int SourceOrder { get; }
}

internal sealed class ResourceStackElement : StackElement
{
    public ResourceStackElement(
        IUndoRedoElement actual,
        string resourceLabel,
        string strResource,
        int groupId,
        int groupOrder,
        int sourceId,
        int sourceOrder)
        : base(actual, new[] { resourceLabel }, new[] { strResource }, groupId, groupOrder, sourceId, sourceOrder)
    {
        ResourceLabel = resourceLabel;
        StrResource = strResource;
        IsValid = true;
    }

    public override UndoRedoElementType Type => UndoRedoElementType.Resource;

    public string ResourceLabel { get; }

    public string StrResource { get; }

    public bool IsValid { get; private set; }

    public void SetValid(bool isValid) => IsValid = isValid;

    public override string ToString() =>
        $"[id:{Id}] [group:{GroupId}] [{(IsValid ? "  VALID" : "INVALID")}] {Actual.GetType().Name} - {Actual}";
}

internal sealed class WorkspaceStackElement : StackElement
{
    public WorkspaceStackElement(
        IUndoRedoElement actual,
        string[] resourceLabels,
        string[] strResources,
        int groupId,
        int groupOrder,
        int sourceId,
        int sourceOrder)
        : base(actual, resourceLabels, strResources, groupId, groupOrder, sourceId, sourceOrder)
    {
    }

    public override UndoRedoElementType Type => UndoRedoElementType.Workspace;

    public RemovedResources? RemovedResources { get; private set; }

    public RemovedResources? InvalidatedResources { get; private set; }

    public bool CanSplit() => Actual is ISplittableWorkspaceUndoRedoElement;

    public void RemoveResource(string resourceLabel, string strResource, RemovedResourceReason reason)
    {
        RemovedResources ??= new RemovedResources();
        if (!RemovedResources.Has(strResource))
        {
            RemovedResources.Set(strResource, new ResourceReasonPair(resourceLabel, reason));
        }
    }

    public void SetValid(string resourceLabel, string strResource, bool isValid)
    {
        if (isValid)
        {
            if (InvalidatedResources is not null)
            {
                InvalidatedResources.Delete(strResource);
                if (InvalidatedResources.Size == 0)
                {
                    InvalidatedResources = null;
                }
            }
        }
        else
        {
            InvalidatedResources ??= new RemovedResources();
            if (!InvalidatedResources.Has(strResource))
            {
                InvalidatedResources.Set(
                    strResource,
                    new ResourceReasonPair(resourceLabel, RemovedResourceReason.ExternalRemoval));
            }
        }
    }

    public override string ToString() =>
        $"[id:{Id}] [group:{GroupId}] [{(InvalidatedResources is not null ? "INVALID" : "  VALID")}] {Actual.GetType().Name} - {Actual}";
}

internal sealed class WorkspaceVerificationError
{
    public WorkspaceVerificationError(ValueTask returnValue)
    {
        ReturnValue = returnValue;
    }

    public WorkspaceVerificationError()
    {
        ReturnValue = ValueTask.CompletedTask;
    }

    public ValueTask ReturnValue { get; }
}
