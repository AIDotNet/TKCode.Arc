// Ported from vscode (src/vs/editor/common/viewModel/viewModelImpl.ts: ViewModel, commit b57d5c9e), MIT license.
// This part: construction, disposal, configuration changes and the reaction to model changes. The queries (lines,
// visible ranges, hidden areas, decorations, rendering data, clipboard) are in ViewModel.Queries.cs, the cursor and
// view layout operations in ViewModel.Cursor.cs, and the helper classes in ViewModel.Helpers.cs.
// Differences from upstream:
//  - the class keeps upstream's name, which is also the name of its namespace: code outside this namespace names it
//    TKCode.Arc.Editor.ViewModel.ViewModel (or through a using alias);
//  - the configuration is a ViewModelConfiguration (ViewModelConfiguration.cs) instead of an IEditorConfiguration; the
//    options are read from its Options record where upstream calls options.get(EditorOption.x), and getEditorOption is
//    the Configuration property; the cursor configuration is recreated when CursorConfiguration.ShouldRecreate says so
//    for the old and the new cursor options, or when the input mode changed;
//  - no dependency injection: the language configuration service is a constructor parameter; there is no theme service
//    (OnDidColorThemeChange is what upstream's onDidColorThemeChange listener does, for the host to call) and no
//    MinimapTokensColorTracker (ViewTokensColorsChangedEvent is not raised);
//  - onDidChangeLanguageConfiguration is the language configuration service's DidChange for the model's language, which
//    is what upstream's tokenization part forwards;
//  - _updateConfigurationViewLineCount, upstream's RunOnceScheduler with a delay of 0, updates the configuration's view
//    line count at once;
//  - glyphLanes (IGlyphMarginLanesModel) is not ported;
//  - the attached view and the batchable target may be null (no view: nothing to tell; no target: the callback runs as
//    it is), and scheduleAtNextAnimationFrame is ViewLayout's Func<Action, IDisposable>;
//  - onEvent is the .NET event OutgoingEvent (the dispatcher's).

using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewLayout;
using EditorViewLayout = TKCode.Arc.Editor.ViewLayout.ViewLayout;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>
/// The view model of an editor: the model's lines as the view shows them (wrapped, folded, with injected text), the
/// cursors, the decorations and the view layout, with the events the view and the editor listen to.
/// </summary>
internal partial class ViewModel : IViewModel, IDisposable
{
    private const bool UseIdentityLinesCollection = true;

    private readonly int _editorId;
    private readonly ViewModelConfiguration _configuration;
    private readonly ViewModelEventDispatcher _eventDispatcher;
    private readonly LanguageConfigurationService _languageConfigurationService;
    private readonly IAttachedView? _attachedView;
    private readonly IBatchableTarget? _transactionalTarget;
    private bool _hasFocus;
    private readonly ViewportStart _viewportStart;
    private readonly IViewModelLines _lines;
    private readonly CursorsController _cursor;
    private readonly ViewModelDecorations _decorations;
    private readonly List<Action> _toDispose = new();
    private bool _isDisposed;

    // What the current cursor configuration was built from (upstream's CursorConfiguration.shouldRecreate reads the
    // change event; here it compares the options).
    private EditorOptions _cursorOptions;
    private EditorInputMode _inputMode;

    private readonly HiddenAreasModel _hiddenAreasModel = new();
    private IReadOnlyList<TextRange> _previousHiddenAreas = [];

