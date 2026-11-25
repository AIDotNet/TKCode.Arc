// Ported from vscode (src/vs/editor/contrib/suggest/browser/suggestController.ts: LineSuffix, SuggestController;
// suggestCommitCharacters.ts: CommitCharacterController; commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the suggest model is the engine's (TKCode.Arc.Editor.Contrib.Suggest.SuggestModel) over a CodeEditorSuggestEditor;
//  - the controller owns the editor's options of the feature (Options) and the memory of accepted items
//    (SuggestMemoryService, in memory only), and registers the word-based completion provider of the editor;
//  - completions go in through ISuggestSnippetInserter (SnippetInserter.cs): the editor's snippet controller, else plain text; the snippets feature
//    is merged;
//  - not ported: suggest alternatives (insertNext/PrevSuggestion), tab completion (insertBestCompletion,
//    triggerSuggestAndAcceptBest), the overtyping capturer, the word context key, the explain mode, telemetry, aria
//    alerts and the status bar menu; the item selectors other than the memory (registerSelector).

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Suggest;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc;

/// <summary>Code completion for a <see cref="CodeEditor"/> (upstream's SuggestController).</summary>
internal sealed partial class SuggestController : ICodeEditorContribution
{
    public const string Id = "editor.contrib.suggestController";

    /// <summary>Created before the first key or pointer press, as upstream.</summary>
    public static readonly EditorContributionDescription Description =
        new(Id, editor => new SuggestController(editor), EditorContributionInstantiation.BeforeFirstInteraction);

    [Flags]
    internal enum InsertFlags
    {
        None = 0,
        NoBeforeUndoStop = 1,
        NoAfterUndoStop = 2,
        KeepAlternativeSuggestions = 4,
        AlternativeOverwriteConfig = 8,
    }

    private readonly CodeEditor _editor;
    private readonly CodeEditorSuggestEditor _suggestEditor;
    private readonly SuggestMemoryService _memoryService;
    private readonly IDisposable _wordBasedRegistration;
    private readonly IContextKey<string> _ctxInsertMode;
    private readonly IContextKey<bool> _ctxMakesTextEdit;
    private readonly IContextKey<bool> _ctxHasInsertAndReplace;
    private readonly IContextKey<bool> _ctxCanResolve;
    private readonly IContextKey<bool> _ctxAcceptSuggestionsOnEnter;
    private readonly Action<bool> _onTextFocus;
    private readonly Action<string> _onWillType;
    private SuggestOptions _options = SuggestOptions.Default;
    private CodeEditorSuggestWidget? _widget;
    private LineSuffix? _lineSuffix;
    private (HashSet<char> AcceptCharacters, SelectedSuggestion Item)? _activeCommitCharacters;
    private bool _disposed;

    private SuggestController(CodeEditor editor)
    {
        _editor = editor;
        _suggestEditor = new CodeEditorSuggestEditor(editor, () => _options);
        Model = new SuggestModel(_suggestEditor);
        _memoryService = new SuggestMemoryService(editor.LanguageConfigurationService);

        // the words of the document, when no language provider answers (upstream's editor worker service registers it)
        _wordBasedRegistration = editor.LanguageFeatures.CompletionProvider.Register(
            new LanguageFilter { Language = "*" },
            new WordBasedCompletionItemProvider(() => _options.WordBasedSuggestions, editor.LanguageConfigurationService));

        // context key: update insert/replace mode
        _ctxInsertMode = editor.CreateContextKey(SuggestContext.InsertMode);
        _ctxInsertMode.Set(InsertModeName(_options.InsertMode));
        _ctxMakesTextEdit = editor.CreateContextKey(SuggestContext.MakesTextEdit);
        _ctxHasInsertAndReplace = editor.CreateContextKey(SuggestContext.HasInsertAndReplaceRange);
        _ctxCanResolve = editor.CreateContextKey(SuggestContext.CanResolve);
        _ctxAcceptSuggestionsOnEnter = editor.CreateContextKey(SuggestContext.AcceptSuggestionsOnEnter);
        UpdateFromConfig();

        Model.DidTrigger += OnModelTrigger;
        Model.DidSuggest += OnModelSuggest;
        Model.DidCancel += OnModelCancel;

        _onTextFocus = focused =>
        {
            if (!focused && !_editor.IsKeyboardFocusWithin)
            {
                Model.Cancel();
                Model.Clear();
            }
        };
        editor.DidChangeTextFocus += _onTextFocus;

        // commit characters (upstream's CommitCharacterController)
        _onWillType = OnWillType;
        editor.WillType += _onWillType;
    }

