// Ported from vscode (src/vs/editor/contrib/folding/browser/folding.ts: FoldingController and RangesLimitReporter;
// the look of folding.css; commit b57d5c9e), MIT license. The actions are in FoldingActions.cs, the gutter column in
// FoldingController.Gutter.cs.
// Differences:
//  - the editor's options are the CodeEditor.Folding property (folding) and properties of the controller
//    (showFoldingControls, foldingHighlight, foldingStrategy, unfoldOnClickAfterEndOfLine, foldingImportsByDefault,
//    foldingMaximumRegions) with upstream's defaults;
//  - the editor keeps its model (setting Text replaces the content), so onModelChanged runs when the controller is
//    created and when folding is turned on or off; the hidden areas are given to each new view model again;
//  - the update delay is a fixed 200 ms (upstream: the language feature debounce, at least 200 ms); the first
//    computation after the model is set up runs when the dispatcher is idle; an action runs a waiting update at once
//    instead of waiting for it;
//  - the folding range provider selector is per controller (upstream: a static of the class);
//  - the chevrons are drawn by a gutter column of the controller (upstream: the decorations' firstLineDecorationClassName
//    with codicons), shown at once on hover (upstream fades them in over 0.5 s); the placeholder after a folded line is
//    the content text of the inline-folded class name; a click on it unfolds the region (upstream: a click on the
//    pseudo element, which is part of the line's text);
//  - the scroll position is not kept when imports are folded by default (StableEditorScrollState is not ported).

using Avalonia;
using Avalonia.Input;
using TKCode.Arc.Contrib.Folding;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Folding;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.ViewModel;
using EditorViewModel = TKCode.Arc.Editor.ViewModel.ViewModel;

namespace TKCode.Arc;

/// <summary>Which ranges the folding uses (upstream's foldingStrategy option).</summary>
internal enum FoldingStrategy
{
    /// <summary>The language's folding range providers, else the indentation.</summary>
    Auto,

    /// <summary>The indentation (and the language's region markers) only.</summary>
    Indentation,
}

/// <summary>Picks the folding range providers to use for a document (upstream's FoldingRangeProviderSelector).</summary>
internal delegate IReadOnlyList<IFoldingRangeProvider>? FoldingRangeProviderSelector(IReadOnlyList<IFoldingRangeProvider> providers, TextModel document);

/// <summary>The folding state to keep with a view state (upstream's FoldingStateMemento).</summary>
internal sealed record FoldingStateMemento
{
    public IReadOnlyList<FoldingLineMemento>? CollapsedRegions { get; init; }

    public int? LineCount { get; init; }

    public string? Provider { get; init; }

    public bool FoldedImports { get; init; }
}

/// <summary>
/// The folding feature of an editor (upstream's FoldingController): computes the folding regions, keeps them as model
/// decorations, hides the lines of collapsed regions, and handles the chevrons and the placeholder.
/// </summary>
internal sealed partial class FoldingController : ICodeEditorContribution
{
    /// <summary>The contribution's id.</summary>
    public const string Id = "editor.contrib.folding";

    /// <summary>The placeholder after the first line of a collapsed region (upstream's ⋯ of folding.css).</summary>
    public const string PlaceholderText = "⋯";

    /// <summary>How editors create the controller (eager: it uses saveViewState / restoreViewState).</summary>
    public static readonly EditorContributionDescription Description = new(Id, editor => new FoldingController(editor), EditorContributionInstantiation.Eager);

    private static readonly TimeSpan s_updateDelay = TimeSpan.FromMilliseconds(200);

