// Ported from vscode (src/vs/editor/contrib/find/browser/findController.ts, commit b57d5c9e), MIT license.
// This file: getSelectionSearchString, IFindStartOptions, IFindStartArguments, CommonFindController and the bodies of
// the find actions (FindActions). The widget-owning FindController, the action registrations and the keybindings are
// in TKCode.Arc.Editor.Controls (Contrib/Find/FindController.cs).
// Differences:
//  - the controller runs on an IFindEditor; the storage, clipboard and context key service come from it;
//  - the history delayer and the notification ("too large for replace all") are not ported: replaceAll returns false;
//  - the editor's model never changes here (CodeEditor keeps one model), so onDidChangeModel is not ported;
//  - the action bodies are static methods that take the controller (upstream: functions that look the controller up
//    from the editor), so the engine's tests run them without the editor control;
//  - MoveToMatchFindAction (a quick input to go to match N) is not ported; GoToMatch is.

using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc.Editor.Contrib.Find;

/// <summary>How the search string is seeded when find starts (upstream's 'none' | 'single' | 'multiple').</summary>
internal enum SeedSearchStringMode
{
    None,
    Single,
    Multiple,
}

/// <summary>What gets focus when find starts (upstream's FindStartFocusAction).</summary>
internal enum FindStartFocusAction
{
    NoFocusChange,
    FocusFindInput,
    FocusReplaceInput,
}

/// <summary>How find starts (upstream's IFindStartOptions).</summary>
internal sealed record FindStartOptions
{
    public bool ForceRevealReplace { get; init; }

    public SeedSearchStringMode SeedSearchStringFromSelection { get; init; }

    public bool SeedSearchStringFromNonEmptySelection { get; init; }

    public bool SeedSearchStringFromGlobalClipboard { get; init; }

    public FindStartFocusAction ShouldFocus { get; init; }

    public bool ShouldAnimate { get; init; }

    public bool UpdateSearchScope { get; init; }

    public bool Loop { get; init; } = true;
}

/// <summary>The arguments of <c>editor.actions.findWithArgs</c> (upstream's IFindStartArguments).</summary>
internal sealed record FindStartArguments
{
    public string? SearchString { get; init; }

    public string? ReplaceString { get; init; }

    public bool? IsRegex { get; init; }

    public bool? MatchWholeWord { get; init; }

    public bool? IsCaseSensitive { get; init; }

    public bool? PreserveCase { get; init; }

    public bool? FindInSelection { get; init; }
}

/// <summary>
/// Find and replace in one editor, without the widget (upstream's CommonFindController): the state, the find model
/// while find is revealed, the persisted toggles and the operations the find actions and widget call.
/// </summary>
internal class CommonFindController : IDisposable
{
    public const string Id = "editor.contrib.findController";

    // upstream's SEARCH_STRING_MAX_LENGTH
    private const int SearchStringMaxLength = 524288;

    /// <summary>Upstream's CONTEXT_FIND_WIDGET_VISIBLE.</summary>
    public static readonly RawContextKey<bool> ContextFindWidgetVisible = new("findWidgetVisible", false);

    /// <summary>Upstream's CONTEXT_FIND_INPUT_FOCUSED.</summary>
    public static readonly RawContextKey<bool> ContextFindInputFocused = new("findInputFocussed", false);

    /// <summary>Upstream's CONTEXT_REPLACE_INPUT_FOCUSED.</summary>
    public static readonly RawContextKey<bool> ContextReplaceInputFocused = new("replaceInputFocussed", false);

    private readonly IContextKey<bool> _findWidgetVisible;
    private readonly IFindStorage _storage;
    private readonly Action<FindReplaceStateChangedEvent> _onStateChanged;
    private FindModelBoundToEditorModel? _model;
    private bool _isDisposed;

