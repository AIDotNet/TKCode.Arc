// The ISuggestEditor of the suggest model tests (upstream's createMockEditor in suggestModel.test.ts): a TestCodeEditor
// with the editor events the suggest model listens to, the typing and command helpers upstream's tests reach through
// editor.trigger and editor.runCommand, and timers the test runs by hand (upstream: real or faked timeouts).

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Contrib.Suggest;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Tests.Contrib.Suggest;

internal sealed class SuggestTestEditor : ISuggestEditor, IDisposable
{
    private readonly List<Timer> _timers = new();
    private SuggestOptions _options = SuggestOptions.Default;

    public SuggestTestEditor(TextModel model, LanguageFeatureRegistry<ICompletionItemProvider> registry, LanguageConfigurationService? languageConfigurationService = null)
    {
        Editor = TestCodeEditor.Create(model, new TestCodeEditorOptions { LanguageConfigurationService = languageConfigurationService });
        CompletionProvider = registry;
        Editor.ViewModel.OutgoingEvent += OnOutgoingEvent;
    }

    public TestCodeEditor Editor { get; }

    public TextModel? Model => Editor.Model;

    public bool IsReadOnly => Editor.IsReadOnly;

    public SuggestOptions SuggestOptions => _options;

    public LanguageFeatureRegistry<ICompletionItemProvider> CompletionProvider { get; }

    public LanguageConfigurationService? LanguageConfigurationService => Editor.LanguageConfigurationService;

    public ICompletionItemProvider? SnippetSuggestSupport { get; set; }

    public event Action? DidChangeModel;

    public event Action? DidChangeModelLanguage;

    public event Action? DidChangeConfiguration;

    public event Action<CursorStateChangedEvent>? DidChangeCursorSelection;

    public event Action? DidChangeModelContent;

    public event Action<string>? DidType;

    public event Action? DidCompositionStart;

    public event Action? DidCompositionEnd;

    public TextPosition GetPosition() => Editor.GetPosition();

    public TextSelection GetSelection() => Editor.GetSelection();

    public bool HasWidgetFocus() => true;

    public IDisposable SetTimeout(Action callback, int delay)
    {
        var timer = new Timer(callback);
        _timers.Add(timer);
        return timer;
    }

    public void Post(Action callback) => _timers.Add(new Timer(callback));

    /// <summary>Runs the timers that are due, and those they set, until none is left.</summary>
    public void RunTimers()
    {
        for (var guard = 0; guard < 100 && _timers.Count > 0; guard++)
        {
            var timer = _timers[0];
            _timers.RemoveAt(0);
            if (!timer.Cancelled)
            {
                timer.Callback();
            }
        }
    }

    public void UpdateOptions(Func<SuggestOptions, SuggestOptions> update)
    {
        _options = update(_options);
        DidChangeConfiguration?.Invoke();
    }

    /// <summary>editor.trigger('keyboard', Handler.Type, { text }), then the due timers.</summary>
    public void Type(string text)
    {
        Editor.Type(text, "keyboard");
        DidType?.Invoke(text);
        RunTimers();
    }

    public void SetPosition(int lineNumber, int column) => Editor.SetPosition(new TextPosition(lineNumber, column));

    public void SetSelection(int startLine, int startColumn, int endLine, int endColumn) =>
        Editor.SetSelection(new TextSelection(startLine, startColumn, endLine, endColumn));

    public void SetValue(string value) => Editor.SetValue(value);

    public void DeleteLeft()
    {
        CoreEditingCommands.DeleteLeft.RunEditorCommand(Editor, null);
        RunTimers();
    }

    public void CursorLeft()
    {
        CoreNavigationCommands.CursorLeft.RunCoreEditorCommand(Editor.ViewModel, new() { Source = "keyboard" });
        RunTimers();
    }

    public void CursorRight()
    {
        CoreNavigationCommands.CursorRight.RunCoreEditorCommand(Editor.ViewModel, new() { Source = "keyboard" });
        RunTimers();
    }

    public void ExecuteEdits(string source, IReadOnlyList<SingleEditOperation> edits)
    {
        Editor.ExecuteEdits(source, edits);
        RunTimers();
    }

    public void FireCompositionStart() => DidCompositionStart?.Invoke();

    public void FireCompositionEnd() => DidCompositionEnd?.Invoke();

    public void FireModelChanged() => DidChangeModel?.Invoke();

    public void FireLanguageChanged() => DidChangeModelLanguage?.Invoke();

    private void OnOutgoingEvent(OutgoingViewModelEvent e)
    {
        if (e is CursorStateChangedEvent cursor)
        {
            DidChangeCursorSelection?.Invoke(cursor);
        }
        else if (e is OutgoingModelContentChangedEvent)
        {
            DidChangeModelContent?.Invoke();
        }
    }

    public void Dispose()
    {
        Editor.ViewModel.OutgoingEvent -= OnOutgoingEvent;
        Editor.Dispose();
    }

    private sealed class Timer(Action callback) : IDisposable
    {
        public Action Callback { get; } = callback;

        public bool Cancelled { get; private set; }

        public void Dispose() => Cancelled = true;
    }
}
