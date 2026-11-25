// Ported from vscode (src/vs/editor/common/model/editStack.ts, commit b57d5c9e), MIT license.
//
// Differences from upstream:
//  - upstream keeps the model of an element as `ITextModel | URI` in one field; here it is a model and a resource,
//    and the model is null once only the resource is known (the model was disposed);
//  - the cursor state computer is a delegate (CursorStateComputer);
//  - PushEditOperation takes the operations the model validated (upstream types them as plain edit operations, but
//    always receives validated ones);
//  - Undo and Redo return completed ValueTasks, because IUndoRedoElement's do.

using System.Buffers.Binary;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc.Editor.Model;

/// <summary>
/// Computes the selections after some edits from the edits that undo them (Monaco's <c>ICursorStateComputer</c>).
/// Returns null to leave the selections to the caller.
/// </summary>
internal delegate IReadOnlyList<TextSelection>? CursorStateComputer(IReadOnlyList<ValidEditOperation> inverseEditOperations);

/// <summary>What one undo step of one model holds: the changes, the version and EOL around them and the cursors.</summary>
internal sealed class SingleModelEditStackData
{
    public SingleModelEditStackData(
        int beforeVersionId,
        int afterVersionId,
        EndOfLineSequence beforeEOL,
        EndOfLineSequence afterEOL,
        IReadOnlyList<TextSelection>? beforeCursorState,
        IReadOnlyList<TextSelection>? afterCursorState,
        List<TextChange> changes)
    {
        BeforeVersionId = beforeVersionId;
        AfterVersionId = afterVersionId;
        BeforeEOL = beforeEOL;
        AfterEOL = afterEOL;
        BeforeCursorState = beforeCursorState;
        AfterCursorState = afterCursorState;
        Changes = changes;
    }

    public int BeforeVersionId { get; }

    public int AfterVersionId { get; private set; }

    public EndOfLineSequence BeforeEOL { get; }

    public EndOfLineSequence AfterEOL { get; private set; }

    public IReadOnlyList<TextSelection>? BeforeCursorState { get; }

    public IReadOnlyList<TextSelection>? AfterCursorState { get; private set; }

    public List<TextChange> Changes { get; private set; }

    public static SingleModelEditStackData Create(TextModel model, IReadOnlyList<TextSelection>? beforeCursorState)
    {
        var alternativeVersionId = model.GetAlternativeVersionId();
        var eol = EditStack.GetModelEOL(model);
        return new SingleModelEditStackData(
            alternativeVersionId,
            alternativeVersionId,
            eol,
            eol,
            beforeCursorState,
            beforeCursorState,
            new List<TextChange>());
    }

    public void Append(
        List<TextChange> textChanges,
        EndOfLineSequence afterEOL,
        int afterVersionId,
        IReadOnlyList<TextSelection>? afterCursorState)
    {
        if (textChanges.Count > 0)
        {
            Changes = TextChange.CompressConsecutiveTextChanges(Changes, textChanges);
        }

        AfterEOL = afterEOL;
        AfterVersionId = afterVersionId;
        AfterCursorState = afterCursorState;
    }

    private static int WriteSelectionsSize(IReadOnlyList<TextSelection>? selections) =>
        4 + (4 * 4 * (selections?.Count ?? 0));

    private static int WriteSelections(Span<byte> b, IReadOnlyList<TextSelection>? selections, int offset)
    {
        BinaryPrimitives.WriteUInt32BigEndian(b[offset..], (uint)(selections?.Count ?? 0));
        offset += 4;
        if (selections is not null)
        {
            foreach (var selection in selections)
            {
                BinaryPrimitives.WriteUInt32BigEndian(b[offset..], (uint)selection.SelectionStartLineNumber);
                offset += 4;
                BinaryPrimitives.WriteUInt32BigEndian(b[offset..], (uint)selection.SelectionStartColumn);
                offset += 4;
                BinaryPrimitives.WriteUInt32BigEndian(b[offset..], (uint)selection.PositionLineNumber);
                offset += 4;
                BinaryPrimitives.WriteUInt32BigEndian(b[offset..], (uint)selection.PositionColumn);
                offset += 4;
            }
        }

        return offset;
    }

