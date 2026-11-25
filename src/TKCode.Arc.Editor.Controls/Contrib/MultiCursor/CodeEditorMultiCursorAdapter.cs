// The CodeEditor as the multi-cursor feature's editor (IMultiCursorEditor, the parts of upstream's ICodeEditor that
// src/vs/editor/contrib/multicursor/browser/multicursor.ts reads; commit b57d5c9e, MIT license), plus the timer the
// selection highlighter schedules with.

using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.MultiCursor;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using EditorViewModel = TKCode.Arc.Editor.ViewModel.ViewModel;

namespace TKCode.Arc;

/// <summary>Adapts a <see cref="CodeEditor"/> to <see cref="IMultiCursorEditor"/>; dispose it to unsubscribe.</summary>
internal sealed class CodeEditorMultiCursorAdapter : IMultiCursorEditor, IDisposable
{
    /// <summary>The id of the find feature's contribution; when it implements <see cref="IMultiCursorFindController"/> the actions use its state.</summary>
    public const string FindControllerId = "editor.contrib.findController";

    private readonly CodeEditor _editor;

    public CodeEditorMultiCursorAdapter(CodeEditor editor)
    {
        _editor = editor;
        editor.DidChangeCursorSelection += OnCursorSelection;
        editor.DidChangeTextFocus += OnTextFocus;
        editor.DidChangeModelContent += OnModelContent;
    }

    public int EditorId => 0;

    public TextModel? Model => _editor.Model;

    public EditorViewModel? ViewModel => _editor.ViewModel;

    public bool HasTextFocus => _editor.IsTextFocused;

    public string WordSeparators => _editor.ViewModel?.CursorConfig.WordSeparators ?? WordHelper.UsualWordSeparators;

    public MultiCursorOptions MultiCursorOptions => MultiCursorContribution.Get(_editor)?.Options ?? MultiCursorOptions.Default;

    public IMultiCursorFindController? FindController =>
        _editor.GetContribution<ICodeEditorContribution>(FindControllerId) as IMultiCursorFindController
        ?? MultiCursorContribution.Get(_editor)?.StandaloneFindController;

    public MultiCursorSelectionController? MultiCursorController => MultiCursorContribution.Get(_editor)?.Controller;

    public event Action<CursorStateChangedEvent>? DidChangeCursorSelection;

    public event Action? DidFocusEditorText;

    public event Action? DidBlurEditorText;

    public event Action<string>? DidChangeConfiguration;

    public event Action? DidChangeModelContent;

    public IReadOnlyList<TextSelection> GetSelections() => _editor.GetSelections();

    public void SetSelections(IReadOnlyList<TextSelection> selections, string source = "api", CursorChangeReason reason = CursorChangeReason.NotSet) =>
        _editor.SetSelections(selections, source, reason);

    public void RevealRangeInCenterIfOutsideViewport(TextRange range, ScrollType scrollType) =>
        _editor.RevealRange(range, VerticalRevealType.CenterIfOutsideViewport, scrollType);

    /// <summary>Reports an option change to the subscribers (upstream's onDidChangeConfiguration).</summary>
    public void RaiseConfigurationChanged(string option) => DidChangeConfiguration?.Invoke(option);

    private void OnCursorSelection(CursorStateChangedEvent e) => DidChangeCursorSelection?.Invoke(e);

    private void OnTextFocus(bool focused)
    {
        if (focused)
        {
            DidFocusEditorText?.Invoke();
        }
        else
        {
            DidBlurEditorText?.Invoke();
        }
    }

    private void OnModelContent(ModelContentChangedEvent e) => DidChangeModelContent?.Invoke();

    public void Dispose()
    {
        _editor.DidChangeCursorSelection -= OnCursorSelection;
        _editor.DidChangeTextFocus -= OnTextFocus;
        _editor.DidChangeModelContent -= OnModelContent;
    }

    /// <summary>Runs <paramref name="action"/> once after <paramref name="delay"/> on the UI thread; dispose the result to cancel.</summary>
    public static IDisposable RunAfter(TimeSpan delay, Action action)
    {
        var timer = new DispatcherTimer(delay, DispatcherPriority.Background, (sender, _) =>
        {
            ((DispatcherTimer)sender!).Stop();
            action();
        });
        timer.Start();
        return new StopTimer(timer);
    }

    private sealed class StopTimer(DispatcherTimer timer) : IDisposable
    {
        public void Dispose() => timer.Stop();
    }
}
