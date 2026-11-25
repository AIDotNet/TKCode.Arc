// The code editor as the snippet engine sees it (upstream's IActiveCodeEditor in src/vs/editor/contrib/snippet/browser/
// snippetSession.ts and snippetController2.ts, commit b57d5c9e), MIT license.
// Differences: decorations are the model's with owner 0 (the editor owns its model, so no other editor shows them);
// showSimpleSuggestions is deferred with a dispatcher post (upstream's queueMicrotask) and goes to the snippet
// controller, which shows the suggest widget's list or its own.

using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Contrib.Snippet;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

/// <summary>A <see cref="CodeEditor"/> as an <see cref="ISnippetEditor"/>.</summary>
internal sealed class CodeEditorSnippetEditor(CodeEditor editor, Action<ICompletionItemProvider> showSimpleSuggestions) : ISnippetEditor
{
    public CodeEditor Editor => editor;

    public TextModel Model => editor.Model;

    public int DecorationOwnerId => 0;

    public LanguageConfigurationService LanguageConfigurationService => editor.LanguageConfigurationService;

    public LanguageFeatures? LanguageFeatures => editor.LanguageFeatures;

    public IContextKeyService ContextKeyService => editor.ContextKeyService;

    public MultiCursorPaste MultiCursorPaste => editor.Configuration?.Options.Cursor.MultiCursorPaste ?? MultiCursorPaste.Spread;

    public SnippetVariableServices VariableServices { get; set; } = SnippetVariableServices.Default;

    public TextSelection GetSelection() => editor.GetSelection() ?? new TextSelection(1, 1, 1, 1);

    public IReadOnlyList<TextSelection> GetSelections() => editor.GetSelections();

    public void SetSelections(IReadOnlyList<TextSelection> selections) => editor.SetSelections(selections);

    public bool ExecuteEdits(string source, IReadOnlyList<SingleEditOperation> edits, CursorStateComputer cursorStateComputer) =>
        editor.ExecuteEdits(source, edits, cursorStateComputer);

    public void RevealRange(TextRange range) => editor.RevealRange(range);

    public void RevealPositionInCenterIfOutsideViewport(TextPosition position) =>
        editor.RevealRange(new TextRange(position.LineNumber, position.Column, position.LineNumber, position.Column), VerticalRevealType.CenterIfOutsideViewport);

    public IDisposable OnDidChangeCursorSelection(Action callback)
    {
        void Handler(CursorStateChangedEvent _) => callback();
        editor.DidChangeCursorSelection += Handler;
        return new Subscription(() => editor.DidChangeCursorSelection -= Handler);
    }

    public IDisposable OnDidChangeModelContent(Action<ModelContentChangedEvent> callback)
    {
        editor.DidChangeModelContent += callback;
        return new Subscription(() => editor.DidChangeModelContent -= callback);
    }

    public void ShowSimpleSuggestions(ICompletionItemProvider provider) =>
        Dispatcher.UIThread.Post(() => showSimpleSuggestions(provider));

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            _dispose?.Invoke();
            _dispose = null;
        }
    }
}
