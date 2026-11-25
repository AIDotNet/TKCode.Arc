// Ported from vscode (src/vs/editor/contrib/stickyScroll/browser/stickyScrollController.ts: StickyScrollController;
// the update scheduling of stickyScrollProvider.ts; and the actions of stickyScrollActions.ts that need no focus;
// commit b57d5c9e), MIT license.
// Differences:
//  - the options are the CodeEditor properties StickyScroll and StickyScrollMaxLineCount (Monaco's
//    stickyScroll.enabled / maxLineCount); the default model is the outline model, as upstream's default;
//  - the candidate provider is the engine's (StickyLineCandidateProvider); its updates are debounced here: 300 ms after
//    an edit, a language change, a change of the symbol or folding providers or of the hidden areas (upstream: a
//    RunOnceScheduler(50) and the model provider's Delayer(300));
//  - no keyboard focus on the sticky lines (focus / focusNext / goToFocused), no go-to-definition on Ctrl+hover, no
//    folding icons, no context menu; shift+click reveals the end of the scope (upstream: shift+click and the
//    showEndForLine rendering);
//  - editor.action.toggleStickyScroll flips the StickyScroll property (upstream changes the user setting).

using Avalonia;
using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.StickyScroll;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

/// <summary>Keeps the lines that start the scopes around the top of the view stuck to the top (upstream's StickyScrollController).</summary>
internal sealed class StickyScrollController : ICodeEditorContribution
{
    public const string Id = "store.contrib.stickyScrollController";

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new StickyScrollController(editor), EditorContributionInstantiation.AfterFirstRender);

    /// <summary>Turns sticky scroll on or off (upstream's editor.action.toggleStickyScroll).</summary>
    public static readonly EditorAction ToggleStickyScrollAction = EditorAction.Create(
        "editor.action.toggleStickyScroll",
        "Toggle Editor Sticky Scroll",
        null,
        static (editor, _) => editor.StickyScroll = !editor.StickyScroll);

    private const int UpdateDelayMs = 300;

    private readonly CodeEditor _editor;
    private readonly StickyLineCandidateProvider _provider;
    private readonly CodeEditorStickyScrollWidget _widget;
    private readonly IContextKey<bool> _stickyScrollVisibleContextKey;
    private readonly DispatcherTimer _updateTimer;
    private bool _enabled;
    private bool _widgetAdded;
    private int _maxStickyLines = int.MaxValue;
    private Task _lastUpdate = Task.CompletedTask;

    private StickyScrollController(CodeEditor editor)
    {
        _editor = editor;
        _provider = new StickyLineCandidateProvider(
            new StickyScrollViewModelEditor(() => editor.Model, () => editor.ViewModel),
            editor.LanguageFeatures,
            editor.LanguageConfigurationService);
        _provider.DidChangeStickyScroll += RenderStickyScroll;
        _widget = new CodeEditorStickyScrollWidget(editor);
        _widget.Bind(CodeEditorStickyScrollWidget.BackgroundProperty, editor.GetObservable(CodeEditor.BackgroundProperty));
        _stickyScrollVisibleContextKey = editor.CreateContextKey(EditorContextKeys.StickyScrollVisible);
        _updateTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(UpdateDelayMs), DispatcherPriority.Background, (_, _) =>
        {
            _updateTimer!.Stop();
            _lastUpdate = UpdateNow();
        });

        editor.PropertyChanged += OnEditorPropertyChanged;
        ReadConfiguration();
    }

    public static StickyScrollController? Get(CodeEditor editor) => editor.GetContribution<StickyScrollController>(Id);

    /// <summary>The candidate provider (upstream's stickyScrollCandidateProvider).</summary>
    internal StickyLineCandidateProvider CandidateProvider => _provider;

    /// <summary>The widget (for tests).</summary>
    internal CodeEditorStickyScrollWidget Widget => _widget;

    /// <summary>The lines shown now (upstream's stickyScrollWidgetState).</summary>
    internal StickyScrollWidgetState WidgetState => _widget.State;

    /// <summary>The update that runs or ran last (for tests).</summary>
    internal Task LastUpdate => _lastUpdate;

    /// <summary>Computes the scopes now instead of after the debounce.</summary>
    internal Task UpdateNow()
    {
        _updateTimer.Stop();
        _lastUpdate = _enabled ? _provider.Update() : Task.CompletedTask;
        return _lastUpdate;
    }

    public void Dispose()
    {
        _editor.PropertyChanged -= OnEditorPropertyChanged;
        _provider.DidChangeStickyScroll -= RenderStickyScroll;
        Disable();
        _provider.Dispose();
    }

    private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == CodeEditor.StickyScrollProperty || e.Property == CodeEditor.StickyScrollMaxLineCountProperty)
        {
            // the view model's cursor padding follows the sticky lines (ViewModelOptions.StickyScroll)
            _editor.View?.UpdateConfiguration();
            ReadConfiguration();
        }
    }

    // upstream's _readConfiguration and StickyLineCandidateProvider.readConfiguration
    private void ReadConfiguration()
    {
        if (!_editor.StickyScroll)
        {
            Disable();
            return;
        }

        if (!_enabled)
        {
            _enabled = true;
            _editor.AddOverlayWidget(_widget);
            _widgetAdded = true;
            _editor.DidScrollChange += OnScroll;
            _editor.DidLayoutChange += OnLayout;
            _editor.DidChangeViewModel += OnViewModel;
            _editor.DidChangeModelTokens += OnTokens;
            _editor.DidChangeModelContent += OnContent;
            _editor.DidChangeModelLanguage += OnLanguage;
            _editor.DidChangeHiddenAreas += OnHiddenAreas;
            _editor.LanguageFeatures.DocumentSymbolProvider.DidChange += OnProviders;
            _editor.LanguageFeatures.FoldingRangeProvider.DidChange += OnProviders;
            OnResize();
            _lastUpdate = UpdateNow();
        }
        else
        {
            OnResize();
        }
    }

    private void Disable()
    {
        _updateTimer.Stop();
        if (_enabled)
        {
            _enabled = false;
            _editor.DidScrollChange -= OnScroll;
            _editor.DidLayoutChange -= OnLayout;
            _editor.DidChangeViewModel -= OnViewModel;
            _editor.DidChangeModelTokens -= OnTokens;
            _editor.DidChangeModelContent -= OnContent;
            _editor.DidChangeModelLanguage -= OnLanguage;
            _editor.DidChangeHiddenAreas -= OnHiddenAreas;
            _editor.LanguageFeatures.DocumentSymbolProvider.DidChange -= OnProviders;
            _editor.LanguageFeatures.FoldingRangeProvider.DidChange -= OnProviders;
        }

        if (_widgetAdded)
        {
            _editor.RemoveOverlayWidget(_widget);
            _widgetAdded = false;
        }

        _widget.SetState(null);
        _stickyScrollVisibleContextKey.Set(false);
    }

    private void ScheduleUpdate()
    {
        _updateTimer.Stop();
        _updateTimer.Start();
    }

    private void OnScroll(ScrollChangedEvent e)
    {
        if (e.ScrollTopChanged)
        {
            RenderStickyScroll();
        }
    }

    private void OnLayout(Size size) => OnResize();

    private void OnViewModel(Editor.ViewModel.ViewModel viewModel) => OnResize();

    private void OnContent(ModelContentChangedEvent e) => ScheduleUpdate();

    private void OnLanguage(ModelLanguageChangedEvent e) => ScheduleUpdate();

    private void OnHiddenAreas(object? e)
    {
        RenderStickyScroll();
        ScheduleUpdate();
    }

    private void OnProviders(int count) => ScheduleUpdate();

    private void OnTokens(ModelTokensChangedEvent e)
    {
        // upstream's _needsUpdate: the tokens of a sticky line changed
        foreach (var line in _widget.LineNumbers)
        {
            foreach (var range in e.Ranges)
            {
                if (line >= range.FromLineNumber && line <= range.ToLineNumber)
                {
                    _widget.InvalidateVisual();
                    return;
                }
            }
        }
    }

    private void OnResize()
    {
        // Make sure sticky scroll doesn't take up more than 25% of the editor
        var height = _editor.GetLayoutSize().Height;
        var lineHeight = _editor.View?.LineHeight ?? 0;
        _maxStickyLines = lineHeight > 0 && height > 0 ? (int)Math.Round(height / lineHeight * 0.25, MidpointRounding.AwayFromZero) : int.MaxValue;
        RenderStickyScroll();
    }

    /// <summary>Finds the lines to show for the scroll position and shows them (upstream's _renderStickyScroll / _updateState).</summary>
    internal void RenderStickyScroll()
    {
        if (!_enabled || _editor.ViewModel is null || _editor.Model.IsTooLargeForTokenization())
        {
            _widget.SetState(null);
            _stickyScrollVisibleContextKey.Set(false);
            return;
        }

        var version = _provider.GetVersionId();
        if (version is not null && version != _editor.Model.GetVersionId())
        {
            // the scopes are of an older text: wait for the update
            return;
        }

        var state = FindScrollWidgetState();
        _stickyScrollVisibleContextKey.Set(state.StartLineNumbers.Count > 0);
        _widget.SetState(state);
        _editor.LayoutOverlayWidget(_widget);
    }

    /// <summary>The lines to show for the current scroll position (upstream's findScrollWidgetState).</summary>
    internal StickyScrollWidgetState FindScrollWidgetState()
    {
        var maxNumberStickyLines = Math.Min(_maxStickyLines, _editor.StickyScrollMaxLineCount);
        return StickyScrollState.Find(new StickyScrollViewModelEditor(() => _editor.Model, () => _editor.ViewModel), _provider, maxNumberStickyLines);
    }

    /// <summary>A click on sticky line <paramref name="index"/> at <paramref name="x"/>: reveals that position, or the end of the scope with Shift.</summary>
    internal void OnLineClicked(int index, double x, bool shift)
    {
        var state = _widget.State;
        if (index < 0 || index >= state.StartLineNumbers.Count)
        {
            return;
        }

        if (shift)
        {
            // upstream: shift+click reveals the last line of the scope, centered if outside the viewport
            var end = Math.Min(state.EndLineNumbers[index], _editor.Model.GetLineCount());
            RevealPosition(new TextPosition(end, 1), VerticalRevealType.CenterIfOutsideViewport);
            return;
        }

        var lineNumber = state.StartLineNumbers[index];
        var column = _editor.View?.GetStickyLineColumnAtX(lineNumber, x) ?? 1;
        RevealPosition(new TextPosition(lineNumber, column), VerticalRevealType.Simple);
    }

    // upstream's _reveaInEditor
    private void RevealPosition(TextPosition position, VerticalRevealType revealType)
    {
        _editor.RevealRange(new TextRange(position), revealType, ScrollType.Immediate);
        _editor.SetSelection(new TextSelection(position.LineNumber, position.Column, position.LineNumber, position.Column));
        _editor.Focus();
    }
}