    private readonly CodeEditor _editor;
    private readonly FoldingDecorationProvider _foldingDecorationProvider;
    private readonly IContextKey<bool> _foldingEnabled;
    private readonly List<IDisposable> _styleRegistrations = new();
    private readonly List<Action> _localToDispose = new();
    private bool _isEnabled;
    private bool _useFoldingProviders;
    private bool _restoringViewState;
    private bool _currentModelHasFoldedImports;
    private FoldingStrategy _foldingStrategy = FoldingStrategy.Auto;
    private FoldingModel? _foldingModel;
    private HiddenRangeModel? _hiddenRangeModel;
    private IRangeProvider? _rangeProvider;
    private CancellationTokenSource? _foldingRegionCts;
    private Task<FoldingModel?>? _foldingModelPromise;
    private Delayer<FoldingModel?>? _updateScheduler;
    private RunOnceScheduler? _cursorChangedScheduler;
    private (int LineNumber, bool IconClicked)? _mouseDownInfo;
    private bool _disposed;

    private FoldingController(CodeEditor editor)
    {
        _editor = editor;
        FoldingLimitReporter = new RangesLimitReporter(this);
        _isEnabled = editor.Folding;
        _useFoldingProviders = _foldingStrategy != FoldingStrategy.Indentation;
        _foldingDecorationProvider = new FoldingDecorationProvider(editor.Model, editor.EditorId);
        _foldingEnabled = editor.CreateContextKey(CodeEditorContextKeys.FoldingEnabled);
        _foldingEnabled.Set(_isEnabled);

        _styleRegistrations.Add(editor.DecorationStyles.Register(FoldingDecorationClassNames.InlineFolded, new CodeEditorDecorationStyle
        {
            ContentText = PlaceholderText,
            ContentMarginEm = 0.2,
            Foreground = CodeEditorThemeColor.Muted,
        }));
        _styleRegistrations.Add(editor.DecorationStyles.Register(FoldingDecorationClassNames.FoldedBackground, new CodeEditorDecorationStyle
        {
            Background = CodeEditorThemeColor.FoldBackground,
        }));

        editor.PropertyChanged += OnEditorPropertyChanged;
        editor.DidChangeViewModel += OnDidChangeViewModel;
        CreateGutterColumn();
        OnModelChanged();
    }

    /// <summary>The controller of <paramref name="editor"/>, or null (upstream's FoldingController.get).</summary>
    public static FoldingController? Get(CodeEditor editor) => editor.GetContribution<FoldingController>(Id);

    /// <summary>Reports how many regions were computed and whether the limit cut them (upstream's limitReporter).</summary>
    public RangesLimitReporter FoldingLimitReporter { get; }

    /// <summary>Picks the folding range providers of a document; null uses all of them, best first.</summary>
    public FoldingRangeProviderSelector? FoldingRangeSelector { get; set; }

    /// <summary>Whether folding is on (the editor's <see cref="CodeEditor.Folding"/>).</summary>
    public bool IsEnabled => _isEnabled;

    /// <summary>When the chevrons show (upstream's showFoldingControls; default mouseover).</summary>
    public ShowFoldingControls ShowFoldingControls
    {
        get => _foldingDecorationProvider.ShowFoldingControls;
        set
        {
            if (_foldingDecorationProvider.ShowFoldingControls != value)
            {
                _foldingDecorationProvider.ShowFoldingControls = value;
                UpdateGutterColumn();
                TriggerFoldingModelChanged();
            }
        }
    }

    /// <summary>Whether a collapsed region's first line gets a background (upstream's foldingHighlight; default true).</summary>
    public bool FoldingHighlight
    {
        get => _foldingDecorationProvider.ShowFoldingHighlights;
        set
        {
            if (_foldingDecorationProvider.ShowFoldingHighlights != value)
            {
                _foldingDecorationProvider.ShowFoldingHighlights = value;
                TriggerFoldingModelChanged();
            }
        }
    }

    /// <summary>Which ranges are used (upstream's foldingStrategy; default auto).</summary>
    public FoldingStrategy FoldingStrategy
    {
        get => _foldingStrategy;
        set
        {
            if (_foldingStrategy != value)
            {
                _foldingStrategy = value;
                _useFoldingProviders = value != FoldingStrategy.Indentation;
                OnFoldingStrategyChanged();
            }
        }
    }

    /// <summary>Whether a click after the end of a folded line unfolds it (upstream's unfoldOnClickAfterEndOfLine; default false).</summary>
    public bool UnfoldOnClickAfterEndOfLine { get; set; }