    private static int ReadSelections(ReadOnlySpan<byte> b, int offset, List<TextSelection> dest)
    {
        var count = (int)BinaryPrimitives.ReadUInt32BigEndian(b[offset..]);
        offset += 4;
        for (var i = 0; i < count; i++)
        {
            var selectionStartLineNumber = (int)BinaryPrimitives.ReadUInt32BigEndian(b[offset..]);
            offset += 4;
            var selectionStartColumn = (int)BinaryPrimitives.ReadUInt32BigEndian(b[offset..]);
            offset += 4;
            var positionLineNumber = (int)BinaryPrimitives.ReadUInt32BigEndian(b[offset..]);
            offset += 4;
            var positionColumn = (int)BinaryPrimitives.ReadUInt32BigEndian(b[offset..]);
            offset += 4;
            dest.Add(new TextSelection(selectionStartLineNumber, selectionStartColumn, positionLineNumber, positionColumn));
        }

        return offset;
    }

    public byte[] Serialize()
    {
        var necessarySize =
            4 // beforeVersionId
            + 4 // afterVersionId
            + 1 // beforeEOL
            + 1 // afterEOL
            + WriteSelectionsSize(BeforeCursorState)
            + WriteSelectionsSize(AfterCursorState)
            + 4; // change count
        foreach (var change in Changes)
        {
            necessarySize += change.WriteSize();
        }

        var b = new byte[necessarySize];
        var offset = 0;
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(offset), (uint)BeforeVersionId);
        offset += 4;
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(offset), (uint)AfterVersionId);
        offset += 4;
        b[offset] = (byte)BeforeEOL;
        offset += 1;
        b[offset] = (byte)AfterEOL;
        offset += 1;
        offset = WriteSelections(b, BeforeCursorState, offset);
        offset = WriteSelections(b, AfterCursorState, offset);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(offset), (uint)Changes.Count);
        offset += 4;
        foreach (var change in Changes)
        {
            offset = change.Write(b, offset);
        }

        return b;
    }

    public static SingleModelEditStackData Deserialize(ReadOnlySpan<byte> b)
    {
        var offset = 0;
        var beforeVersionId = (int)BinaryPrimitives.ReadUInt32BigEndian(b[offset..]);
        offset += 4;
        var afterVersionId = (int)BinaryPrimitives.ReadUInt32BigEndian(b[offset..]);
        offset += 4;
        var beforeEOL = (EndOfLineSequence)b[offset];
        offset += 1;
        var afterEOL = (EndOfLineSequence)b[offset];
        offset += 1;
        var beforeCursorState = new List<TextSelection>();
        offset = ReadSelections(b, offset, beforeCursorState);
        var afterCursorState = new List<TextSelection>();
        offset = ReadSelections(b, offset, afterCursorState);
        var changeCount = (int)BinaryPrimitives.ReadUInt32BigEndian(b[offset..]);
        offset += 4;
        var changes = new List<TextChange>(changeCount);
        for (var i = 0; i < changeCount; i++)
        {
            offset = TextChange.Read(b, offset, changes);
        }

        return new SingleModelEditStackData(
            beforeVersionId,
            afterVersionId,
            beforeEOL,
            afterEOL,
            beforeCursorState,
            afterCursorState,
            changes);
    }
}

/// <summary>Called before a multi-model element is undone or redone (Monaco's <c>IUndoRedoDelegate</c>).</summary>
internal interface IUndoRedoDelegate
{
    ValueTask<IDisposable?> PrepareUndoRedo(MultiModelEditStackElement element);
}

/// <summary>
/// One undo step of one model. While it is open, edits are appended to it; closing it serializes its data, which
/// takes far less memory than the objects.
/// </summary>
internal sealed class SingleModelEditStackElement : IResourceUndoRedoElement
{
    private SingleModelEditStackData? _data;
    private byte[]? _serialized;