    public CommonFindController(IFindEditor editor)
    {
        Editor = editor;
        _findWidgetVisible = ContextFindWidgetVisible.BindTo(editor.ContextKeyService);
        _storage = editor.Storage ?? new InMemoryFindStorage();

        State = new FindReplaceState();
        LoadQueryState();
        _onStateChanged = OnStateChanged;
        State.FindReplaceStateChange += _onStateChanged;
    }

    public IFindEditor Editor { get; }

    /// <summary>The find state (upstream's getState()).</summary>
    public FindReplaceState State { get; }

    /// <summary>The find model while find is revealed (for tests).</summary>
    internal FindModelBoundToEditorModel? Model => _model;

    public FindReplaceState GetState() => State;

    public virtual void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        DisposeModel();
        State.FindReplaceStateChange -= _onStateChanged;
        State.Dispose();
    }

    private void DisposeModel()
    {
        if (_model is not null)
        {
            _model.Dispose();
            _model = null;
        }
    }

    private void OnStateChanged(FindReplaceStateChangedEvent e)
    {
        SaveQueryState(e);

        if (e.IsRevealed)
        {
            if (State.IsRevealed)
            {
                _findWidgetVisible.Set(true);
            }
            else
            {
                _findWidgetVisible.Reset();
                DisposeModel();
            }
        }
        if (e.SearchString)
        {
            SetGlobalBufferTerm(State.SearchString);
        }
    }

    private void SaveQueryState(FindReplaceStateChangedEvent e)
    {
        if (e.IsRegex)
        {
            _storage.Store("editor.isRegex", State.ActualIsRegex);
        }
        if (e.WholeWord)
        {
            _storage.Store("editor.wholeWord", State.ActualWholeWord);
        }
        if (e.MatchCase)
        {
            _storage.Store("editor.matchCase", State.ActualMatchCase);
        }
        if (e.PreserveCase)
        {
            _storage.Store("editor.preserveCase", State.ActualPreserveCase);
        }
    }

    private void LoadQueryState()
    {
        State.Change(new NewFindReplaceState
        {
            MatchCase = _storage.GetBoolean("editor.matchCase", State.MatchCase),
            WholeWord = _storage.GetBoolean("editor.wholeWord", State.WholeWord),
            IsRegex = _storage.GetBoolean("editor.isRegex", State.IsRegex),
            PreserveCase = _storage.GetBoolean("editor.preserveCase", State.PreserveCase),
        }, false);
    }

    public bool IsFindInputFocused() => ContextFindInputFocused.GetValue(Editor.ContextKeyService);

    /// <summary>Whether the replace input was the last focused input of the widget (false without a widget).</summary>
    public virtual bool WasReplaceInputLastFocused() => false;

    /// <summary>Focuses the last focused element of the widget (nothing without a widget).</summary>
    public virtual void FocusLastElement()
    {
    }

    public void CloseFindWidget()
    {
        State.Change(new NewFindReplaceState { IsRevealed = false, SearchScope = null }, false);
        Editor.Focus();
    }

    public void ToggleCaseSensitive()
    {
        State.Change(new NewFindReplaceState { MatchCase = !State.MatchCase }, false);
        if (!State.IsRevealed)
        {
            HighlightFindOptions();
        }
    }

    public void ToggleWholeWords()
    {
        State.Change(new NewFindReplaceState { WholeWord = !State.WholeWord }, false);
        if (!State.IsRevealed)
        {
            HighlightFindOptions();
        }
    }

    public void ToggleRegex()
    {
        State.Change(new NewFindReplaceState { IsRegex = !State.IsRegex }, false);
        if (!State.IsRevealed)
        {
            HighlightFindOptions();
        }
    }

    public void TogglePreserveCase()
    {
        State.Change(new NewFindReplaceState { PreserveCase = !State.PreserveCase }, false);
        if (!State.IsRevealed)
        {
            HighlightFindOptions();
        }
    }

    public void ToggleSearchScope()
    {
        if (State.SearchScope is not null)
        {
            State.Change(new NewFindReplaceState { SearchScope = null }, true);
        }
        else if (Editor.GetModel() is { } model)
        {
            var selections = new List<TextRange>();
            foreach (var original in Editor.GetSelections())
            {
                var selection = original;
                if (selection.EndColumn == 1 && selection.EndLineNumber > selection.StartLineNumber)
                {
                    selection = selection.SetEndPosition(selection.EndLineNumber - 1, model.GetLineMaxColumn(selection.EndLineNumber - 1));
                }
                if (!selection.IsEmpty)
                {
                    selections.Add(selection);
                }
            }

            if (selections.Count > 0)
            {
                State.Change(new NewFindReplaceState { SearchScope = selections }, true);
            }
        }
    }

    public void SetSearchString(string searchString)
    {
        if (State.IsRegex)
        {
            searchString = TKCode.Arc.Editor.Model.SearchHelpers.EscapeRegExpCharacters(searchString);
        }

        State.Change(new NewFindReplaceState { SearchString = searchString }, false);
    }

    /// <summary>Draws attention to the find options (overwritten by the widget controller).</summary>
    public virtual void HighlightFindOptions(bool ignoreWhenVisible = false)
    {
    }

    /// <summary>Upstream's _start: reveals find with the options; subclasses create and focus their widget.</summary>
    protected virtual async Task StartCore(FindStartOptions opts, NewFindReplaceState? newState)
    {
        DisposeModel();

        if (Editor.GetModel() is null)
        {
            // cannot do anything with an editor that doesn't have a model...
            return;
        }

        var stateChanges = (newState ?? new NewFindReplaceState()) with { IsRevealed = true };

        if (opts.SeedSearchStringFromSelection == SeedSearchStringMode.Single)
        {
            var selectionSearchString = GetSelectionSearchString(Editor, opts.SeedSearchStringFromSelection, opts.SeedSearchStringFromNonEmptySelection);
            if (!string.IsNullOrEmpty(selectionSearchString))
            {
                stateChanges = stateChanges with
                {
                    SearchString = State.IsRegex ? TKCode.Arc.Editor.Model.SearchHelpers.EscapeRegExpCharacters(selectionSearchString) : selectionSearchString,
                };
            }
        }
        else if (opts.SeedSearchStringFromSelection == SeedSearchStringMode.Multiple && !opts.UpdateSearchScope)
        {
            var selectionSearchString = GetSelectionSearchString(Editor, opts.SeedSearchStringFromSelection);
            if (!string.IsNullOrEmpty(selectionSearchString))
            {
                stateChanges = stateChanges with { SearchString = selectionSearchString };
            }
        }

        if (string.IsNullOrEmpty(stateChanges.SearchString) && opts.SeedSearchStringFromGlobalClipboard)
        {
            var selectionSearchString = await GetGlobalBufferTerm();

            if (Editor.GetModel() is null)
            {
                // the editor has lost its model in the meantime
                return;
            }

            if (!string.IsNullOrEmpty(selectionSearchString))
            {
                stateChanges = stateChanges with { SearchString = selectionSearchString };
            }
        }

        // Overwrite isReplaceRevealed
        if (opts.ForceRevealReplace || stateChanges.IsReplaceRevealed == true)
        {
            stateChanges = stateChanges with { IsReplaceRevealed = true };
        }
        else if (!_findWidgetVisible.Get())
        {
            stateChanges = stateChanges with { IsReplaceRevealed = false };
        }

        if (opts.UpdateSearchScope)
        {
            var currentSelections = Editor.GetSelections();
            if (currentSelections.Any(static selection => !selection.IsEmpty))
            {
                stateChanges = stateChanges with { SearchScope = currentSelections.Select(static s => (TextRange)s).ToList() };
            }
        }

        stateChanges = stateChanges with { Loop = opts.Loop };

        State.Change(stateChanges, false);

        _model ??= new FindModelBoundToEditorModel(Editor, State);
    }

    /// <summary>Reveals find (upstream's start).</summary>
    public Task Start(FindStartOptions opts, NewFindReplaceState? newState = null) => StartCore(opts, newState);

    public bool MoveToNextMatch()
    {
        if (_model is not null)
        {
            _model.MoveToNextMatch();
            return true;
        }

        return false;
    }

    public bool MoveToPrevMatch()
    {
        if (_model is not null)
        {
            _model.MoveToPrevMatch();
            return true;
        }

        return false;
    }

    public bool GoToMatch(int index)
    {
        if (_model is not null)
        {
            _model.MoveToMatch(index);
            return true;
        }

        return false;
    }

    public bool Replace()
    {
        if (_model is not null)
        {
            _model.Replace();
            return true;
        }

        return false;
    }

    public bool ReplaceAll()
    {
        if (_model is not null)
        {
            if (Editor.GetModel()?.IsTooLargeForHeapOperation() == true)
            {
                // upstream warns: "The file is too large to perform a replace all operation."
                return false;
            }

            _model.ReplaceAll();
            return true;
        }

        return false;
    }

    public bool SelectAllMatches()
    {
        if (_model is not null)
        {
            _model.SelectAllMatches();
            Editor.Focus();
            return true;
        }

        return false;
    }

    public async Task<string> GetGlobalBufferTerm()
    {
        if (Editor.FindOptions.GlobalFindClipboard
            && Editor.GetModel() is { } model
            && !model.IsTooLargeForSyncing()
            && Editor.Clipboard is { } clipboard)
        {
            return await clipboard.ReadFindText();
        }

        return string.Empty;
    }

    public void SetGlobalBufferTerm(string text)
    {
        if (Editor.FindOptions.GlobalFindClipboard
            && Editor.GetModel() is { } model
            && !model.IsTooLargeForSyncing())
        {
            // intentionally not awaited
            Editor.Clipboard?.WriteFindText(text);
        }
    }

    /// <summary>
    /// The search string the selection suggests: the selected text on one line (or several lines for
    /// <see cref="SeedSearchStringMode.Multiple"/>), else the word at the cursor; null for none (upstream's getSelectionSearchString).
    /// </summary>
    public static string? GetSelectionSearchString(IFindEditor editor, SeedSearchStringMode seedSearchStringFromSelection = SeedSearchStringMode.Single, bool seedSearchStringFromNonEmptySelection = false)
    {
        if (editor.GetModel() is not { } model)
        {
            return null;
        }

        var selection = editor.GetSelection();
        // if selection spans multiple lines, default search string to empty

        if ((seedSearchStringFromSelection == SeedSearchStringMode.Single && selection.StartLineNumber == selection.EndLineNumber)
            || seedSearchStringFromSelection == SeedSearchStringMode.Multiple)
        {
            if (selection.IsEmpty)
            {
                var wordAtPosition = editor.GetConfiguredWordAtPosition(selection.Range.Start);
                if (wordAtPosition is not null && !seedSearchStringFromNonEmptySelection)
                {
                    return wordAtPosition.Word;
                }
            }
            else
            {
                if (model.GetValueLengthInRange(selection) < SearchStringMaxLength)
                {
                    return model.GetValueInRange(selection);
                }
            }
        }

        return null;
    }
}