    /// <summary>Whether import regions are folded when a document opens (upstream's foldingImportsByDefault; default false).</summary>
    public bool FoldingImportsByDefault { get; set; }

    /// <summary>The most regions computed (upstream's foldingMaximumRegions; default 5000).</summary>
    public int FoldingMaximumRegions
    {
        get => _foldingMaximumRegions;
        set
        {
            if (_foldingMaximumRegions != value)
            {
                _foldingMaximumRegions = value;
                OnModelChanged();
            }
        }
    }

    private int _foldingMaximumRegions = IndentRangeProvider.MaxFoldingRegionsForIndentDefault;

    /// <summary>The hidden lines of the collapsed regions, or null when folding is off.</summary>
    internal HiddenRangeModel? HiddenRangeModel => _hiddenRangeModel;

    /// <summary>The folding model once computed (upstream's getFoldingModel); null when folding is off.</summary>
    public Task<FoldingModel?>? GetFoldingModel() => _foldingModelPromise;

    /// <summary>The folding model, running a waiting update first (the actions do not wait for the update delay).</summary>
    internal Task<FoldingModel?>? GetFoldingModelNow()
    {
        _updateScheduler?.Flush();
        return _foldingModelPromise;
    }

    /// <summary>The folding model as it is now, without waiting for an update (tests, the gutter).</summary>
    internal FoldingModel? CurrentFoldingModel => _foldingModel;

    /// <summary>Saves the view state (upstream's saveViewState).</summary>
    public object? SaveViewState()
    {
        var model = _editor.Model;
        if (!_isEnabled || model.IsTooLargeForTokenization())
        {
            return new FoldingStateMemento();
        }

        if (_foldingModel is not null)
        {
            var collapsedRegions = _foldingModel.GetMemento();
            return new FoldingStateMemento
            {
                CollapsedRegions = collapsedRegions,
                LineCount = model.GetLineCount(),
                Provider = _rangeProvider?.Id,
                FoldedImports = _currentModelHasFoldedImports,
            };
        }

        return null;
    }

    /// <summary>Restores the view state (upstream's restoreViewState).</summary>
    public void RestoreViewState(object? state)
    {
        var model = _editor.Model;
        if (!_isEnabled || model.IsTooLargeForTokenization() || _hiddenRangeModel is null)
        {
            return;
        }

        if (state is not FoldingStateMemento memento)
        {
            return;
        }

        _currentModelHasFoldedImports = memento.FoldedImports;
        if (memento.CollapsedRegions is { Count: > 0 } collapsedRegions && _foldingModel is not null)
        {
            _restoringViewState = true;
            try
            {
                _foldingModel.ApplyMemento(collapsedRegions);
            }
            finally
            {
                _restoringViewState = false;
            }
        }
    }

