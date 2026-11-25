// The ISnippetEditor of the snippet tests: upstream's test code editor (createTestCodeEditor with a label service, a
// workspace service and the language configuration service) as the snippet code sees it, over TestCodeEditor.
// Upstream's tests reach the code editor widget directly; here the widget parts the snippet code uses are this adapter:
// selections, executeEdits, the cursor and content events, the editor's context keys and its language features, and
// showSimpleSuggestions, which records the provider (upstream's suggest controller is a stub in the controller tests).

using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Contrib.Snippet;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Tests;

internal sealed class SnippetTestEditor : ISnippetEditor, IDisposable
{
    private readonly ContextKeyService _contextKeyService = new();

    public SnippetTestEditor(TestCodeEditor editor, SnippetVariableServices? variableServices = null, LanguageFeatures? languageFeatures = null, int editorId = 1)
    {
        Editor = editor;
        DecorationOwnerId = editorId;
        VariableServices = variableServices ?? SnippetVariableServices.Default;
        LanguageFeatures = languageFeatures;
    }

    /// <summary>The editor of the lines joined with \n, with an owned model.</summary>
    public static SnippetTestEditor Create(string text, TestCodeEditorOptions? options = null) =>
        new(TestCodeEditor.Create(text, options), editorId: options?.EditorId ?? 1);

    public TestCodeEditor Editor { get; }

    public TextModel Model => Editor.Model;

    public int DecorationOwnerId { get; }

    public LanguageConfigurationService LanguageConfigurationService => Editor.LanguageConfigurationService;

    public LanguageFeatures? LanguageFeatures { get; }

    public IContextKeyService ContextKeyService => _contextKeyService;

    public MultiCursorPaste MultiCursorPaste => Editor.ViewModel.Configuration.Options.Cursor.MultiCursorPaste;

    public SnippetVariableServices VariableServices { get; set; }

    /// <summary>The providers <see cref="ShowSimpleSuggestions"/> was asked to show, in order.</summary>
    public List<ICompletionItemProvider> ShownSuggestions { get; } = [];

    public TextSelection GetSelection() => Editor.GetSelection();

    public IReadOnlyList<TextSelection> GetSelections() => Editor.GetSelections();

    public void SetSelections(IReadOnlyList<TextSelection> selections) => Editor.SetSelections(selections);

    public void SetSelection(TextSelection selection) => Editor.SetSelection(selection);

    public bool ExecuteEdits(string source, IReadOnlyList<SingleEditOperation> edits, CursorStateComputer cursorStateComputer) =>
        Editor.ExecuteEdits(source, edits, cursorStateComputer);

    public void RevealRange(TextRange range)
    {
    }

    public void RevealPositionInCenterIfOutsideViewport(TextPosition position)
    {
    }

    public IDisposable OnDidChangeCursorSelection(Action callback)
    {
        void Handler(OutgoingViewModelEvent e)
        {
            if (e is CursorStateChangedEvent)
            {
                callback();
            }
        }

        Editor.ViewModel.OutgoingEvent += Handler;
        return new Disposer(() => Editor.ViewModel.OutgoingEvent -= Handler);
    }

    public IDisposable OnDidChangeModelContent(Action<ModelContentChangedEvent> callback)
    {
        Model.DidChangeContent += callback;
        return new Disposer(() => Model.DidChangeContent -= callback);
    }

    public void ShowSimpleSuggestions(ICompletionItemProvider provider) => ShownSuggestions.Add(provider);

    /// <summary>Upstream's trigger('test', 'type', { text }).</summary>
    public void Type(string text) => Editor.Type(text, "test");

    public void Dispose()
    {
        _contextKeyService.Dispose();
        Editor.Dispose();
    }

    private sealed class Disposer(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            _dispose?.Invoke();
            _dispose = null;
        }
    }
}