/// <summary>
/// The bodies of the find actions (upstream's StartFindAction, StartFindWithArgsAction, StartFindWithSelectionAction,
/// matchFindAction, SelectionMatchFindAction and StartFindReplaceAction implementations), on a controller.
/// </summary>
internal static class FindActions
{
    private static SeedSearchStringMode SeedIfEmpty(CommonFindController controller) =>
        controller.GetState().SearchString.Length == 0 && controller.Editor.FindOptions.SeedSearchStringFromSelection != SeedSearchStringFromSelection.Never
            ? SeedSearchStringMode.Single
            : SeedSearchStringMode.None;

    /// <summary>Ctrl+F (upstream's StartFindAction implementation).</summary>
    public static Task StartFind(CommonFindController controller)
    {
        var find = controller.Editor.FindOptions;
        return controller.Start(new FindStartOptions
        {
            ForceRevealReplace = false,
            SeedSearchStringFromSelection = find.SeedSearchStringFromSelection != SeedSearchStringFromSelection.Never ? SeedSearchStringMode.Single : SeedSearchStringMode.None,
            SeedSearchStringFromNonEmptySelection = find.SeedSearchStringFromSelection == SeedSearchStringFromSelection.Selection,
            SeedSearchStringFromGlobalClipboard = find.GlobalFindClipboard,
            ShouldFocus = FindStartFocusAction.FocusFindInput,
            ShouldAnimate = true,
            UpdateSearchScope = false,
            Loop = find.Loop,
        });
    }