    private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == CodeEditor.FoldingProperty)
        {
            _isEnabled = _editor.Folding;
            _foldingEnabled.Set(_isEnabled);
            UpdateGutterColumn();
            OnModelChanged();
        }
    }

    private void OnModelChanged()
    {
        ClearLocal();

        var model = _editor.Model;
        if (_disposed || !_isEnabled || model.IsTooLargeForTokenization())
        {
            // huge files get no view model, so they cannot support hidden areas
            return;
        }

        _currentModelHasFoldedImports = false;
        var foldingModel = new FoldingModel(model, _foldingDecorationProvider);
        _foldingModel = foldingModel;
        _localToDispose.Add(foldingModel.Dispose);

        // the hidden range model is disposed while its listener is still there, so that the hidden areas go away
        var hiddenRangeModel = new HiddenRangeModel(foldingModel);
        _hiddenRangeModel = hiddenRangeModel;
        hiddenRangeModel.DidChange += OnHiddenRangesChanges;
        _localToDispose.Add(() =>
        {
            hiddenRangeModel.Dispose();
            hiddenRangeModel.DidChange -= OnHiddenRangesChanges;
        });

        var updateScheduler = new Delayer<FoldingModel?>(s_updateDelay);
        _updateScheduler = updateScheduler;
        _localToDispose.Add(updateScheduler.Dispose);

        var cursorChangedScheduler = new RunOnceScheduler(RevealCursor, TimeSpan.FromMilliseconds(200));
        _cursorChangedScheduler = cursorChangedScheduler;
        _localToDispose.Add(cursorChangedScheduler.Dispose);

        Action<int> onProvidersChanged = _ => OnFoldingStrategyChanged();
        _editor.LanguageFeatures.FoldingRangeProvider.DidChange += onProvidersChanged;
        _localToDispose.Add(() => _editor.LanguageFeatures.FoldingRangeProvider.DidChange -= onProvidersChanged);

        // covers model language changes as well (upstream's onDidChangeModelLanguageConfiguration)
        Action<LanguageConfigurationServiceChangeEvent> onConfigurationChanged = e =>
        {
            if (e.Affects(_editor.Model.GetLanguageId()))
            {
                OnFoldingStrategyChanged();
            }
        };
        _editor.LanguageConfigurationService.DidChange += onConfigurationChanged;
        _localToDispose.Add(() => _editor.LanguageConfigurationService.DidChange -= onConfigurationChanged);
        Action<ModelLanguageChangedEvent> onLanguageChanged = _ => OnFoldingStrategyChanged();
        _editor.DidChangeModelLanguage += onLanguageChanged;
        _localToDispose.Add(() => _editor.DidChangeModelLanguage -= onLanguageChanged);

        _editor.DidChangeModelContent += OnDidChangeModelContent;
        _localToDispose.Add(() => _editor.DidChangeModelContent -= OnDidChangeModelContent);
        _editor.DidChangeCursorSelection += OnCursorPositionChanged;
        _localToDispose.Add(() => _editor.DidChangeCursorSelection -= OnCursorPositionChanged);
        _editor.MouseDown += OnEditorMouseDown;
        _localToDispose.Add(() => _editor.MouseDown -= OnEditorMouseDown);
        _editor.MouseUp += OnEditorMouseUp;
        _localToDispose.Add(() => _editor.MouseUp -= OnEditorMouseUp);
        _editor.MouseMove += OnEditorMouseMove;
        _localToDispose.Add(() => _editor.MouseMove -= OnEditorMouseMove);
        _editor.MouseLeave += OnEditorMouseLeave;
        _localToDispose.Add(() => _editor.MouseLeave -= OnEditorMouseLeave);
        _editor.DidChangeModelDecorations += OnDidChangeModelDecorations;
        _localToDispose.Add(() => _editor.DidChangeModelDecorations -= OnDidChangeModelDecorations);
        _localToDispose.Add(() =>
        {
            _foldingRegionCts?.Cancel();
            _foldingRegionCts = null;
            _updateScheduler?.Cancel();
            _updateScheduler = null;
            _foldingModel = null;
            _foldingModelPromise = null;
            _hiddenRangeModel = null;
            _cursorChangedScheduler = null;
            _rangeProvider?.Dispose();
            _rangeProvider = null;
        });

        // the first computation does not wait for the update delay
        TriggerFoldingModelChanged(TimeSpan.Zero);
    }

    private void ClearLocal()
    {
        var actions = _localToDispose.ToArray();
        _localToDispose.Clear();
        foreach (var action in actions)
        {
            action();
        }
    }

    private void OnFoldingStrategyChanged()
    {
        _rangeProvider?.Dispose();
        _rangeProvider = null;
        TriggerFoldingModelChanged();
    }

    private IRangeProvider GetRangeProvider(TextModel editorModel)
    {
        if (_rangeProvider is not null)
        {
            return _rangeProvider;
        }

        var indentRangeProvider = new IndentRangeProvider(editorModel, _editor.LanguageConfigurationService, FoldingLimitReporter);
        _rangeProvider = indentRangeProvider; // fallback
        if (_useFoldingProviders && _foldingModel is not null)
        {
            var selectedProviders = GetFoldingRangeProviders(editorModel);
            if (selectedProviders.Count > 0)
            {
                _rangeProvider = new SyntaxRangeProvider(editorModel, selectedProviders, () => TriggerFoldingModelChanged(), FoldingLimitReporter, indentRangeProvider);
            }
        }

        return _rangeProvider;
    }

    /// <summary>The folding range providers of a document, after the selector (upstream's getFoldingRangeProviders).</summary>
    internal IReadOnlyList<IFoldingRangeProvider> GetFoldingRangeProviders(TextModel model)
    {
        var foldingRangeProviders = _editor.LanguageFeatures.FoldingRangeProvider.Ordered(model);
        return FoldingRangeSelector?.Invoke(foldingRangeProviders, model) ?? foldingRangeProviders;
    }

    private void OnDidChangeModelContent(ModelContentChangedEvent e)
    {
        _hiddenRangeModel?.NotifyChangeModelContent(e);
        TriggerFoldingModelChanged();
    }

    /// <summary>Computes the regions again after the update delay (upstream's triggerFoldingModelChanged).</summary>
    public void TriggerFoldingModelChanged() => TriggerFoldingModelChanged(null);

    private void TriggerFoldingModelChanged(TimeSpan? delay)
    {
        if (_updateScheduler is not { } updateScheduler)
        {
            return;
        }

        _foldingRegionCts?.Cancel();
        _foldingRegionCts = null;
        _foldingModelPromise = CatchErrors(updateScheduler.Trigger(UpdateFoldingModel, delay));
    }

    private async Task<FoldingModel?> UpdateFoldingModel()
    {
        var foldingModel = _foldingModel;
        if (foldingModel is null)
        {
            // null if the editor has been disposed, or folding turned off
            return null;
        }

        var provider = GetRangeProvider(foldingModel.TextModel);
        var cts = new CancellationTokenSource();
        _foldingRegionCts = cts;
        var foldingRanges = await provider.Compute(cts.Token).ConfigureAwait(true);
        if (foldingRanges is not null && ReferenceEquals(cts, _foldingRegionCts) && !cts.IsCancellationRequested && foldingModel == _foldingModel)
        {
            // new request or cancelled in the meantime?
            if (FoldingImportsByDefault && !_currentModelHasFoldedImports)
            {
                var hasChanges = foldingRanges.SetCollapsedAllOfType(FoldingRangeKind.Imports.Value, true);
                if (hasChanges)
                {
                    _currentModelHasFoldedImports = hasChanges;
                }
            }

            // some cursors might have moved into hidden regions, make sure they are in expanded regions
            var selections = _editor.GetSelections();
            foldingModel.Update(foldingRanges, SelectedLines.ToSelectedLines(selections));
        }

        return foldingModel;
    }

    private static async Task<FoldingModel?> CatchErrors(Task<FoldingModel?> task)
    {
        try
        {
            return await task.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception e)
        {
            UnexpectedErrors.Report(e);
            return null;
        }
    }

    private void OnHiddenRangesChanges(IReadOnlyList<TextRange> hiddenRanges)
    {
        if (_hiddenRangeModel is { } hiddenRangeModel && hiddenRanges.Count > 0 && !_restoringViewState)
        {
            var selections = _editor.GetSelections().ToList();
            if (selections.Count > 0 && hiddenRangeModel.AdjustSelections(selections))
            {
                _editor.SetSelections(selections);
            }
        }

        _editor.ViewModel?.SetHiddenAreas(hiddenRanges, this);
        InvalidateGutter();
    }

    // a new view model (font, size, template) starts without hidden areas: give it ours
    private void OnDidChangeViewModel(EditorViewModel viewModel)
    {
        if (_hiddenRangeModel is { } hiddenRangeModel && hiddenRangeModel.HasRanges())
        {
            viewModel.SetHiddenAreas(hiddenRangeModel.HiddenRanges, this);
        }

        UpdateGutterColumn();
    }

    private void OnCursorPositionChanged(CursorStateChangedEvent e)
    {
        if (_hiddenRangeModel is { } hiddenRangeModel && hiddenRangeModel.HasRanges())
        {
            _cursorChangedScheduler!.Schedule();
        }
    }

    /// <summary>Runs a waiting reveal of the cursor now (tests).</summary>
    internal void FlushCursorChanged() => _cursorChangedScheduler?.Flush();

    private async void RevealCursor()
    {
        if (GetFoldingModel() is not { } foldingModelPromise)
        {
            return;
        }

        try
        {
            // null is returned if folding got disabled in the meantime
            if (await foldingModelPromise.ConfigureAwait(true) is not { } foldingModel)
            {
                return;
            }

            var selections = _editor.GetSelections();
            if (selections.Count > 0)
            {
                var toToggle = new List<FoldingRegion>();
                foreach (var selection in selections)
                {
                    var lineNumber = selection.SelectionStartLineNumber;
                    if (_hiddenRangeModel is { } hiddenRangeModel && hiddenRangeModel.IsHidden(lineNumber))
                    {
                        toToggle.AddRange(foldingModel.GetAllRegionsAtLine(lineNumber, (r, _) => r.IsCollapsed && lineNumber > r.StartLineNumber));
                    }
                }

                if (toToggle.Count > 0)
                {
                    foldingModel.ToggleCollapseState(toToggle);
                    Reveal(selections[0].Position);
                }
            }
        }
        catch (Exception e)
        {
            UnexpectedErrors.Report(e);
        }
    }

    // ---- the pointer

    private void OnEditorMouseDown(CodeEditorMouseEvent e)
    {
        _mouseDownInfo = null;
        if (_hiddenRangeModel is not { } hiddenRangeModel || e.Target.Range is not { } range)
        {
            return;
        }

        var properties = e.Event.GetCurrentPoint(null).Properties;
        if (!properties.IsLeftButtonPressed && !properties.IsMiddleButtonPressed)
        {
            return;
        }

        var iconClicked = false;
        switch (e.Target.Type)
        {
            case CodeEditorMouseTargetType.GutterLineDecorations:
                if (e.Target.Detail != FoldingColumnId)
                {
                    return;
                }

                iconClicked = true;
                break;
            case CodeEditorMouseTargetType.ContentEmpty:
                if (IsOnPlaceholder(e.Target))
                {
                    break;
                }

                if (UnfoldOnClickAfterEndOfLine && hiddenRangeModel.HasRanges())
                {
                    if (!IsAfterLines(e.Target))
                    {
                        break;
                    }
                }

                return;
            case CodeEditorMouseTargetType.ContentText:
                if (hiddenRangeModel.HasRanges())
                {
                    var model = _editor.Model;
                    if (range.StartColumn == model.GetLineMaxColumn(range.StartLineNumber))
                    {
                        break;
                    }
                }

                return;
            default:
                return;
        }

        _mouseDownInfo = (range.StartLineNumber, iconClicked);
    }

    private void OnEditorMouseUp(CodeEditorMouseEvent e)
    {
        var foldingModel = _foldingModel;
        if (foldingModel is null || _mouseDownInfo is not { } mouseDownInfo)
        {
            return;
        }

        _mouseDownInfo = null;
        var lineNumber = mouseDownInfo.LineNumber;
        var iconClicked = mouseDownInfo.IconClicked;

        if (e.Target.Range is not { } range || range.StartLineNumber != lineNumber)
        {
            return;
        }

        if (iconClicked)
        {
            if (e.Target.Type != CodeEditorMouseTargetType.GutterLineDecorations)
            {
                return;
            }
        }
        else
        {
            if (range.StartColumn != _editor.Model.GetLineMaxColumn(lineNumber))
            {
                return;
            }
        }

        var region = foldingModel.GetRegionAtLine(lineNumber);
        if (region is not null && region.StartLineNumber == lineNumber)
        {
            var isCollapsed = region.IsCollapsed;
            if (iconClicked || isCollapsed)
            {
                var surrounding = e.Event.KeyModifiers.HasFlag(KeyModifiers.Alt);
                var toToggle = new List<FoldingRegion>();
                if (surrounding)
                {
                    var toMaybeToggle = foldingModel.GetRegionsInside(null, otherRegion => !otherRegion.ContainedBy(region) && !region.ContainedBy(otherRegion));
                    foreach (var r in toMaybeToggle)
                    {
                        if (r.IsCollapsed)
                        {
                            toToggle.Add(r);
                        }
                    }

                    // if any surrounding regions are folded, unfold those. Otherwise, fold all surrounding
                    if (toToggle.Count == 0)
                    {
                        toToggle = toMaybeToggle;
                    }
                }
                else
                {
                    var middleButton = e.Event is PointerReleasedEventArgs { InitialPressMouseButton: MouseButton.Middle };
                    var recursive = middleButton || e.Event.KeyModifiers.HasFlag(KeyModifiers.Shift);
                    if (recursive)
                    {
                        foreach (var r in foldingModel.GetRegionsInside(region))
                        {
                            if (r.IsCollapsed == isCollapsed)
                            {
                                toToggle.Add(r);
                            }
                        }
                    }

                    // when recursive, first only collapse all children. If all are already folded or there are no children, also fold parent.
                    if (isCollapsed || !recursive || toToggle.Count == 0)
                    {
                        toToggle.Add(region);
                    }
                }

                foldingModel.ToggleCollapseState(toToggle);
                Reveal(new TextPosition(lineNumber, 1));
            }
        }
    }

    // upstream's target detail isAfterLines: below the last line
    private bool IsAfterLines(CodeEditorMouseTarget target) =>
        _editor.View is { } view && _editor.ViewModel is { } viewModel && viewModel.ViewLayout.IsAfterLines(target.Point.Y + view.ScrollTop);

    /// <summary>Whether a point is on the placeholder after a folded line.</summary>
    private bool IsOnPlaceholder(CodeEditorMouseTarget target)
    {
        if (_editor.View is not { } view || _editor.ViewModel is not { } viewModel || target.Position is not { } position)
        {
            return false;
        }

        var viewLine = viewModel.CoordinatesConverter.ConvertModelPositionToViewPosition(position).LineNumber;
        foreach (var rect in view.GetContentDecorationRects(viewLine, viewLine))
        {
            if (rect.ClassName == FoldingDecorationClassNames.InlineFolded && rect.Bounds.Contains(target.Point))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Scrolls a position into view, centered if it is outside (upstream's reveal).</summary>
    public void Reveal(TextPosition position) =>
        _editor.RevealRange(new TextRange(position.LineNumber, position.Column, position.LineNumber, position.Column), VerticalRevealType.CenterIfOutsideViewport, ScrollType.Smooth);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _editor.PropertyChanged -= OnEditorPropertyChanged;
        _editor.DidChangeViewModel -= OnDidChangeViewModel;
        ClearLocal();
        _foldingEnabled.Reset();
        RemoveGutterColumn();
        foreach (var registration in _styleRegistrations)
        {
            registration.Dispose();
        }

        _styleRegistrations.Clear();
    }

    /// <summary>The limit of regions and what the last computation reported (upstream's RangesLimitReporter).</summary>
    internal sealed class RangesLimitReporter(FoldingController controller) : IFoldingLimitReporter
    {
        public int Limit => controller.FoldingMaximumRegions;

        /// <summary>Raised when <see cref="Computed"/> or <see cref="Limited"/> changed.</summary>
        public event Action? DidChange;

        /// <summary>How many regions the last computation found.</summary>
        public int Computed { get; private set; }

        /// <summary>The limit that cut them, or null when they were not cut.</summary>
        public int? Limited { get; private set; }

        public void Update(int computed, int? limited)
        {
            if (computed != Computed || limited != Limited)
            {
                Computed = computed;
                Limited = limited;
                DidChange?.Invoke();
            }
        }
    }
}