    public ViewModel(
        int editorId,
        ViewModelConfiguration configuration,
        TextModel model,
        ILineBreaksComputerFactory domLineBreaksComputerFactory,
        ILineBreaksComputerFactory monospaceLineBreaksComputerFactory,
        Func<Action, IDisposable> scheduleAtNextAnimationFrame,
        LanguageConfigurationService languageConfigurationService,
        IAttachedView? attachedView = null,
        IBatchableTarget? transactionalTarget = null)
    {
        _editorId = editorId;
        _configuration = configuration;
        Model = model;
        _languageConfigurationService = languageConfigurationService;
        _attachedView = attachedView;
        _transactionalTarget = transactionalTarget;
        _eventDispatcher = new ViewModelEventDispatcher();
        _cursorOptions = _configuration.Options.Cursor;
        _inputMode = _configuration.Options.InputMode;
        CursorConfig = CreateCursorConfiguration();
        _hasFocus = false;
        _viewportStart = ViewportStart.Create(Model);

        if (UseIdentityLinesCollection && Model.IsTooLargeForTokenization())
        {
            _lines = new ViewModelLinesFromModelAsIs(Model);
        }
        else
        {
            var options = _configuration.Options;

            _lines = new ViewModelLinesFromProjectedModel(
                _editorId,
                Model,
                domLineBreaksComputerFactory,
                monospaceLineBreaksComputerFactory,
                options.FontInfo,
                Model.GetOptions().TabSize,
                options.WrappingStrategy,
                options.WrappingColumn,
                options.WrappingIndent,
                options.WordBreak,
                options.WrapOnEscapedLineFeeds,
                options.UseTwoCellFullwidthCharacters,
                Model.GetGuides(_languageConfigurationService));
        }

        CoordinatesConverter = _lines.CreateCoordinatesConverter();

        _cursor = new CursorsController(model, this, CoordinatesConverter, CursorConfig);

        ViewLayout = new EditorViewLayout(_configuration, GetLineCount(), GetCustomLineHeights(), scheduleAtNextAnimationFrame);

        Action<ScrollEvent> onDidScroll = e =>
        {
            if (e.ScrollTopChanged)
            {
                HandleVisibleLinesChanged();
            }
            if (e.ScrollTopChanged)
            {
                _viewportStart.Invalidate();
            }
            _eventDispatcher.EmitSingleViewEvent(new ViewScrollChangedEvent(e));
            _eventDispatcher.EmitOutgoingEvent(new ScrollChangedEvent(
                e.OldScrollWidth, e.OldScrollLeft, e.OldScrollHeight, e.OldScrollTop,
                e.ScrollWidth, e.ScrollLeft, e.ScrollHeight, e.ScrollTop));
        };
        ViewLayout.DidScroll += onDidScroll;
        _toDispose.Add(() => ViewLayout.DidScroll -= onDidScroll);

        Action<ContentSizeChangedEvent> onDidContentSizeChange = e => _eventDispatcher.EmitOutgoingEvent(e);
        ViewLayout.DidContentSizeChange += onDidContentSizeChange;
        _toDispose.Add(() => ViewLayout.DidContentSizeChange -= onDidContentSizeChange);

        _decorations = new ViewModelDecorations(
            _editorId,
            Model,
            _configuration.FilterValidationDecorations,
            _configuration.FilterFontDecorations,
            _lines,
            CoordinatesConverter);

        RegisterModelEvents();

        Action<ConfigurationChangedEvent> onDidChangeFast = e =>
        {
            try
            {
                var eventsCollector = _eventDispatcher.BeginEmitViewEvents();
                OnConfigurationChanged(eventsCollector, e);
            }
            finally
            {
                _eventDispatcher.EndEmitViewEvents();
            }
        };
        _configuration.DidChangeFast += onDidChangeFast;
        _toDispose.Add(() => _configuration.DidChangeFast -= onDidChangeFast);

        UpdateConfigurationViewLineCountNow();
        Model.RegisterViewModel(this);
    }

    /// <summary>The text model this view model shows.</summary>
    public TextModel Model { get; }

    /// <summary>The editor configuration (upstream's getEditorOption reads from it).</summary>
    public ViewModelConfiguration Configuration => _configuration;

    /// <summary>The cursor configuration (options and language rules) for the model's language.</summary>
    public CursorConfiguration CursorConfig { get; private set; }

    /// <summary>Converts between model and view coordinates.</summary>
    public ICoordinatesConverter CoordinatesConverter { get; }

    /// <summary>The vertical layout of the view lines and the scroll state over it.</summary>
    public EditorViewLayout ViewLayout { get; }

    IViewLayout IViewModel.ViewLayout => ViewLayout;

    /// <summary>An outgoing event (upstream's onEvent).</summary>
    public event Action<OutgoingViewModelEvent>? OutgoingEvent
    {
        add => _eventDispatcher.OutgoingEvent += value;
        remove => _eventDispatcher.OutgoingEvent -= value;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;

        // First remove listeners, as disposing the lines might end up sending
        // model decoration changed events ... and we no longer care about them ...
        foreach (var dispose in _toDispose)
        {
            dispose();
        }
        _toDispose.Clear();
        _cursor.Dispose();
        ViewLayout.Dispose();
        _decorations.Dispose();
        _lines.Dispose();
        _viewportStart.Dispose();
        _eventDispatcher.Dispose();
        Model.UnregisterViewModel(this);
    }