    public SingleModelEditStackElement(string label, string code, TextModel model, IReadOnlyList<TextSelection>? beforeCursorState)
    {
        Label = label;
        Code = code;
        Model = model;
        Resource = model.Uri;
        _data = SingleModelEditStackData.Create(model, beforeCursorState);
    }

    public UndoRedoElementType Type => UndoRedoElementType.Resource;

    public string Label { get; }

    public string Code { get; }

    public bool ConfirmBeforeUndo => false;

    /// <summary>The model the element edits; null when only its resource is known.</summary>
    public TextModel? Model { get; private set; }

    public Uri Resource { get; private set; }

    private SingleModelEditStackData Data => _data ?? SingleModelEditStackData.Deserialize(_serialized!);

    public override string ToString() => string.Join(", ", Data.Changes.Select(change => change.ToString()));

    public bool MatchesResource(Uri resource) => Resource.ToString() == resource.ToString();

    public void SetModel(TextModel model)
    {
        Model = model;
        Resource = model.Uri;
    }

    public void SetModel(Uri resource)
    {
        Model = null;
        Resource = resource;
    }

    public bool CanAppend(TextModel model) => ReferenceEquals(Model, model) && _data is not null;

    public void Append(
        TextModel model,
        List<TextChange> textChanges,
        EndOfLineSequence afterEOL,
        int afterVersionId,
        IReadOnlyList<TextSelection>? afterCursorState)
    {
        _ = model;
        _data?.Append(textChanges, afterEOL, afterVersionId, afterCursorState);
    }

    public void Close()
    {
        if (_data is not null)
        {
            _serialized = _data.Serialize();
            _data = null;
        }
    }

    public void Open()
    {
        if (_data is null)
        {
            _data = SingleModelEditStackData.Deserialize(_serialized!);
            _serialized = null;
        }
    }

    public ValueTask Undo()
    {
        var model = Model ?? throw new InvalidOperationException("Invalid SingleModelEditStackElement");
        Close();
        var data = SingleModelEditStackData.Deserialize(_serialized!);
        model.ApplyUndo(data.Changes, data.BeforeEOL, data.BeforeVersionId, data.BeforeCursorState);
        return ValueTask.CompletedTask;
    }

    public ValueTask Redo()
    {
        var model = Model ?? throw new InvalidOperationException("Invalid SingleModelEditStackElement");
        Close();
        var data = SingleModelEditStackData.Deserialize(_serialized!);
        model.ApplyRedo(data.Changes, data.AfterEOL, data.AfterVersionId, data.AfterCursorState);
        return ValueTask.CompletedTask;
    }

    public int HeapSize()
    {
        Close();
        return _serialized!.Length + 168; // heap overhead
    }
}

/// <summary>One undo step that edits several models at once (a refactoring, a workspace edit).</summary>
internal sealed class MultiModelEditStackElement : ISplittableWorkspaceUndoRedoElement
{
    private readonly List<SingleModelEditStackElement> _editStackElementsArr;
    private readonly Dictionary<string, SingleModelEditStackElement> _editStackElementsMap;
    private bool _isOpen;
    private IUndoRedoDelegate? _delegate;

    public MultiModelEditStackElement(string label, string code, IReadOnlyList<SingleModelEditStackElement> editStackElements)
    {
        Label = label;
        Code = code;
        _isOpen = true;
        _editStackElementsArr = new List<SingleModelEditStackElement>(editStackElements);
        _editStackElementsMap = new Dictionary<string, SingleModelEditStackElement>();
        foreach (var editStackElement in _editStackElementsArr)
        {
            _editStackElementsMap[UriGetComparisonKey(editStackElement.Resource)] = editStackElement;
        }
    }

    public UndoRedoElementType Type => UndoRedoElementType.Workspace;

    public string Label { get; }

    public string Code { get; }