    /// <summary>The suggest controller of an editor, or null.</summary>
    public static SuggestController? Get(CodeEditor editor) => editor.GetContribution<SuggestController>(Id);

    /// <summary>The suggest model: when to ask the providers and how to filter.</summary>
    public SuggestModel Model { get; }

    /// <summary>The widget, created on first use.</summary>
    public CodeEditorSuggestWidget Widget => _widget ??= CreateWidget();

    /// <summary>Whether the widget was created.</summary>
    public bool HasWidget => _widget is not null;

    /// <summary>How completions are inserted (the snippets feature's controller once merged).</summary>
    public ISuggestSnippetInserter SnippetInserter { get; set; } = SnippetControllerSuggestInserter.Instance;

    /// <summary>The snippet provider asked with the language providers (upstream's snippet suggest support).</summary>
    public ICompletionItemProvider? SnippetSuggestSupport
    {
        get => _suggestEditor.SnippetSuggestSupport;
        set => _suggestEditor.SnippetSuggestSupport = value;
    }

    /// <summary>The feature's options (upstream's editor.suggest, quickSuggestions, acceptSuggestionOnEnter, ...).</summary>
    public SuggestOptions Options
    {
        get => _options;
        set
        {
            _options = value;
            UpdateFromConfig();
            _suggestEditor.RaiseConfigurationChanged();
        }
    }

    private static string InsertModeName(SuggestInsertMode mode) => mode == SuggestInsertMode.Replace ? "replace" : "insert";

    private void UpdateFromConfig() =>
        _ctxAcceptSuggestionsOnEnter.Set(_options.AcceptSuggestionOnEnter is AcceptSuggestionOnEnter.On or AcceptSuggestionOnEnter.Smart);

    private CodeEditorSuggestWidget CreateWidget()
    {
        var widget = new CodeEditorSuggestWidget(_editor);
        widget.DidSelect += item => InsertSuggestion(item, InsertFlags.None);
        widget.DidShow += () => OnCommitItem(widget.GetFocusedItem());
        widget.DidFocus += OnWidgetFocus;
        widget.DidHide += ResetCommitCharacters;
        _editor.AddContentWidget(widget);
        return widget;
    }

    private void OnWidgetFocus(SelectedSuggestion selected)
    {
        OnCommitItem(selected);
        var item = selected.Item;

        // (ctx: makesTextEdit)
        var position = _editor.GetPosition() ?? item.Position;
        var startColumn = item.EditStart.Column;
        var endColumn = position.Column;
        var value = true;
        if (_options.AcceptSuggestionOnEnter == AcceptSuggestionOnEnter.Smart
            && Model.State == SuggestState.Auto
            && item.Completion.AdditionalTextEdits is null
            && (item.Completion.InsertTextRules & CompletionItemInsertTextRule.InsertAsSnippet) == 0
            && endColumn - startColumn == item.Completion.InsertText.Length)
        {
            var oldText = _editor.Model.GetValueInRange(new TextRange(position.LineNumber, startColumn, position.LineNumber, endColumn));
            value = oldText != item.Completion.InsertText;
        }
        _ctxMakesTextEdit.Set(value);

        // (ctx: hasInsertAndReplaceRange)
        _ctxHasInsertAndReplace.Set(item.EditInsertEnd != item.EditReplaceEnd);

        // (ctx: canResolve)
        _ctxCanResolve.Set(item.Provider.SupportsResolveCompletionItem || item.Completion.Documentation is not null || item.Completion.Detail != item.TextLabel);
    }

    private void OnModelTrigger(SuggestTriggerEvent e)
    {
        _ctxInsertMode.Set(InsertModeName(_options.InsertMode));
        Widget.ShowTriggered(e.Auto, e.Shy ? 250 : 50);
        _lineSuffix?.Dispose();
        _lineSuffix = new LineSuffix(_editor.Model, e.Position);
    }

