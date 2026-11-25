// The IFindEditor of the find tests: upstream's tests run find on its test code editor (withTestCodeEditor); here the
// test code editor is wrapped in the interface find runs on. The context keys, storage and clipboard are the test's
// (upstream: the editor's scoped context key service, an InMemoryStorageService and a TestClipboardService).

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Contrib.Find;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.ViewModel;
using ICommand = TKCode.Arc.Editor.Commands.ICommand;

namespace TKCode.Arc.Editor.Tests;

internal sealed class TestFindClipboard : IFindClipboard
{
    public string Text { get; set; } = string.Empty;

    public Task<string> ReadFindText() => Task.FromResult(Text);

    public void WriteFindText(string text) => Text = text;
}

internal sealed class TestFindEditor : IFindEditor, IDisposable
{
    private readonly TestCodeEditor _editor;
    private readonly Action<OutgoingViewModelEvent> _onOutgoing;

    public TestFindEditor(TestCodeEditor editor, EditorFindOptions? findOptions = null, IFindStorage? storage = null, IFindClipboard? clipboard = null)
    {
        _editor = editor;
        FindOptions = findOptions ?? EditorFindOptions.Default;
        Storage = storage ?? new InMemoryFindStorage();
        Clipboard = clipboard ?? new TestFindClipboard();
        _onOutgoing = e =>
        {
            switch (e)
            {
                case CursorStateChangedEvent cursor:
                    DidChangeCursorPosition?.Invoke(new FindCursorPositionChangedEvent(cursor.Reason));
                    break;
                case OutgoingModelContentChangedEvent content:
                    DidChangeModelContent?.Invoke(content.Event);
                    break;
            }
        };
        _editor.ViewModel.OutgoingEvent += _onOutgoing;
    }

    public TestCodeEditor Inner => _editor;

    public int EditorId => 1;

    public EditorFindOptions FindOptions { get; set; }

    public string WordSeparators => EditorOptions.UsualWordSeparators;

    public bool IsReadOnly => _editor.IsReadOnly;

    public ContextKeyService ContextKeyServiceInstance { get; } = new();

    public IContextKeyService ContextKeyService => ContextKeyServiceInstance;

    public IdleScheduler Scheduler => IdleScheduler.None;

    public IFindStorage? Storage { get; }

    public IFindClipboard? Clipboard { get; }

    /// <summary>How many times find gave the editor focus.</summary>
    public int FocusCount { get; private set; }

    public event Action<FindCursorPositionChangedEvent>? DidChangeCursorPosition;

    public event Action<ModelContentChangedEvent>? DidChangeModelContent;

    public TextModel? GetModel() => _editor.Model;

    public TextSelection GetSelection() => _editor.GetSelection();

    public IReadOnlyList<TextSelection> GetSelections() => _editor.GetSelections();

    public TextPosition GetPosition() => _editor.GetPosition();

    public void SetPosition(TextPosition position) => _editor.SetPosition(position);

    public void SetSelection(TextSelection selection) => _editor.SetSelection(selection);

    public void SetSelection(TextRange range) => _editor.SetSelection(range);

    public void SetSelections(IReadOnlyList<TextSelection> selections) => _editor.SetSelections(selections);

    public void RevealRangeInCenterIfOutsideViewport(TextRange range)
    {
    }

    public bool PushUndoStop() => _editor.PushUndoStop();

    public void ExecuteCommand(string source, ICommand command) => _editor.ExecuteCommand(source, command);

    public double GetLayoutHeight() => 100;

    public WordAtPosition? GetConfiguredWordAtPosition(TextPosition position) =>
        WordOperations.GetWordAtPosition(_editor.Model, WordSeparators, [], position);

    public void Focus() => FocusCount++;

    public string GetValue() => _editor.GetValue();

    public void Dispose() => _editor.ViewModel.OutgoingEvent -= _onOutgoing;

    /// <summary>Upstream's withTestCodeEditor with the find editor around the test editor.</summary>
    public static void With(string[] lines, Action<TestFindEditor> callback, EditorFindOptions? findOptions = null)
    {
        TestCodeEditor.WithTestCodeEditor(lines, null, (editor, _) =>
        {
            using var findEditor = new TestFindEditor(editor, findOptions);
            callback(findEditor);
        });
    }

    /// <summary>On a model the test owns.</summary>
    public static void With(TextModel model, Action<TestFindEditor> callback, EditorFindOptions? findOptions = null)
    {
        TestCodeEditor.WithTestCodeEditor(model, null, (editor, _) =>
        {
            using var findEditor = new TestFindEditor(editor, findOptions);
            callback(findEditor);
        });
    }

    /// <summary>Upstream's withAsyncTestCodeEditor.</summary>
    public static Task WithAsync(string[] lines, Func<TestFindEditor, Task> callback, EditorFindOptions? findOptions = null, IFindStorage? storage = null, IFindClipboard? clipboard = null) =>
        TestCodeEditor.WithAsyncTestCodeEditor(lines, null, async (editor, _) =>
        {
            using var findEditor = new TestFindEditor(editor, findOptions, storage, clipboard);
            await callback(findEditor);
        });
}