    public bool ConfirmBeforeUndo => false;

    public IReadOnlyList<Uri> Resources => _editStackElementsArr.Select(element => element.Resource).ToList();

    private static string UriGetComparisonKey(Uri resource) => resource.ToString();

    public void SetDelegate(IUndoRedoDelegate @delegate) => _delegate = @delegate;

    public ValueTask<IDisposable?> PrepareUndoRedo() =>
        _delegate is not null ? _delegate.PrepareUndoRedo(this) : ValueTask.FromResult<IDisposable?>(null);

    public List<Uri> GetMissingModels()
    {
        var result = new List<Uri>();
        foreach (var editStackElement in _editStackElementsArr)
        {
            if (editStackElement.Model is null)
            {
                result.Add(editStackElement.Resource);
            }
        }

        return result;
    }

    public bool MatchesResource(Uri resource) => _editStackElementsMap.ContainsKey(UriGetComparisonKey(resource));

    public void SetModel(TextModel model)
    {
        if (_editStackElementsMap.TryGetValue(UriGetComparisonKey(model.Uri), out var element))
        {
            element.SetModel(model);
        }
    }

    public void SetModel(Uri resource)
    {
        if (_editStackElementsMap.TryGetValue(UriGetComparisonKey(resource), out var element))
        {
            element.SetModel(resource);
        }
    }

    public bool CanAppend(TextModel model)
    {
        if (!_isOpen)
        {
            return false;
        }

        return _editStackElementsMap.TryGetValue(UriGetComparisonKey(model.Uri), out var element) && element.CanAppend(model);
    }

    public void Append(
        TextModel model,
        List<TextChange> textChanges,
        EndOfLineSequence afterEOL,
        int afterVersionId,
        IReadOnlyList<TextSelection>? afterCursorState)
    {
        var editStackElement = _editStackElementsMap[UriGetComparisonKey(model.Uri)];
        editStackElement.Append(model, textChanges, afterEOL, afterVersionId, afterCursorState);
    }

    public void Close() => _isOpen = false;

    public void Open()
    {
        // cannot reopen
    }

    public async ValueTask Undo()
    {
        _isOpen = false;
        foreach (var editStackElement in _editStackElementsArr)
        {
            await editStackElement.Undo().ConfigureAwait(false);
        }
    }

    public async ValueTask Redo()
    {
        foreach (var editStackElement in _editStackElementsArr)
        {
            await editStackElement.Redo().ConfigureAwait(false);
        }
    }

    public int HeapSize(Uri resource) =>
        _editStackElementsMap.TryGetValue(UriGetComparisonKey(resource), out var element) ? element.HeapSize() : 0;

    public IReadOnlyList<IResourceUndoRedoElement> Split() => _editStackElementsArr;

    public override string ToString()
    {
        var result = new List<string>();
        foreach (var editStackElement in _editStackElementsArr)
        {
            result.Add($"{Path.GetFileName(editStackElement.Resource.AbsolutePath)}: {editStackElement}");
        }

        return "{" + string.Join(", ", result) + "}";
    }
}

/// <summary>Connects a model's edits to the undo-redo service: one element per undo step.</summary>
internal sealed class EditStack
{
    /// <summary>The label of an undo step of typing (upstream localizes it).</summary>
    internal const string TypingLabel = "Typing";

    private readonly TextModel _model;
    private readonly IUndoRedoService _undoRedoService;

    public EditStack(TextModel model, IUndoRedoService undoRedoService)
    {
        _model = model;
        _undoRedoService = undoRedoService;
    }

    internal static EndOfLineSequence GetModelEOL(TextModel model) =>
        model.GetEOL() == "\n" ? EndOfLineSequence.LF : EndOfLineSequence.CRLF;

    /// <summary>Ends the current undo step: the next edit starts a new one.</summary>
    public void PushStackElement()
    {
        switch (_undoRedoService.GetLastElement(_model.Uri))
        {
            case SingleModelEditStackElement single:
                single.Close();
                break;
            case MultiModelEditStackElement multi:
                multi.Close();
                break;
        }
    }