    public ILineBreaksComputer CreateLineBreaksComputer(ILineBreaksComputerContext? context = null)
    {
        return _lines.CreateLineBreaksComputer(context);
    }

    public void AddViewEventHandler(ViewEventHandler eventHandler)
    {
        _eventDispatcher.AddViewEventHandler(eventHandler);
    }

    public void RemoveViewEventHandler(ViewEventHandler eventHandler)
    {
        _eventDispatcher.RemoveViewEventHandler(eventHandler);
    }

    /// <summary>The theme changed (upstream's onDidColorThemeChange listener); <paramref name="theme"/> is opaque.</summary>
    public void OnDidColorThemeChange(object theme)
    {
        InvalidateDecorationsColorCache();
        _eventDispatcher.EmitSingleViewEvent(new ViewThemeChangedEvent(theme));
    }

    private CursorConfiguration CreateCursorConfiguration() =>
        new(Model.GetLanguageId(), Model.GetOptions(), _cursorOptions, _languageConfigurationService, _inputMode);

    private void RecreateCursorConfiguration()
    {
        _cursorOptions = _configuration.Options.Cursor;
        _inputMode = _configuration.Options.InputMode;
        CursorConfig = CreateCursorConfiguration();
        _cursor.UpdateConfiguration(CursorConfig);
    }

    private IReadOnlyList<CustomLineHeightData> GetCustomLineHeights()
    {
        var allowVariableLineHeights = _configuration.Options.AllowVariableLineHeights;
        if (!allowVariableLineHeights)
        {
            return [];
        }
        var decorations = Model.GetCustomLineHeightsDecorations(_editorId);
        return CustomLineHeightData.FromDecorations(decorations, r => CoordinatesConverter.ConvertModelRangeToViewRange(r), _configuration.Options.Layout.LineHeight);
    }

    private IReadOnlyList<CustomLineHeightData> GetCustomLineHeightsForLines(int fromLineNumber, int toLineNumber)
    {
        var allowVariableLineHeights = _configuration.Options.AllowVariableLineHeights;
        if (!allowVariableLineHeights)
        {
            return [];
        }
        var modelRange = new TextRange(fromLineNumber, 1, toLineNumber, Model.GetLineMaxColumn(toLineNumber));
        var decorations = Model.GetCustomLineHeightsDecorationsInRange(modelRange, _editorId);
        return CustomLineHeightData.FromDecorations(decorations, r => CoordinatesConverter.ConvertModelRangeToViewRange(r), _configuration.Options.Layout.LineHeight);
    }

    private void UpdateConfigurationViewLineCountNow()
    {
        _configuration.SetViewLineCount(_lines.GetViewLineCount());
    }

    private List<TextRange> GetModelVisibleRanges()
    {
        var linesViewportData = ViewLayout.GetLinesViewportData();
        var viewVisibleRange = new TextRange(
            linesViewportData.StartLineNumber,
            GetLineMinColumn(linesViewportData.StartLineNumber),
            linesViewportData.EndLineNumber,
            GetLineMaxColumn(linesViewportData.EndLineNumber));
        var modelVisibleRanges = ToModelVisibleRanges(viewVisibleRange);
        return modelVisibleRanges;
    }

    public void VisibleLinesStabilized()
    {
        var modelVisibleRanges = GetModelVisibleRanges();
        _attachedView?.SetVisibleLines(modelVisibleRanges, true);
    }

    private void HandleVisibleLinesChanged()
    {
        var modelVisibleRanges = GetModelVisibleRanges();
        _attachedView?.SetVisibleLines(modelVisibleRanges, false);
    }

    public void SetHasFocus(bool hasFocus)
    {
        _hasFocus = hasFocus;
        _cursor.SetHasFocus(hasFocus);
        _eventDispatcher.EmitSingleViewEvent(new ViewFocusChangedEvent(hasFocus));
        _eventDispatcher.EmitOutgoingEvent(new FocusChangedEvent(!hasFocus, hasFocus));
    }

    public void SetHasWidgetFocus(bool hasWidgetFocus)
    {
        _eventDispatcher.EmitOutgoingEvent(new WidgetFocusChangedEvent(!hasWidgetFocus, hasWidgetFocus));
    }

    public void OnCompositionStart()
    {
        _eventDispatcher.EmitSingleViewEvent(new ViewCompositionStartEvent());
    }