    /// <summary>Upstream's StartFindWithArgsAction.run.</summary>
    public static async Task StartFindWithArgs(CommonFindController controller, FindStartArguments? args)
    {
        var find = controller.Editor.FindOptions;
        var newState = args is not null
            ? new NewFindReplaceState
            {
                SearchString = args.SearchString,
                ReplaceString = args.ReplaceString,
                IsReplaceRevealed = args.ReplaceString is not null,
                IsRegex = args.IsRegex,
                WholeWord = args.MatchWholeWord,
                MatchCase = args.IsCaseSensitive,
                PreserveCase = args.PreserveCase,
            }
            : new NewFindReplaceState();

        await controller.Start(new FindStartOptions
        {
            ForceRevealReplace = false,
            SeedSearchStringFromSelection = args?.SearchString is null && controller.GetState().SearchString.Length == 0 && find.SeedSearchStringFromSelection != SeedSearchStringFromSelection.Never
                ? SeedSearchStringMode.Single
                : SeedSearchStringMode.None,
            SeedSearchStringFromNonEmptySelection = find.SeedSearchStringFromSelection == SeedSearchStringFromSelection.Selection,
            SeedSearchStringFromGlobalClipboard = args?.SearchString is null,
            ShouldFocus = FindStartFocusAction.FocusFindInput,
            ShouldAnimate = true,
            UpdateSearchScope = args?.FindInSelection ?? false,
            Loop = find.Loop,
        }, newState);

        controller.SetGlobalBufferTerm(controller.GetState().SearchString);
    }