    /// <summary>Reopens the last undo step, so that the next edit joins it.</summary>
    public void PopStackElement()
    {
        switch (_undoRedoService.GetLastElement(_model.Uri))
        {
            case SingleModelEditStackElement single:
                single.Open();
                break;
            case MultiModelEditStackElement multi:
                multi.Open();
                break;
        }
    }

    public void Clear() => _undoRedoService.RemoveElements(_model.Uri);

    private IUndoRedoElement GetOrCreateEditStackElement(IReadOnlyList<TextSelection>? beforeCursorState, UndoRedoGroup? group)
    {
        var lastElement = _undoRedoService.GetLastElement(_model.Uri);
        if (lastElement is SingleModelEditStackElement single && single.CanAppend(_model))
        {
            return single;
        }

        if (lastElement is MultiModelEditStackElement multi && multi.CanAppend(_model))
        {
            return multi;
        }

        var newElement = new SingleModelEditStackElement(TypingLabel, "undoredo.textBufferEdit", _model, beforeCursorState);
        _undoRedoService.PushElement(newElement, group);
        return newElement;
    }

    private void Append(
        IUndoRedoElement element,
        List<TextChange> textChanges,
        IReadOnlyList<TextSelection>? afterCursorState)
    {
        var afterEOL = GetModelEOL(_model);
        var afterVersionId = _model.GetAlternativeVersionId();
        if (element is SingleModelEditStackElement single)
        {
            single.Append(_model, textChanges, afterEOL, afterVersionId, afterCursorState);
        }
        else if (element is MultiModelEditStackElement multi)
        {
            multi.Append(_model, textChanges, afterEOL, afterVersionId, afterCursorState);
        }
    }

    public void PushEOL(EndOfLineSequence eol)
    {
        var editStackElement = GetOrCreateEditStackElement(null, null);
        _model.SetEOL(eol);
        Append(editStackElement, new List<TextChange>(), null);
    }

    public IReadOnlyList<TextSelection>? PushEditOperation(
        IReadOnlyList<TextSelection>? beforeCursorState,
        List<ValidAnnotatedEditOperation> editOperations,
        CursorStateComputer? cursorStateComputer,
        UndoRedoGroup? group = null,
        TextModelEditSource? reason = null)
    {
        var editStackElement = GetOrCreateEditStackElement(beforeCursorState, group);
        var inverseEditOperations = _model.ApplyValidatedEdits(
            editOperations, computeUndoEdits: true, reason ?? EditSources.Unknown("pushEditOperation"))!;
        var afterCursorState = ComputeCursorState(cursorStateComputer, inverseEditOperations);

        // Sort by old position; the index keeps equal positions in their order (upstream sorts stably).
        var textChanges = new List<(int Index, TextChange TextChange)>(inverseEditOperations.Count);
        for (var i = 0; i < inverseEditOperations.Count; i++)
        {
            textChanges.Add((i, inverseEditOperations[i].TextChange));
        }

        textChanges.Sort(static (a, b) =>
            a.TextChange.OldPosition == b.TextChange.OldPosition
                ? a.Index - b.Index
                : a.TextChange.OldPosition - b.TextChange.OldPosition);

        Append(editStackElement, textChanges.Select(op => op.TextChange).ToList(), afterCursorState);
        return afterCursorState;
    }

    private static IReadOnlyList<TextSelection>? ComputeCursorState(
        CursorStateComputer? cursorStateComputer,
        IReadOnlyList<ValidEditOperation> inverseEditOperations)
    {
        if (cursorStateComputer is null)
        {
            return null;
        }

        try
        {
            return cursorStateComputer(inverseEditOperations);
        }
        catch (Exception e)
        {
            // Upstream reports the error (onUnexpectedError) and goes on without a cursor state.
            UnexpectedErrors.Report(e);
            return null;
        }
    }
}
