// The test editor of the multi-cursor tests: upstream's withTestCodeEditor plus
// editor.registerAndInstantiateContribution(CommonFindController / MultiCursorSelectionController / SelectionHighlighter)
// (src/vs/editor/contrib/multicursor/test/browser/multicursor.test.ts, commit b57d5c9e; MIT license), as an
// IMultiCursorEditor over a TestCodeEditor. Timers (the selection highlighter's RunOnceScheduler) are queued and run by
// RunAllTimers, like sinon's fake timers.

using TKCode.Arc.Editor.Contrib.MultiCursor;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using EditorViewModel = TKCode.Arc.Editor.ViewModel.ViewModel;

namespace TKCode.Arc.Editor.Tests;

internal sealed class TestMultiCursorEditor : IMultiCursorEditor, IDisposable
{
    private readonly List<(Action Action, bool Cancelled)> _timers = [];
    private bool _hasTextFocus;
    private MultiCursorOptions _options;

    public TestMultiCursorEditor(TestCodeEditor editor, MultiCursorOptions? options = null, bool hasTextFocus = true)
    {
        Editor = editor;
        _options = options ?? MultiCursorOptions.Default;
        _hasTextFocus = hasTextFocus;
        editor.HasTextFocus = hasTextFocus;
        editor.ViewModel.SetHasFocus(hasTextFocus);
        editor.ViewModel.OutgoingEvent += OnOutgoingEvent;
        FindControllerImpl = new StandaloneMultiCursorFindController();
        MultiCursorController = new MultiCursorSelectionController(this);
    }

    public TestCodeEditor Editor { get; }

    public StandaloneMultiCursorFindController FindControllerImpl { get; }

    public SelectionHighlighter? SelectionHighlighter { get; private set; }

    public int EditorId => 1;

    public TextModel? Model => Editor.Model;

    public EditorViewModel? ViewModel => Editor.ViewModel;

    public bool HasTextFocus => _hasTextFocus;

    public string WordSeparators => Editor.ViewModel.CursorConfig.WordSeparators;

    public MultiCursorOptions MultiCursorOptions => _options;

    public IMultiCursorFindController? FindController => FindControllerImpl;

    public MultiCursorSelectionController? MultiCursorController { get; private set; }

    public event Action<CursorStateChangedEvent>? DidChangeCursorSelection;

    public event Action? DidFocusEditorText;

    public event Action? DidBlurEditorText;

    public event Action<string>? DidChangeConfiguration;

    public event Action? DidChangeModelContent;

    public IReadOnlyList<TextSelection> GetSelections() => Editor.GetSelections();

    public void SetSelections(IReadOnlyList<TextSelection> selections, string source = "api", CursorChangeReason reason = CursorChangeReason.NotSet) =>
        Editor.SetSelections(selections, source, reason);

    public void SetSelection(TextSelection selection) => SetSelections([selection]);

    public void RevealRangeInCenterIfOutsideViewport(TextRange range, ScrollType scrollType)
    {
    }

    /// <summary>Upstream's TestCodeEditor.setHasTextFocus plus the view model's focus (the tests' setTextFocus).</summary>
    public void SetTextFocus(bool hasFocus)
    {
        if (_hasTextFocus == hasFocus)
        {
            return;
        }

        _hasTextFocus = hasFocus;
        Editor.HasTextFocus = hasFocus;
        Editor.ViewModel.SetHasFocus(hasFocus);
        if (hasFocus)
        {
            DidFocusEditorText?.Invoke();
        }
        else
        {
            DidBlurEditorText?.Invoke();
        }
    }

    /// <summary>Upstream's updateOptions({ selectedTextMatchMode }).</summary>
    public void UpdateOptions(MultiCursorOptions options)
    {
        var old = _options;
        _options = options;
        if (old.SelectedTextMatchMode != options.SelectedTextMatchMode)
        {
            DidChangeConfiguration?.Invoke(MultiCursorOptionNames.SelectedTextMatchMode);
        }

        if (old.SelectionHighlight != options.SelectionHighlight)
        {
            DidChangeConfiguration?.Invoke(MultiCursorOptionNames.SelectionHighlight);
        }
    }

    /// <summary>Upstream's registerAndInstantiateContribution(SelectionHighlighter).</summary>
    public SelectionHighlighter AddSelectionHighlighter() =>
        SelectionHighlighter = new SelectionHighlighter(this, static _ => false, RunAfter);

    /// <summary>Disposes the multi-cursor controller (upstream: MultiCursorSelectionController.get(editor).dispose()).</summary>
    public void DisposeController() => MultiCursorController?.Dispose();

    /// <summary>The ranges of the selection highlights, sorted.</summary>
    public List<TextRange> SelectionHighlights() =>
        Editor.Model.GetAllDecorations()
            .Where(static d => d.Options.ClassName == "selectionHighlight")
            .Select(static d => d.Range)
            .OrderBy(static r => r, Comparer<TextRange>.Create(TextRange.CompareRangesUsingStarts))
            .ToList();

    /// <summary>Runs the pending timers (sinon's clock.runAll).</summary>
    public void RunAllTimers()
    {
        while (_timers.Count > 0)
        {
            var timers = _timers.ToList();
            _timers.Clear();
            foreach (var (action, cancelled) in timers)
            {
                if (!cancelled)
                {
                    action();
                }
            }
        }
    }

    private IDisposable RunAfter(TimeSpan delay, Action action)
    {
        var index = _timers.Count;
        _timers.Add((action, false));
        return new Cancel(() =>
        {
            if (index < _timers.Count && ReferenceEquals(_timers[index].Action, action))
            {
                _timers[index] = (action, true);
            }
        });
    }

    private void OnOutgoingEvent(OutgoingViewModelEvent e)
    {
        switch (e)
        {
            case CursorStateChangedEvent cursor:
                DidChangeCursorSelection?.Invoke(cursor);
                break;
            case OutgoingModelContentChangedEvent:
                DidChangeModelContent?.Invoke();
                break;
        }
    }

    public void Dispose()
    {
        SelectionHighlighter?.Dispose();
        MultiCursorController?.Dispose();
        Editor.ViewModel.OutgoingEvent -= OnOutgoingEvent;
        Editor.Dispose();
    }

    private sealed class Cancel(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}
