// The ISuggestEditor of a CodeEditor: the editor events, options and services the suggest model of the engine reads
// (upstream's SuggestModel gets them from ICodeEditor and its service container; suggestModel.ts, commit b57d5c9e, MIT
// license). Timers are dispatcher timers; a zero delay posts to the dispatcher.

using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Suggest;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

internal sealed class CodeEditorSuggestEditor : ISuggestEditor, IDisposable
{
    private readonly CodeEditor _editor;
    private readonly Func<SuggestOptions> _options;
    private readonly Action<CursorStateChangedEvent> _onCursor;
    private readonly Action<ModelContentChangedEvent> _onContent;
    private readonly Action<ModelLanguageChangedEvent> _onLanguage;
    private readonly Action<string> _onType;

    public CodeEditorSuggestEditor(CodeEditor editor, Func<SuggestOptions> options)
    {
        _editor = editor;
        _options = options;
        _onCursor = e => DidChangeCursorSelection?.Invoke(e);
        _onContent = _ => DidChangeModelContent?.Invoke();
        _onLanguage = _ => DidChangeModelLanguage?.Invoke();
        _onType = text => DidType?.Invoke(text);
        editor.DidChangeCursorSelection += _onCursor;
        editor.DidChangeModelContent += _onContent;
        editor.DidChangeModelLanguage += _onLanguage;
        editor.DidType += _onType;
    }

    public TextModel? Model => _editor.Model;

    public bool IsReadOnly => _editor.IsReadOnly;

    public SuggestOptions SuggestOptions => _options();

    public LanguageFeatureRegistry<ICompletionItemProvider> CompletionProvider => _editor.LanguageFeatures.CompletionProvider;

    public LanguageConfigurationService? LanguageConfigurationService => _editor.LanguageConfigurationService;

    /// <summary>The snippet provider asked with the others; null until a snippets feature supplies one.</summary>
    public ICompletionItemProvider? SnippetSuggestSupport { get; set; }

    public event Action? DidChangeModel;

    public event Action? DidChangeModelLanguage;

    public event Action? DidChangeConfiguration;

    public event Action<CursorStateChangedEvent>? DidChangeCursorSelection;

    public event Action? DidChangeModelContent;

    public event Action<string>? DidType;

    public event Action? DidCompositionStart;

    public event Action? DidCompositionEnd;

    public TextPosition GetPosition() => _editor.GetPosition() ?? new TextPosition(1, 1);

    public TextSelection GetSelection() => _editor.GetSelection() ?? new TextSelection(1, 1, 1, 1);

    public bool HasWidgetFocus() => _editor.IsKeyboardFocusWithin;

    public bool IsInSnippet() => _editor.ContextKeyService.GetContextKeyValue(CodeEditorContextKeys.InSnippetMode.Key) is true;

    public bool IsInlineSuggestionVisible() => _editor.ContextKeyService.GetContextKeyValue(CodeEditorContextKeys.InlineSuggestionVisible.Key) is true;

    public void Post(Action callback) => Dispatcher.UIThread.Post(callback);

    public IDisposable SetTimeout(Action callback, int delay)
    {
        if (delay <= 0)
        {
            var posted = new PostedCallback(callback);
            Dispatcher.UIThread.Post(posted.Run);
            return posted;
        }

        return DispatcherTimer.RunOnce(callback, TimeSpan.FromMilliseconds(delay));
    }

    /// <summary>Tells the model that the options changed (upstream's onDidChangeConfiguration).</summary>
    public void RaiseConfigurationChanged() => DidChangeConfiguration?.Invoke();

    /// <summary>Tells the model that the editor's model was replaced (upstream's onDidChangeModel).</summary>
    public void RaiseModelChanged() => DidChangeModel?.Invoke();

    /// <summary>Composition (IME) started or ended.</summary>
    public void RaiseComposition(bool started)
    {
        if (started)
        {
            DidCompositionStart?.Invoke();
        }
        else
        {
            DidCompositionEnd?.Invoke();
        }
    }

    public void Dispose()
    {
        _editor.DidChangeCursorSelection -= _onCursor;
        _editor.DidChangeModelContent -= _onContent;
        _editor.DidChangeModelLanguage -= _onLanguage;
        _editor.DidType -= _onType;
    }

    private sealed class PostedCallback(Action callback) : IDisposable
    {
        private bool _cancelled;

        public void Run()
        {
            if (!_cancelled)
            {
                callback();
            }
        }

        public void Dispose() => _cancelled = true;
    }
}