    private void OnModelSuggest(SuggestEvent e)
    {
        if (e.TriggerOptions.Shy)
        {
            return;
        }

        var position = _editor.GetPosition() ?? new TextPosition(1, 1);
        var index = _memoryService.Select(_editor.Model, position, e.CompletionModel.Items, _options.SuggestSelection);
        if (index == -1)
        {
            index = 0;
        }
        if (Model.State == SuggestState.Idle)
        {
            // selecting an item can "pump" out selection/cursor change events which can cancel suggest halfway
            return;
        }

        var noFocus = false;
        if (e.TriggerOptions.Auto)
        {
            // don't "focus" item when configured to do
            noFocus = _options.SelectionMode switch
            {
                SuggestSelectionMode.Never => true,
                SuggestSelectionMode.WhenTriggerCharacter => e.TriggerOptions.TriggerKind != CompletionTriggerKind.TriggerCharacter,
                SuggestSelectionMode.WhenQuickSuggestion => e.TriggerOptions.TriggerKind == CompletionTriggerKind.TriggerCharacter && !e.TriggerOptions.Refilter,
                _ => false,
            };
        }
        Widget.ShowSuggestions(e.CompletionModel, index, e.IsFrozen, e.TriggerOptions.Auto, noFocus);
        if (e.CompletionModel.Items.Count == 0)
        {
            ResetCommitCharacters();
        }
    }

    private void OnModelCancel(SuggestCancelEvent e)
    {
        ResetCommitCharacters();
        if (!e.Retrigger)
        {
            _widget?.HideWidget();
        }
    }

    // ---- commit characters (upstream's CommitCharacterController)

    private void OnCommitItem(SelectedSuggestion? selected)
    {
        if (selected is null || selected.Item.Completion.CommitCharacters is not { Count: > 0 } characters)
        {
            // no item or no commit characters
            ResetCommitCharacters();
            return;
        }

        if (_activeCommitCharacters is { } active && ReferenceEquals(active.Item.Item, selected.Item))
        {
            // still the same item
            return;
        }

        // keep item and its commit characters
        var acceptCharacters = new HashSet<char>();
        foreach (var ch in characters)
        {
            if (ch.Length > 0)
            {
                acceptCharacters.Add(ch[0]);
            }
        }
        _activeCommitCharacters = (acceptCharacters, selected);
    }

    private void ResetCommitCharacters() => _activeCommitCharacters = null;

    private void OnWillType(string text)
    {
        if (_activeCommitCharacters is { } active && text.Length > 0 && _widget is { } widget && !widget.IsFrozen() && Model.State != SuggestState.Idle)
        {
            var ch = text[^1];
            if (active.AcceptCharacters.Contains(ch) && _options.AcceptSuggestionOnCommitCharacter)
            {
                InsertSuggestion(active.Item, InsertFlags.NoAfterUndoStop);
            }
        }
    }

    // ---- insert

    /// <summary>Inserts a suggestion (upstream's _insertSuggestion); null cancels.</summary>
    internal void InsertSuggestion(SelectedSuggestion? selected, InsertFlags flags)
    {
        if (selected is null)
        {
            Model.Cancel();
            Model.Clear();
            return;
        }

        var model = _editor.Model;
        if (_editor.GetPosition() is not { } position)
        {
            return;
        }

        var item = selected.Item;
        var tasks = new List<Task>();
        var cts = new CancellationTokenSource();

        // pushing undo stops *before* additional text edits and *after* the main edit
        if ((flags & InsertFlags.NoBeforeUndoStop) == 0)
        {
            _editor.PushUndoStop();
        }

        // compute overwrite[Before|After] deltas BEFORE applying extra edits
        var info = GetOverwriteInfo(item, (flags & InsertFlags.AlternativeOverwriteConfig) != 0);

        // keep item in memory
        _memoryService.Memorize(model, position, item, _options.SuggestSelection);

        var isResolved = item.IsResolved;

        if (item.Completion.AdditionalTextEdits is { } additionalTextEdits)
        {
            // cancel -> stops all listening and closes widget
            Model.Cancel();

            // sync additional edits
            var cursorColumn = position.Column;
            _editor.ExecuteEdits(
                "suggestController.additionalTextEdits.sync",
                additionalTextEdits.Select(edit =>
                {
                    var range = edit.Range;
                    if (range.StartLineNumber == item.Position.LineNumber && range.StartColumn > item.Position.Column)
                    {
                        // shift additional edit when it is "after" the completion insertion position
                        var columnDelta = cursorColumn - item.Position.Column;
                        var endColumnDelta = range.StartLineNumber != range.EndLineNumber ? 0 : columnDelta;
                        range = new TextRange(range.StartLineNumber, range.StartColumn + columnDelta, range.EndLineNumber, range.EndColumn + endColumnDelta);
                    }
                    return EditOperation.ReplaceMove(range, edit.Text);
                }).ToList());
        }
        else if (!isResolved)
        {
            // async additional edits
            tasks.Add(ApplyAdditionalEditsWhenResolved(item, flags, cts));
            flags |= InsertFlags.NoAfterUndoStop;
        }

        var insertText = item.Completion.InsertText;
        if ((item.Completion.InsertTextRules & CompletionItemInsertTextRule.InsertAsSnippet) == 0)
        {
            insertText = PlainSnippet.Escape(insertText);
        }

        // cancel -> stops all listening and closes widget
        Model.Cancel();

        SnippetInserter.Insert(_editor, insertText, new SnippetInsertParams(
            info.OverwriteBefore,
            info.OverwriteAfter,
            UndoStopBefore: false,
            UndoStopAfter: false,
            AdjustWhitespace: (item.Completion.InsertTextRules & CompletionItemInsertTextRule.KeepWhitespace) == 0,
            ClipboardText: selected.Model.ClipboardText));

        if ((flags & InsertFlags.NoAfterUndoStop) == 0)
        {
            _editor.PushUndoStop();
        }

        if (item.Completion.Command is { } command)
        {
            if (command.Id == TriggerSuggestActionId)
            {
                // retrigger
                Model.Trigger(new SuggestTriggerOptions { Auto = true, Retrigger = true });
            }
            else
            {
                // exec command, done
                tasks.Add(ExecuteItemCommand(command));
            }
        }

        // clear only now - after all tasks are done
        _ = ClearWhenDone(tasks, cts);
    }

