// The CodeEditor as find sees it: the IFindEditor of the engine's find (the part of upstream's ICodeEditor that
// src/vs/editor/contrib/find/browser/findController.ts and findModel.ts call; commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the find options are a property of this adapter (upstream: the editor's `find` option), the toggles persist per
//    editor (upstream: in the workspace storage) and there is no find clipboard (upstream: macOS's find pasteboard).

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Contrib.Find;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.ViewModel;
using EditCommand = TKCode.Arc.Editor.Commands.ICommand;

namespace TKCode.Arc;

/// <summary>The <see cref="IFindEditor"/> of a <see cref="CodeEditor"/>.</summary>
internal sealed class CodeEditorFindEditor : IFindEditor, IDisposable
{
    private readonly CodeEditor _editor;
    private readonly Action<CursorStateChangedEvent> _onCursor;
    private readonly Action<ModelContentChangedEvent> _onContent;

    public CodeEditorFindEditor(CodeEditor editor)
    {
        _editor = editor;
        _onCursor = e => DidChangeCursorPosition?.Invoke(new FindCursorPositionChangedEvent(e.Reason));
        _onContent = e => DidChangeModelContent?.Invoke(e);
        _editor.DidChangeCursorSelection += _onCursor;
        _editor.DidChangeModelContent += _onContent;
    }

    public CodeEditor Editor => _editor;

    public int EditorId => _editor.EditorId;

    /// <summary>The find options (upstream's <c>find</c> editor option).</summary>
    public EditorFindOptions FindOptions { get; set; } = EditorFindOptions.Default;

    public string WordSeparators => _editor.Configuration?.Options.Cursor.WordSeparators ?? EditorOptions.UsualWordSeparators;

    public bool IsReadOnly => _editor.IsReadOnly;

    public IContextKeyService ContextKeyService => _editor.ContextKeyService;

    public IdleScheduler Scheduler => CodeEditor.IdleScheduler;

    public IFindStorage? Storage { get; } = new InMemoryFindStorage();

    public event Action<FindCursorPositionChangedEvent>? DidChangeCursorPosition;

    public event Action<ModelContentChangedEvent>? DidChangeModelContent;

    public TextModel? GetModel() => _editor.Model;

    public TextSelection GetSelection() => _editor.GetSelection() ?? new TextSelection(1, 1, 1, 1);

    public IReadOnlyList<TextSelection> GetSelections()
    {
        var selections = _editor.GetSelections();
        return selections.Count > 0 ? selections : [GetSelection()];
    }

    public TextPosition GetPosition() => _editor.GetPosition() ?? new TextPosition(1, 1);

    public void SetSelection(TextSelection selection) => _editor.SetSelection(selection);

    public void SetSelections(IReadOnlyList<TextSelection> selections) => _editor.SetSelections(selections);

    public void RevealRangeInCenterIfOutsideViewport(TextRange range) =>
        _editor.RevealRange(range, VerticalRevealType.CenterIfOutsideViewport, ScrollType.Smooth);

    public bool PushUndoStop() => _editor.PushUndoStop();

    public void ExecuteCommand(string source, EditCommand command) => _editor.ViewModel?.ExecuteCommand(command, source);

    public double GetLayoutHeight() => _editor.GetLayoutSize().Height;

    public WordAtPosition? GetConfiguredWordAtPosition(TextPosition position) =>
        WordOperations.GetWordAtPosition(_editor.Model, WordSeparators, [], position);

    public void Focus() => _editor.Focus();

    public void Dispose()
    {
        _editor.DidChangeCursorSelection -= _onCursor;
        _editor.DidChangeModelContent -= _onContent;
    }
}