    /// <summary>Cmd+E (upstream's StartFindWithSelectionAction.run).</summary>
    public static async Task StartFindWithSelection(CommonFindController controller)
    {
        await controller.Start(new FindStartOptions
        {
            ForceRevealReplace = false,
            SeedSearchStringFromSelection = SeedSearchStringMode.Multiple,
            SeedSearchStringFromNonEmptySelection = false,
            SeedSearchStringFromGlobalClipboard = false,
            ShouldFocus = FindStartFocusAction.NoFocusChange,
            ShouldAnimate = true,
            UpdateSearchScope = false,
            Loop = controller.Editor.FindOptions.Loop,
        });

        controller.SetGlobalBufferTerm(controller.GetState().SearchString);
    }

    /// <summary>F3 / Shift+F3 (upstream's matchFindAction).</summary>
    public static async Task MatchFind(CommonFindController controller, bool next)
    {
        var editor = controller.Editor;
        var find = editor.FindOptions;
        var shouldCloseOnResult = find.CloseOnResult;
        var wasFindWidgetVisible = controller.GetState().IsRevealed;

        bool RunMatch()
        {
            var previousSelection = editor.GetSelection();
            var result = next ? controller.MoveToNextMatch() : controller.MoveToPrevMatch();

            var landedOnMatch = false;
            if (result)
            {
                var currentSelection = editor.GetSelection();
                if (previousSelection != currentSelection)
                {
                    landedOnMatch = true;
                }
            }

            if (landedOnMatch)
            {
                editor.PushUndoStop();
                if (shouldCloseOnResult && wasFindWidgetVisible && controller.IsFindInputFocused())
                {
                    controller.CloseFindWidget();
                }

                return true;
            }

            return false;
        }

        if (!RunMatch())
        {
            await controller.Start(new FindStartOptions
            {
                ForceRevealReplace = false,
                SeedSearchStringFromSelection = SeedIfEmpty(controller),
                SeedSearchStringFromNonEmptySelection = find.SeedSearchStringFromSelection == SeedSearchStringFromSelection.Selection,
                SeedSearchStringFromGlobalClipboard = true,
                ShouldFocus = FindStartFocusAction.NoFocusChange,
                ShouldAnimate = true,
                UpdateSearchScope = false,
                Loop = find.Loop,
            });
            // upstream re-announces "no results" to screen readers here
            RunMatch();
        }
    }