    public void OnCompositionEnd()
    {
        _eventDispatcher.EmitSingleViewEvent(new ViewCompositionEndEvent());
    }

    private StableViewport CaptureStableViewport()
    {
        // We might need to restore the current start view range, so save it (if available)
        // But only if the scroll position is not at the top of the file
        if (_viewportStart.IsValid && ViewLayout.GetCurrentScrollTop() > 0)
        {
            var previousViewportStartViewPosition = new TextPosition(_viewportStart.ViewLineNumber, GetLineMinColumn(_viewportStart.ViewLineNumber));
            var previousViewportStartModelPosition = CoordinatesConverter.ConvertViewPositionToModelPosition(previousViewportStartViewPosition);
            return new StableViewport(previousViewportStartModelPosition, _viewportStart.StartLineDelta);
        }
        return new StableViewport(null, 0);
    }

    private void OnConfigurationChanged(ViewModelEventsCollector eventsCollector, ConfigurationChangedEvent e)
    {
        var stableViewport = CaptureStableViewport();
        var options = _configuration.Options;

        if (_lines.SetWrappingSettings(options.FontInfo, options.WrappingStrategy, options.WrappingColumn, options.WrappingIndent, options.WordBreak, options.UseTwoCellFullwidthCharacters))
        {
            eventsCollector.EmitViewEvent(new ViewFlushedEvent());
            eventsCollector.EmitViewEvent(new ViewLineMappingChangedEvent());
            eventsCollector.EmitViewEvent(new ViewDecorationsChangedEvent(null));
            _cursor.OnLineMappingChanged(eventsCollector);
            _decorations.OnLineMappingChanged();
            ViewLayout.OnFlushed(GetLineCount(), GetCustomLineHeights());

            UpdateConfigurationViewLineCountNow();
        }

        if (e.HasChanged(EditorOptionNames.ReadOnly))
        {
            // Must read again all decorations due to readOnly filtering
            _decorations.Reset();
            eventsCollector.EmitViewEvent(new ViewDecorationsChangedEvent(null));
        }

        if (e.HasChanged(EditorOptionNames.RenderValidationDecorations))
        {
            _decorations.Reset();
            eventsCollector.EmitViewEvent(new ViewDecorationsChangedEvent(null));
        }

        eventsCollector.EmitViewEvent(new ViewConfigurationChangedEvent(e));
        ViewLayout.OnConfigurationChanged(e);

        stableViewport.RecoverViewportStart(CoordinatesConverter, ViewLayout);

        if (CursorConfiguration.ShouldRecreate(_cursorOptions, options.Cursor) || _inputMode != options.InputMode)
        {
            RecreateCursorConfiguration();
        }
    }

