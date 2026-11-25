// Ported from vscode (src/vs/platform/undoRedo/common/undoRedo.ts, commit b57d5c9e), MIT license.

namespace TKCode.Arc.Editor.Platform;

internal enum UndoRedoElementType
{
    Resource = 0,
    Workspace = 1,
}

/// <summary>An undo or redo step. Its <see cref="Undo"/> and <see cref="Redo"/> may complete synchronously.</summary>
internal interface IUndoRedoElement
{
    UndoRedoElementType Type { get; }

    /// <summary>A user presentable label. May be localized.</summary>
    string Label { get; }

    /// <summary>A code describing the operation. Will not be localized.</summary>
    string Code { get; }

    /// <summary>Show a message to the user confirming when trying to undo this element.</summary>
    bool ConfirmBeforeUndo { get; }

    ValueTask Undo();

    ValueTask Redo();
}

internal interface IResourceUndoRedoElement : IUndoRedoElement
{
    /// <summary>The resource impacted by this element.</summary>
    Uri Resource { get; }
}

internal interface IWorkspaceUndoRedoElement : IUndoRedoElement
{
    /// <summary>The resources impacted by this element.</summary>
    IReadOnlyList<Uri> Resources { get; }

    /// <summary>
    /// If implemented, will be invoked before calling <see cref="IUndoRedoElement.Undo"/> or
    /// <see cref="IUndoRedoElement.Redo"/>. This is a good place to prepare everything such that those calls are
    /// synchronous. If a disposable is returned, it will be invoked to clean things up.
    /// </summary>
    ValueTask<IDisposable?> PrepareUndoRedo() => ValueTask.FromResult<IDisposable?>(null);
}

/// <summary>
/// Implemented next to <see cref="IWorkspaceUndoRedoElement"/> by elements that can be split into one element per
/// resource, so that an undo or redo can continue on the resources that are still compatible.
/// </summary>
internal interface ISplittableWorkspaceUndoRedoElement : IWorkspaceUndoRedoElement
{
    IReadOnlyList<IResourceUndoRedoElement> Split();
}

internal interface IPastFutureElements
{
    IReadOnlyList<IUndoRedoElement> Past { get; }

    IReadOnlyList<IUndoRedoElement> Future { get; }
}

internal sealed class PastFutureElements : IPastFutureElements
{
    public PastFutureElements(IReadOnlyList<IUndoRedoElement> past, IReadOnlyList<IUndoRedoElement> future)
    {
        Past = past;
        Future = future;
    }

    public IReadOnlyList<IUndoRedoElement> Past { get; }

    public IReadOnlyList<IUndoRedoElement> Future { get; }
}

internal interface UriComparisonKeyComputer
{
    string GetComparisonKey(Uri uri);
}

internal sealed class ResourceEditStackSnapshot
{
    public ResourceEditStackSnapshot(Uri resource, IReadOnlyList<int> elements)
    {
        Resource = resource;
        Elements = elements;
    }

    public Uri Resource { get; }

    public IReadOnlyList<int> Elements { get; }
}

internal sealed class UndoRedoGroup
{
    private static readonly UndoRedoGroup s_none = new();

    private static int s_id;

    private int _order;

    public UndoRedoGroup()
    {
        Id = Interlocked.Increment(ref s_id) - 1;
        _order = 1;
    }

    public static UndoRedoGroup None => s_none;

    public int Id { get; }

    public int NextOrder()
    {
        if (Id == 0)
        {
            return 0;
        }
        return _order++;
    }
}

internal sealed class UndoRedoSource
{
    private static readonly UndoRedoSource s_none = new();

    private static int s_id;

    private int _order;

    public UndoRedoSource()
    {
        Id = Interlocked.Increment(ref s_id) - 1;
        _order = 1;
    }

    public static UndoRedoSource None => s_none;

    public int Id { get; }

    public int NextOrder()
    {
        if (Id == 0)
        {
            return 0;
        }
        return _order++;
    }
}

internal interface IUndoRedoService
{
    /// <summary>
    /// Register an URI -> string hasher. This is useful for making multiple URIs share the same undo-redo stack.
    /// </summary>
    IDisposable RegisterUriComparisonKeyComputer(string scheme, UriComparisonKeyComputer uriComparisonKeyComputer);

    /// <summary>Get the hash used internally for a certain URI. This uses any registered <see cref="UriComparisonKeyComputer"/>.</summary>
    string GetUriComparisonKey(Uri resource);

    /// <summary>Add a new element to the undo stack. This will destroy the redo stack.</summary>
    void PushElement(IUndoRedoElement element, UndoRedoGroup? group = null, UndoRedoSource? source = null);

    /// <summary>Get the last pushed element for a resource. If the last pushed element has been undone, returns null.</summary>
    IUndoRedoElement? GetLastElement(Uri resource);

    /// <summary>Get all the elements associated with a resource. This includes the past and the future.</summary>
    IPastFutureElements GetElements(Uri resource);

    /// <summary>Validate or invalidate stack elements associated with a resource.</summary>
    void SetElementsValidFlag(Uri resource, bool isValid, Func<IUndoRedoElement, bool> filter);

    /// <summary>Remove elements that target <paramref name="resource"/>.</summary>
    void RemoveElements(Uri resource);

    /// <summary>Create a snapshot of the current elements on the undo-redo stack for a resource.</summary>
    ResourceEditStackSnapshot CreateSnapshot(Uri resource);

    /// <summary>
    /// Attempt (as best as possible) to restore a certain snapshot previously created with <see cref="CreateSnapshot"/>
    /// for a resource.
    /// </summary>
    void RestoreSnapshot(ResourceEditStackSnapshot snapshot);

    bool CanUndo(Uri resource);

    bool CanUndo(UndoRedoSource source);

    ValueTask Undo(Uri resource);

    ValueTask Undo(UndoRedoSource source);

    bool CanRedo(Uri resource);

    bool CanRedo(UndoRedoSource source);

    ValueTask Redo(Uri resource);

    ValueTask Redo(UndoRedoSource source);
}