    /// <summary>Ctrl+F3 / Ctrl+Shift+F3 (upstream's SelectionMatchFindAction.run).</summary>
    public static async Task SelectionMatchFind(CommonFindController controller, bool next)
    {
        var selectionSearchString = CommonFindController.GetSelectionSearchString(controller.Editor, SeedSearchStringMode.Single, false);
        if (!string.IsNullOrEmpty(selectionSearchString))
        {
            controller.SetSearchString(selectionSearchString);
        }
        if (!(next ? controller.MoveToNextMatch() : controller.MoveToPrevMatch()))
        {
            await controller.Start(new FindStartOptions
            {
                ForceRevealReplace = false,
                SeedSearchStringFromSelection = SeedSearchStringMode.None,
                SeedSearchStringFromNonEmptySelection = false,
                SeedSearchStringFromGlobalClipboard = false,
                ShouldFocus = FindStartFocusAction.NoFocusChange,
                ShouldAnimate = true,
                UpdateSearchScope = false,
                Loop = controller.Editor.FindOptions.Loop,
            });
            _ = next ? controller.MoveToNextMatch() : controller.MoveToPrevMatch();
        }
    }

    /// <summary>Ctrl+H (upstream's StartFindReplaceAction implementation); false when the editor is read-only.</summary>
    public static async Task<bool> StartFindReplace(CommonFindController controller)
    {
        var editor = controller.Editor;
        if (editor.GetModel() is null || editor.IsReadOnly)
        {
            return false;
        }

        var find = editor.FindOptions;
        var currentSelection = editor.GetSelection();
        var findInputFocused = controller.IsFindInputFocused();
        // we only seed search string from selection when the current selection is single line and not empty,
        // + the find input is not focused
        var seedSearchStringFromSelection = !currentSelection.IsEmpty
            && currentSelection.StartLineNumber == currentSelection.EndLineNumber
            && find.SeedSearchStringFromSelection != SeedSearchStringFromSelection.Never
            && !findInputFocused;
        // if the existing search string in find widget is empty and we don't seed search string from selection, it
        // means the Find Input is still empty, so we should focus the Find Input instead of Replace Input.
        var shouldFocus = findInputFocused || seedSearchStringFromSelection
            ? FindStartFocusAction.FocusReplaceInput
            : FindStartFocusAction.FocusFindInput;

        await controller.Start(new FindStartOptions
        {
            ForceRevealReplace = true,
            SeedSearchStringFromSelection = seedSearchStringFromSelection ? SeedSearchStringMode.Single : SeedSearchStringMode.None,
            SeedSearchStringFromNonEmptySelection = find.SeedSearchStringFromSelection == SeedSearchStringFromSelection.Selection,
            SeedSearchStringFromGlobalClipboard = find.SeedSearchStringFromSelection != SeedSearchStringFromSelection.Never,
            ShouldFocus = shouldFocus,
            ShouldAnimate = true,
            UpdateSearchScope = false,
            Loop = find.Loop,
        });
        return true;
    }
}