    /// <summary>Gets called directly by the text model.</summary>
    public void OnDidChangeContentOrInjectedText(IModelContentOrInjectedTextChangedEvent e)
    {
        try
        {
            var eventsCollector = _eventDispatcher.BeginEmitViewEvents();

            var hadOtherModelChange = false;
            var hadModelLineChangeThatChangedLineMapping = false;

            IReadOnlyList<ModelRawChange> changes;
            int? versionId;
            if (e is InternalModelContentChangeEvent contentChange)
            {
                changes = contentChange.RawContentChangedEvent.Changes;
                versionId = contentChange.RawContentChangedEvent.VersionId;
            }
            else
            {
                changes = ((ModelInjectedTextChangedEvent)e).Changes;
                versionId = null;
            }

            // Do a first pass to compute line mappings, and a second pass to actually interpret them
            var lineBreaksComputer = _lines.CreateLineBreaksComputer();
            foreach (var change in changes)
            {
                switch (change)
                {
                    case ModelRawLinesInserted linesInserted:
                    {
                        for (var i = 0; i < linesInserted.Count; i++)
                        {
                            lineBreaksComputer.AddRequest(linesInserted.FromLineNumberPostEdit + i, null);
                        }
                        break;
                    }
                    case ModelRawLineChanged lineChanged:
                    {
                        lineBreaksComputer.AddRequest(lineChanged.LineNumberPostEdit, null);
                        break;
                    }
                }
            }
            var lineBreaks = lineBreaksComputer.Finalize();
            var lineBreakQueue = 0;

            // Collect model line ranges that need custom line height computation.
            // We defer this until after the loop because the coordinatesConverter
            // relies on projections that may not yet reflect all changes in the batch.
            var customLineHeightRangesToInsert = new List<(int FromLineNumber, int ToLineNumber)>();

            foreach (var change in changes)
            {
                switch (change)
                {
                    case ModelRawFlush:
                    {
                        _lines.OnModelFlushed();
                        eventsCollector.EmitViewEvent(new ViewFlushedEvent());
                        _decorations.Reset();
                        ViewLayout.OnFlushed(GetLineCount(), GetCustomLineHeights());
                        hadOtherModelChange = true;
                        break;
                    }
                    case ModelRawLinesDeleted linesDeleted:
                    {
                        var linesDeletedEvent = _lines.OnModelLinesDeleted(versionId, linesDeleted.FromLineNumber, linesDeleted.ToLineNumber);
                        if (linesDeletedEvent is not null)
                        {
                            eventsCollector.EmitViewEvent(linesDeletedEvent);
                            ViewLayout.OnLinesDeleted(linesDeletedEvent.FromLineNumber, linesDeletedEvent.ToLineNumber);
                            customLineHeightRangesToInsert.Add((linesDeleted.LastUntouchedLinePostEdit, linesDeleted.LastUntouchedLinePostEdit));
                        }
                        hadOtherModelChange = true;
                        break;
                    }
                    case ModelRawLinesInserted linesInserted:
                    {
                        var insertedLineBreaks = new ModelLineProjectionData?[linesInserted.Count];
                        Array.Copy(lineBreaks, lineBreakQueue, insertedLineBreaks, 0, linesInserted.Count);
                        lineBreakQueue += linesInserted.Count;
                        var linesInsertedEvent = _lines.OnModelLinesInserted(versionId, linesInserted.FromLineNumber, linesInserted.ToLineNumber, insertedLineBreaks);
                        if (linesInsertedEvent is not null)
                        {
                            eventsCollector.EmitViewEvent(linesInsertedEvent);
                            ViewLayout.OnLinesInserted(linesInsertedEvent.FromLineNumber, linesInsertedEvent.ToLineNumber);
                            customLineHeightRangesToInsert.Add((linesInserted.FromLineNumberPostEdit, linesInserted.ToLineNumberPostEdit));
                        }
                        hadOtherModelChange = true;
                        break;
                    }
                    case ModelRawLineChanged lineChanged:
                    {
                        var changedLineBreakData = lineBreaks[lineBreakQueue++];
                        var result = _lines.OnModelLineChanged(versionId, lineChanged.LineNumber, changedLineBreakData);
                        hadModelLineChangeThatChangedLineMapping = result.LineMappingChanged;
                        if (result.LinesChangedEvent is { } linesChangedEvent)
                        {
                            eventsCollector.EmitViewEvent(linesChangedEvent);
                        }
                        if (result.LinesInsertedEvent is { } linesInsertedEvent)
                        {
                            eventsCollector.EmitViewEvent(linesInsertedEvent);
                            ViewLayout.OnLinesInserted(linesInsertedEvent.FromLineNumber, linesInsertedEvent.ToLineNumber);
                            customLineHeightRangesToInsert.Add((lineChanged.LineNumberPostEdit, lineChanged.LineNumberPostEdit));
                        }
                        if (result.LinesDeletedEvent is { } linesDeletedEvent)
                        {
                            eventsCollector.EmitViewEvent(linesDeletedEvent);
                            ViewLayout.OnLinesDeleted(linesDeletedEvent.FromLineNumber, linesDeletedEvent.ToLineNumber);
                            customLineHeightRangesToInsert.Add((lineChanged.LineNumberPostEdit, lineChanged.LineNumberPostEdit));
                        }
                        break;
                    }
                    case ModelRawEOLChanged:
                    {
                        // Nothing to do. The new version will be accepted below
                        break;
                    }
                }
            }

            if (versionId is { } acceptedVersionId)
            {
                _lines.AcceptVersionId(acceptedVersionId);
            }

            // Apply deferred custom line heights now that projections are stable
            if (customLineHeightRangesToInsert.Count > 0)
            {
                ViewLayout.ChangeSpecialLineHeights(accessor =>
                {
                    foreach (var range in customLineHeightRangesToInsert)
                    {
                        var customLineHeights = GetCustomLineHeightsForLines(range.FromLineNumber, range.ToLineNumber);
                        foreach (var data in customLineHeights)
                        {
                            accessor.InsertOrChangeCustomLineHeight(data.DecorationId, data.StartLineNumber, data.EndLineNumber, data.LineHeight);
                        }
                    }
                });
            }

            ViewLayout.OnHeightMaybeChanged();

            if (!hadOtherModelChange && hadModelLineChangeThatChangedLineMapping)
            {
                eventsCollector.EmitViewEvent(new ViewLineMappingChangedEvent());
                eventsCollector.EmitViewEvent(new ViewDecorationsChangedEvent(null));
                _cursor.OnLineMappingChanged(eventsCollector);
                _decorations.OnLineMappingChanged();
            }
        }
        finally
        {
            _eventDispatcher.EndEmitViewEvents();
        }

        // Update the configuration and reset the centered view line
        var viewportStartWasValid = _viewportStart.IsValid;
        _viewportStart.Invalidate();
        _configuration.SetModelLineCount(Model.GetLineCount());
        UpdateConfigurationViewLineCountNow();

        // Recover viewport
        if (!_hasFocus && Model.GetAttachedEditorCount() >= 2 && viewportStartWasValid)
        {
            var modelRange = Model.GetTrackedRange(_viewportStart.ModelTrackedRange);
            if (modelRange is { } range)
            {
                var viewPosition = CoordinatesConverter.ConvertModelPositionToViewPosition(range.Start);
                var viewPositionTop = ViewLayout.GetVerticalOffsetForLineNumber(viewPosition.LineNumber);
                ViewLayout.SetScrollPosition(new NewScrollPosition(ScrollTop: viewPositionTop + _viewportStart.StartLineDelta), ScrollType.Immediate);
            }
        }

        HandleVisibleLinesChanged();
    }