    private async Task ExecuteItemCommand(LanguageCommand command)
    {
        try
        {
            await _editor.ExecuteCommand(command.Id, command.Arguments is { Count: 1 } one ? one[0] : command.Arguments);
        }
        catch (Exception e)
        {
            UnexpectedErrors.Report(e);
        }
    }

    private async Task ClearWhenDone(List<Task> tasks, CancellationTokenSource cts)
    {
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            UnexpectedErrors.Report(e);
        }
        catch (OperationCanceledException)
        {
            // cancelled
        }
        finally
        {
            if (!_disposed)
            {
                Model.Clear();
            }
            cts.Dispose();
        }
    }

    // upstream's "async additional edits": resolve the item after inserting it, then apply its additional edits unless
    // the document changed before them meanwhile
    private async Task ApplyAdditionalEditsWhenResolved(SuggestCompletionItem item, InsertFlags oldFlags, CancellationTokenSource cts)
    {
        var model = _editor.Model;
        TextPosition? position = null;
        var didType = false;

        void OnContent(ModelContentChangedEvent e)
        {
            if (e.IsFlush)
            {
                cts.Cancel();
                return;
            }
            foreach (var change in e.Changes)
            {
                var thisPosition = change.Range.End;
                if (position is null || thisPosition.IsBefore(position.Value))
                {
                    position = thisPosition;
                }
            }
        }

        void OnWillTypeOnce(string _)
        {
            if (didType)
            {
                return;
            }
            didType = true;
            if ((oldFlags & InsertFlags.NoAfterUndoStop) == 0)
            {
                _editor.PushUndoStop();
            }
        }

        _editor.DidChangeModelContent += OnContent;
        _editor.WillType += OnWillTypeOnce;
        try
        {
            await item.Resolve(cts.Token);
            if (item.Completion.AdditionalTextEdits is not { } edits || cts.IsCancellationRequested || _disposed)
            {
                return;
            }
            if (position is { } p && edits.Any(edit => p.IsBefore(edit.Range.Start)))
            {
                return;
            }
            if (didType)
            {
                _editor.PushUndoStop();
            }
            _editor.ExecuteEdits(
                "suggestController.additionalTextEdits.async",
                edits.Select(edit => EditOperation.ReplaceMove(edit.Range, edit.Text)).ToList());
            if (didType || (oldFlags & InsertFlags.NoAfterUndoStop) == 0)
            {
                _editor.PushUndoStop();
            }
        }
        finally
        {
            _editor.DidChangeModelContent -= OnContent;
            _editor.WillType -= OnWillTypeOnce;
        }
    }

    /// <summary>How many characters left and right of the cursor the item replaces (upstream's getOverwriteInfo).</summary>
    internal (int OverwriteBefore, int OverwriteAfter) GetOverwriteInfo(SuggestCompletionItem item, bool toggleMode)
    {
        var replace = _options.InsertMode == SuggestInsertMode.Replace;
        if (toggleMode)
        {
            replace = !replace;
        }
        var position = _editor.GetPosition() ?? item.Position;
        var overwriteBefore = item.Position.Column - item.EditStart.Column;
        var overwriteAfter = (replace ? item.EditReplaceEnd.Column : item.EditInsertEnd.Column) - item.Position.Column;
        var columnDelta = position.Column - item.Position.Column;
        var suffixDelta = _lineSuffix?.Delta(position) ?? 0;

        return (overwriteBefore + columnDelta, overwriteAfter + suffixDelta);
    }

    // ---- what the commands call

    /// <summary>Asks for suggestions at the cursor (upstream's triggerSuggest).</summary>
    public void TriggerSuggest(IReadOnlySet<ICompletionItemProvider>? onlyFrom = null, bool? auto = null, bool noFilter = false)
    {
        Model.Trigger(new SuggestTriggerOptions
        {
            Auto = auto ?? false,
            ProviderFilter = onlyFrom,
            KindFilter = noFilter ? new HashSet<CompletionItemKind>() : null,
        });
        if (_editor.GetPosition() is { } position)
        {
            _editor.RevealRange(new TextRange(position));
        }
        _editor.Focus();
    }

    public void AcceptSelectedSuggestion(bool keepAlternativeSuggestions, bool alternativeOverwriteConfig)
    {
        var flags = InsertFlags.None;
        if (keepAlternativeSuggestions)
        {
            flags |= InsertFlags.KeepAlternativeSuggestions;
        }
        if (alternativeOverwriteConfig)
        {
            flags |= InsertFlags.AlternativeOverwriteConfig;
        }
        InsertSuggestion(_widget?.GetFocusedItem(), flags);
    }

    public void CancelSuggestWidget()
    {
        Model.Cancel();
        Model.Clear();
        _widget?.HideWidget();
    }

    public void FocusSuggestion() => _widget?.FocusSelected();

    public void SelectNextSuggestion() => _widget?.SelectNext();

    public void SelectNextPageSuggestion() => _widget?.SelectNextPage();

    public void SelectLastSuggestion() => _widget?.SelectLast();

    public void SelectPrevSuggestion() => _widget?.SelectPrevious();

    public void SelectPrevPageSuggestion() => _widget?.SelectPreviousPage();

    public void SelectFirstSuggestion() => _widget?.SelectFirst();

    public void ToggleSuggestionDetails() => _widget?.ToggleDetails();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _editor.DidChangeTextFocus -= _onTextFocus;
        _editor.WillType -= _onWillType;
        Model.DidTrigger -= OnModelTrigger;
        Model.DidSuggest -= OnModelSuggest;
        Model.DidCancel -= OnModelCancel;
        if (_widget is { } widget)
        {
            widget.HideWidget();
            _editor.RemoveContentWidget(widget);
        }
        Model.Dispose();
        _suggestEditor.Dispose();
        _wordBasedRegistration.Dispose();
        _lineSuffix?.Dispose();
        _ctxInsertMode.Reset();
        _ctxMakesTextEdit.Reset();
        _ctxHasInsertAndReplace.Reset();
        _ctxCanResolve.Reset();
        _ctxAcceptSuggestionsOnEnter.Reset();
    }

    /// <summary>Spies on the text right of where suggestions were asked for (upstream's LineSuffix).</summary>
    private sealed class LineSuffix : IDisposable
    {
        private readonly TextModel _model;
        private readonly TextPosition _position;
        private string? _marker;

        public LineSuffix(TextModel model, TextPosition position)
        {
            _model = model;
            _position = position;

            // 1. end of line -> check that it's still end of line; 2. mid of line -> add a marker and compute the delta
            var maxColumn = model.GetLineMaxColumn(position.LineNumber);
            if (maxColumn != position.Column)
            {
                var offset = model.GetOffsetAt(position);
                var end = model.GetPositionAt(offset + 1);
                _marker = model.DeltaDecorations(null,
                [
                    new ModelDeltaDecoration(new TextRange(position, end), ModelDecorationOptions.TrackedRangeOptions[(int)TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges]),
                ])[0];
            }
        }

        public void Dispose()
        {
            if (_marker is { } marker && !_model.IsDisposed())
            {
                _model.DeltaDecorations([marker], []);
                _marker = null;
            }
        }

        public int Delta(TextPosition position)
        {
            if (_model.IsDisposed() || _position.LineNumber != position.LineNumber)
            {
                // bail out early if things seems fishy
                return 0;
            }

            // read the marker (in case suggest was triggered at line end) or compare the cursor to the line end
            if (_marker is { } marker && _model.GetDecorationRange(marker) is { } range)
            {
                var end = _model.GetOffsetAt(range.Start);
                return end - _model.GetOffsetAt(position);
            }
            return _model.GetLineMaxColumn(position.LineNumber) - position.Column;
        }
    }
}