    /// <summary>Gets called directly by the text model.</summary>
    public void EmitContentChangeEvent(IModelContentOrInjectedTextChangedEvent e)
    {
        EmitViewEvent(eventsCollector =>
        {
            if (e is InternalModelContentChangeEvent contentChange)
            {
                eventsCollector.EmitOutgoingEvent(new OutgoingModelContentChangedEvent(contentChange.ContentChangedEvent));
            }
            _cursor.OnModelContentChanged(eventsCollector, e);
            return true;
        });
    }

    private void RegisterModelEvents()
    {
        var allowVariableLineHeights = _configuration.Options.AllowVariableLineHeights;
        if (allowVariableLineHeights)
        {
            Action<ModelLineHeightChangedEvent> onDidChangeLineHeight = e =>
            {
                var filteredChanges = e.Changes.Where(change => change.OwnerId == _editorId || change.OwnerId == 0).ToList();

                ViewLayout.ChangeSpecialLineHeights(accessor =>
                {
                    foreach (var change in filteredChanges)
                    {
                        var lineNumber = change.LineNumber;
                        var viewRange = CoordinatesConverter.ConvertModelRangeToViewRange(new TextRange(lineNumber, 1, lineNumber, Model.GetLineMaxColumn(lineNumber)));
                        if (change.LineHeightMultiplier is { } lineHeightMultiplier)
                        {
                            accessor.InsertOrChangeCustomLineHeight(change.DecorationId, viewRange.StartLineNumber, viewRange.EndLineNumber, lineHeightMultiplier * _configuration.Options.Layout.LineHeight);
                        }
                        else
                        {
                            accessor.RemoveCustomLineHeight(change.DecorationId);
                        }
                    }
                });

                // recreate the model event using the filtered changes
                if (filteredChanges.Count > 0)
                {
                    var filteredEvent = new ModelLineHeightChangedEvent(filteredChanges);
                    _eventDispatcher.EmitOutgoingEvent(new OutgoingModelLineHeightChangedEvent(filteredEvent));
                }
            };
            Model.DidChangeLineHeight += onDidChangeLineHeight;
            _toDispose.Add(() => Model.DidChangeLineHeight -= onDidChangeLineHeight);
        }

        var allowVariableFonts = _configuration.Options.EffectiveAllowVariableFonts;
        if (allowVariableFonts)
        {
            Action<ModelFontChangedEvent> onDidChangeFont = e =>
            {
                var filteredChanges = e.Changes.Where(change => change.OwnerId == _editorId || change.OwnerId == 0).ToList();
                // recreate the model event using the filtered changes
                if (filteredChanges.Count > 0)
                {
                    var filteredEvent = new ModelFontChangedEvent(filteredChanges);
                    _eventDispatcher.EmitOutgoingEvent(new OutgoingModelFontChangedEvent(filteredEvent));
                }
            };
            Model.DidChangeFont += onDidChangeFont;
            _toDispose.Add(() => Model.DidChangeFont -= onDidChangeFont);
        }

        // Upstream's model.onDidChangeLanguageConfiguration: the tokenization part forwards the language configuration
        // service's changes that affect the model's language.
        Action<LanguageConfigurationServiceChangeEvent> onDidChangeLanguageConfiguration = e =>
        {
            if (!e.Affects(Model.GetLanguageId()))
            {
                return;
            }
            _eventDispatcher.EmitSingleViewEvent(new ViewLanguageConfigurationEvent());
            RecreateCursorConfiguration();
            _eventDispatcher.EmitOutgoingEvent(new OutgoingModelLanguageConfigurationChangedEvent(new ModelLanguageConfigurationChangedEvent()));
        };
        _languageConfigurationService.DidChange += onDidChangeLanguageConfiguration;
        _toDispose.Add(() => _languageConfigurationService.DidChange -= onDidChangeLanguageConfiguration);

        Action<ModelTokensChangedEvent> onDidChangeTokens = e =>
        {
            var viewRanges = new ViewTokensChangedRange[e.Ranges.Count];
            for (var j = 0; j < e.Ranges.Count; j++)
            {
                var modelRange = e.Ranges[j];
                var viewStartLineNumber = CoordinatesConverter.ConvertModelPositionToViewPosition(
                    new TextPosition(modelRange.FromLineNumber, 1)).LineNumber;
                var viewEndLineNumber = CoordinatesConverter.ConvertModelPositionToViewPosition(
                    new TextPosition(modelRange.ToLineNumber, Model.GetLineMaxColumn(modelRange.ToLineNumber))).LineNumber;
                viewRanges[j] = new ViewTokensChangedRange(viewStartLineNumber, viewEndLineNumber);
            }

            _eventDispatcher.EmitSingleViewEvent(new ViewTokensChangedEvent(viewRanges));
            _eventDispatcher.EmitOutgoingEvent(new OutgoingModelTokensChangedEvent(e));
        };
        Model.DidChangeTokens += onDidChangeTokens;
        _toDispose.Add(() => Model.DidChangeTokens -= onDidChangeTokens);

        Action<ModelLanguageChangedEvent> onDidChangeLanguage = e =>
        {
            RecreateCursorConfiguration();
            _eventDispatcher.EmitOutgoingEvent(new OutgoingModelLanguageChangedEvent(e));
        };
        Model.DidChangeLanguage += onDidChangeLanguage;
        _toDispose.Add(() => Model.DidChangeLanguage -= onDidChangeLanguage);

        Action<ModelOptionsChangedEvent> onDidChangeOptions = e =>
        {
            // A tab size change causes a line mapping changed event => all view parts will repaint OK, no further event needed here
            if (_lines.SetTabSize(Model.GetOptions().TabSize))
            {
                try
                {
                    var eventsCollector = _eventDispatcher.BeginEmitViewEvents();
                    eventsCollector.EmitViewEvent(new ViewFlushedEvent());
                    eventsCollector.EmitViewEvent(new ViewLineMappingChangedEvent());
                    eventsCollector.EmitViewEvent(new ViewDecorationsChangedEvent(null));
                    _cursor.OnLineMappingChanged(eventsCollector);
                    _decorations.OnLineMappingChanged();
                    ViewLayout.OnFlushed(GetLineCount(), GetCustomLineHeights());
                }
                finally
                {
                    _eventDispatcher.EndEmitViewEvents();
                }
                UpdateConfigurationViewLineCountNow();
            }

            RecreateCursorConfiguration();

            _eventDispatcher.EmitOutgoingEvent(new OutgoingModelOptionsChangedEvent(e));
        };
        Model.DidChangeOptions += onDidChangeOptions;
        _toDispose.Add(() => Model.DidChangeOptions -= onDidChangeOptions);

        Action<ModelDecorationsChangedEvent> onDidChangeDecorations = e =>
        {
            _decorations.OnModelDecorationsChanged();
            _eventDispatcher.EmitSingleViewEvent(new ViewDecorationsChangedEvent(e));
            _eventDispatcher.EmitOutgoingEvent(new OutgoingModelDecorationsChangedEvent(e));
        };
        Model.DidChangeDecorations += onDidChangeDecorations;
        _toDispose.Add(() => Model.DidChangeDecorations -= onDidChangeDecorations);
    }
}
