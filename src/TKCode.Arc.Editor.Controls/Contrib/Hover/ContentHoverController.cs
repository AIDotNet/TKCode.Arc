// Ported from vscode (src/vs/editor/contrib/hover/browser/contentHoverController.ts, contentHoverWidgetWrapper.ts,
// hoverActions.ts and hoverContribution.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the controller and the widget wrapper are one class; its participants are the marker and Markdown participants
//    (upstream's HoverParticipantRegistry also has the color, inlay hint and inline completion participants of other
//    features);
//  - the hover options are the controller's Options (upstream: EditorOption.hover); the timers run on the editor's
//    IdleScheduler (a test can give its own);
//  - no context menu service (the context menu does not suspend the hover), no color picker, no resizing sash, no
//    accessible view; the glyph margin hover (glyphHoverController.ts) is not ported;
//  - "mouse on the widget" is the widget's bounds in the editor's view coordinates; the widget lives in the widget host
//    over the view, so the view sees a pointer leave when the pointer moves onto the hover;
//  - links open through OpenLinkHandler when set, else command: links run the editor command (trusted Markdown only)
//    and other links go to the top level's launcher;
//  - content empty targets (the pointer just after the end of a line) do not start a hover.

using Avalonia.Controls;
using Avalonia.Input;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Hover;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.ViewModel;
using EditorKeyCode = TKCode.Arc.Editor.KeyCode;

namespace TKCode.Arc;

/// <summary>Shows hovers: on mouse rest over text after a delay, and from the keyboard (upstream's ContentHoverController).</summary>
internal sealed class ContentHoverController : ICodeEditorContribution
{
    public const string Id = "editor.contrib.contentHover";
    public const string ShowOrFocusHoverActionId = "editor.action.showHover";
    public const string HideHoverActionId = "editor.action.hideHover";
    public const string ScrollUpHoverActionId = "editor.action.scrollUpHover";
    public const string ScrollDownHoverActionId = "editor.action.scrollDownHover";
    public const string ScrollLeftHoverActionId = "editor.action.scrollLeftHover";
    public const string ScrollRightHoverActionId = "editor.action.scrollRightHover";
    public const string PageUpHoverActionId = "editor.action.pageUpHover";
    public const string PageDownHoverActionId = "editor.action.pageDownHover";
    public const string GoToTopHoverActionId = "editor.action.goToTopHover";
    public const string GoToBottomHoverActionId = "editor.action.goToBottomHover";
    public const string IncreaseHoverVerbosityActionId = "editor.action.increaseHoverVerbosityLevel";
    public const string DecreaseHoverVerbosityActionId = "editor.action.decreaseHoverVerbosityLevel";

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new ContentHoverController(editor), EditorContributionInstantiation.BeforeFirstInteraction);

    private readonly CodeEditor _editor;
    private readonly CodeEditorHoverEditor _hoverEditor;
    private readonly IReadOnlyList<IEditorHoverParticipant> _participants;
    private IdleScheduler? _scheduler;
    private HoverOperation<ContentHoverComputerOptions, IHoverPart>? _hoverOperation;
    private RunOnceScheduler? _reactToEditorMouseMoveRunner;
    private ContentHoverWidget? _contentWidget;
    private ContentHoverResult? _currentResult;
    private CodeEditorMouseEvent? _mouseMoveEvent;
    private HoverOptions _options = HoverOptions.Default;
    private bool _isMouseDown;
    private bool _disposed;

    private ContentHoverController(CodeEditor editor)
    {
        _editor = editor;
        _hoverEditor = new CodeEditorHoverEditor(editor, () => Options);
        _participants = new IEditorHoverParticipant[] { new MarkdownHoverParticipant(_hoverEditor), new MarkerHoverParticipant(_hoverEditor) }
            .OrderBy(p => p.HoverOrdinal)
            .ToList();

        editor.MouseDown += OnEditorMouseDown;
        editor.MouseUp += OnEditorMouseUp;
        editor.MouseMove += OnEditorMouseMove;
        editor.MouseLeave += OnEditorMouseLeave;
        editor.EditorKeyDown += OnKeyDown;
        editor.DidChangeModelContent += OnModelContentChanged;
        editor.DidScrollChange += OnEditorScrollChanged;
        editor.DidChangeModelLanguage += OnModelLanguageChanged;
    }

    /// <summary>The controller of <paramref name="editor"/>, or null.</summary>
    public static ContentHoverController? Get(CodeEditor editor) => editor.GetContribution<ContentHoverController>(Id);

    /// <summary>The hover options (upstream's editor.hover); changing them hides a visible hover when hovers are turned off.</summary>
    public HoverOptions Options
    {
        get => _options;
        set
        {
            _options = value;
            if (value.Enabled == HoverEnabled.Off)
            {
                CancelSchedulerAndHide();
            }
        }
    }

    /// <summary>
    /// Opens a link of a hover (the target; whether its Markdown is trusted); true when handled. Null: command links run
    /// editor commands, other links go to the top level's launcher.
    /// </summary>
    public Func<string, bool, bool>? OpenLinkHandler { get; set; }

    /// <summary>Upstream's shouldKeepOpenOnEditorMouseMoveOrLeave.</summary>
    public bool ShouldKeepOpenOnEditorMouseMoveOrLeave { get; set; }

    /// <summary>Whether the hover shows (upstream's isHoverVisible).</summary>
    public bool IsHoverVisible => _contentWidget?.IsVisible ?? false;

    /// <summary>The widget, once created.</summary>
    internal ContentHoverWidget? Widget => _contentWidget;

    /// <summary>The participants, in hover order.</summary>
    internal IReadOnlyList<IEditorHoverParticipant> Participants => _participants;

    /// <summary>Runs the hover timers on <paramref name="scheduler"/> (tests); before the first hover.</summary>
    internal void SetScheduler(IdleScheduler scheduler)
    {
        _scheduler = scheduler;
        _hoverOperation?.Dispose();
        _hoverOperation = null;
        _reactToEditorMouseMoveRunner?.Dispose();
        _reactToEditorMouseMoveRunner = null;
    }

    private IdleScheduler Scheduler => _scheduler ?? CodeEditor.IdleScheduler;

    private HoverOperation<ContentHoverComputerOptions, IHoverPart> HoverOperation
    {
        get
        {
            if (_hoverOperation is null)
            {
                _hoverOperation = new HoverOperation<ContentHoverComputerOptions, IHoverPart>(
                    Scheduler, new ContentHoverComputer(_hoverEditor, _participants), () => Options.Delay);
                _hoverOperation.OnResult += OnHoverResult;
            }

            return _hoverOperation;
        }
    }

    private RunOnceScheduler ReactToEditorMouseMoveRunner =>
        _reactToEditorMouseMoveRunner ??= new RunOnceScheduler(Scheduler, () =>
        {
            if (_mouseMoveEvent is { } e)
            {
                ReactToEditorMouseMove(e);
            }
        });

    // ---- editor events (contentHoverController.ts)

    private void CancelSchedulerAndHide()
    {
        CancelScheduler();
        HideContentHover();
    }

    private void CancelScheduler()
    {
        _mouseMoveEvent = null;
        _reactToEditorMouseMoveRunner?.Cancel();
    }

    private void OnModelContentChanged(TKCode.Arc.Editor.Model.ModelContentChangedEvent e) => CancelScheduler();

    private void OnModelLanguageChanged(TKCode.Arc.Editor.Model.ModelLanguageChangedEvent e) => CancelSchedulerAndHide();

    private void OnEditorScrollChanged(ScrollChangedEvent e)
    {
        if (e.ScrollTopChanged || e.ScrollLeftChanged)
        {
            HideContentHover();
        }
    }

    private void OnEditorMouseDown(CodeEditorMouseEvent mouseEvent)
    {
        _isMouseDown = true;
        if (ShouldKeepHoverWidgetVisible(mouseEvent))
        {
            return;
        }

        HideContentHover();
    }

    private bool ShouldKeepHoverWidgetVisible(CodeEditorMouseEvent mouseEvent) => IsMouseOnContentHoverWidget(mouseEvent);

    private bool IsMouseOnContentHoverWidget(CodeEditorMouseEvent mouseEvent) =>
        _contentWidget is { } widget && widget.ContainsPoint(mouseEvent.Target.Point);

    private void OnEditorMouseUp(CodeEditorMouseEvent mouseEvent) => _isMouseDown = false;

    private void OnEditorMouseLeave(PointerEventArgs e)
    {
        if (ShouldKeepOpenOnEditorMouseMoveOrLeave)
        {
            return;
        }

        CancelScheduler();
        if (_contentWidget is { } widget && _editor.WidgetHost is { } host && widget.ContainsNodePoint(e.GetPosition(host)))
        {
            // the pointer moved onto the hover (upstream: relatedTarget is in the widget)
            return;
        }

        HideContentHover();
    }

    private bool ShouldKeepCurrentHover(CodeEditorMouseEvent mouseEvent)
    {
        if (_contentWidget is not { } widget)
        {
            return false;
        }

        var isHoverSticky = Options.Sticky;
        var isMouseOnStickyContentHoverWidget = isHoverSticky && IsMouseOnContentHoverWidget(mouseEvent);
        var isStickyAndVisibleFromKeyboard = isHoverSticky && widget.IsVisible && widget.IsVisibleFromKeyboard;
        return ShouldKeepOpenOnEditorMouseMoveOrLeave
            || widget.IsFocused
            || isStickyAndVisibleFromKeyboard
            || isMouseOnStickyContentHoverWidget;
    }

    private void OnEditorMouseMove(CodeEditorMouseEvent mouseEvent)
    {
        // dragging to select text (the press started outside the hover) hides the hover and starts none
        if (_isMouseDown && !ShouldKeepHoverWidgetVisible(mouseEvent))
        {
            CancelSchedulerAndHide();
            return;
        }

        _mouseMoveEvent = mouseEvent;
        if (ShouldKeepCurrentHover(mouseEvent))
        {
            _reactToEditorMouseMoveRunner?.Cancel();
            return;
        }

        if (ShouldRescheduleHoverComputation())
        {
            if (!ReactToEditorMouseMoveRunner.IsScheduled())
            {
                ReactToEditorMouseMoveRunner.Schedule(Options.HidingDelay);
            }

            return;
        }

        ReactToEditorMouseMove(mouseEvent);
    }

    // a sticky hover gives the pointer a grace period before it reacts to it
    private bool ShouldRescheduleHoverComputation() =>
        (_contentWidget?.IsVisible ?? false) && Options.Sticky && Options.HidingDelay > 0;

    private void ReactToEditorMouseMove(CodeEditorMouseEvent mouseEvent)
    {
        var e = mouseEvent.Event;
        var keys = ToModifierKeys(e.KeyModifiers);
        if (HoverUtils.ShouldShowHover(Options.Enabled, Options.MultiCursorModifier, keys) && ShowsOrWillShow(mouseEvent))
        {
            return;
        }

        HideContentHover();
    }

    private static HoverModifierKeys ToModifierKeys(KeyModifiers modifiers) => new(
        modifiers.HasFlag(KeyModifiers.Control),
        modifiers.HasFlag(KeyModifiers.Alt),
        modifiers.HasFlag(KeyModifiers.Meta),
        modifiers.HasFlag(KeyModifiers.Shift));

    private void OnKeyDown(KeyEventArgs e)
    {
        if (_contentWidget is not { } widget)
        {
            return;
        }

        if (Options.Enabled == HoverEnabled.OnKeyboardModifier
            && HoverUtils.IsTriggerModifierPressed(Options.MultiCursorModifier, ToModifierKeys(e.KeyModifiers))
            && _mouseMoveEvent is { } mouseMoveEvent)
        {
            if (!widget.IsVisible)
            {
                ShowsOrWillShow(mouseMoveEvent);
            }

            return;
        }

        if (IsPotentialKeyboardShortcut(e) || CodeEditorKeys.IsModifier(e.Key))
        {
            return;
        }

        if (widget.IsFocused && e.Key == Key.Tab)
        {
            return;
        }

        HideContentHover();
    }

    private bool IsPotentialKeyboardShortcut(KeyEventArgs e)
    {
        if (_contentWidget is not { } widget || _editor.View is not { } view)
        {
            return false;
        }

        if (view.SoftDispatch(e.Key, e.KeyModifiers) is not { } resolved)
        {
            return false;
        }

        var moreChordsAreNeeded = resolved.Kind == ResultKind.MoreChordsNeeded;
        var isHoverAction = resolved.Kind == ResultKind.KbFound
            && resolved.CommandId is ShowOrFocusHoverActionId or IncreaseHoverVerbosityActionId or DecreaseHoverVerbosityActionId
            && widget.IsVisible;
        return moreChordsAreNeeded || isHoverAction;
    }

    /// <summary>Hides the hover (upstream's hideContentHover).</summary>
    public void HideContentHover()
    {
        _hoverOperation?.Cancel();
        SetCurrentResult(null);
    }

    /// <summary>Cancels pending and visible hover work (upstream's cancel).</summary>
    public void Cancel()
    {
        CancelScheduler();
        HideContentHover();
    }

    // ---- the widget wrapper (contentHoverWidgetWrapper.ts)

    private ContentHoverWidget GetOrCreateContentWidget()
    {
        if (_contentWidget is null)
        {
            _contentWidget = new ContentHoverWidget(_editor, () => Options);
            _contentWidget.HideRequested += HideContentHover;
        }

        return _contentWidget;
    }

    /// <summary>Upstream's showsOrWillShow: true when the hover shows now or will show for the pointer.</summary>
    private bool ShowsOrWillShow(CodeEditorMouseEvent mouseEvent)
    {
        var anchor = FindHoverAnchor(mouseEvent);
        return StartShowingOrUpdateHover(anchor, HoverStartMode.Delayed, HoverStartSource.Mouse, false, mouseEvent);
    }

    // upstream's _findHoverAnchorCandidates for the participants here (none suggests an anchor of its own)
    private static HoverAnchor? FindHoverAnchor(CodeEditorMouseEvent mouseEvent)
    {
        var target = mouseEvent.Target;
        if (target.Type == CodeEditorMouseTargetType.ContentText && target.Range is { } range)
        {
            return new HoverRangeAnchor(0, range, target.Point.X, target.Point.Y);
        }

        return null;
    }

    private bool StartShowingOrUpdateHover(HoverAnchor? anchor, HoverStartMode mode, HoverStartSource source, bool focus, CodeEditorMouseEvent? mouseEvent)
    {
        var widget = GetOrCreateContentWidget();
        var contentHoverIsVisible = widget.Position is not null && _currentResult is not null;
        if (!contentHoverIsVisible)
        {
            if (anchor is not null)
            {
                StartHoverOperationIfNecessary(anchor, mode, source, focus, false);
                return true;
            }

            return false;
        }

        // the pointer gets closer to a sticky hover: keep it, but compute the new anchor, insisting on keeping it visible
        var isMouseGettingCloser = mouseEvent is not null && widget.IsMouseGettingCloser(mouseEvent.Target.Point.X, mouseEvent.Target.Point.Y);
        if (Options.Sticky && isMouseGettingCloser)
        {
            if (anchor is not null)
            {
                StartHoverOperationIfNecessary(anchor, mode, source, focus, true);
            }

            return true;
        }

        if (anchor is null)
        {
            SetCurrentResult(null);
            return false;
        }

        if (_currentResult is { } current && current.Options.Anchor.Equals(anchor))
        {
            return true;
        }

        if (_currentResult is not { } result || widget.Position is not { } position || !anchor.CanAdoptVisibleHover(result.Options.Anchor, position))
        {
            SetCurrentResult(null);
            StartHoverOperationIfNecessary(anchor, mode, source, focus, false);
            return true;
        }

        // not getting closer: keep the parts that also apply to the new anchor
        SetCurrentResult(result.Filter(anchor));
        StartHoverOperationIfNecessary(anchor, mode, source, focus, false);
        return true;
    }

    private void StartHoverOperationIfNecessary(HoverAnchor anchor, HoverStartMode mode, HoverStartSource source, bool shouldFocus, bool insistOnKeepingHoverVisible)
    {
        var operation = HoverOperation;
        if (operation.Options is { } options && options.Anchor.Equals(anchor) && options.Source == source)
        {
            return;
        }

        operation.Cancel();
        operation.Start(mode, new ContentHoverComputerOptions(anchor, source, shouldFocus, insistOnKeepingHoverVisible));
    }

    private void OnHoverResult(HoverResult<ContentHoverComputerOptions, IHoverPart> result)
    {
        var messages = result.HasLoadingMessage ? AddLoadingMessage(result) : result.Value;
        WithResult(new ContentHoverResult(messages, result.IsComplete, result.Options));
    }

    private IReadOnlyList<IHoverPart> AddLoadingMessage(HoverResult<ContentHoverComputerOptions, IHoverPart> hoverResult)
    {
        foreach (var participant in _participants)
        {
            if (participant.CreateLoadingMessage(hoverResult.Options.Anchor) is { } loadingMessage)
            {
                return [.. hoverResult.Value, loadingMessage];
            }
        }

        return hoverResult.Value;
    }

    private void WithResult(ContentHoverResult hoverResult)
    {
        var previousHoverIsVisibleWithCompleteResult = _contentWidget?.Position is not null && _currentResult is { IsComplete: true };
        if (!previousHoverIsVisibleWithCompleteResult)
        {
            SetCurrentResult(hoverResult);
        }

        if (!hoverResult.IsComplete)
        {
            // instead of rendering the new partial result, wait for the result to be complete
            return;
        }

        if (hoverResult.HoverParts.Count == 0 && hoverResult.Options.InsistOnKeepingHoverVisible)
        {
            // the hover would hide now: keep the previous messages
            return;
        }

        SetCurrentResult(hoverResult);
    }

    private void SetCurrentResult(ContentHoverResult? hoverResult)
    {
        if (ReferenceEquals(_currentResult, hoverResult))
        {
            return;
        }

        if (hoverResult is { HoverParts.Count: 0 })
        {
            hoverResult = null;
        }

        _currentResult = hoverResult;
        if (hoverResult is not null)
        {
            ShowHover(hoverResult);
        }
        else
        {
            _contentWidget?.Hide();
        }
    }

    private void ShowHover(ContentHoverResult hoverResult)
    {
        var widget = GetOrCreateContentWidget();
        var context = new HoverRenderContext(
            HideContentHover,
            widget.HandleContentsChanged,
            widget.Focus,
            OpenLink,
            marker => MarkerController.Get(_editor)?.ShowAtMarker(marker));
        var rendered = new RenderedContentHover(_editor, hoverResult, _participants, widget.Surface, context);
        if (rendered.HasContent)
        {
            widget.Hide();
            widget.Show(rendered);
        }
        else
        {
            rendered.Dispose();
        }
    }

    /// <summary>Opens a hover link (see <see cref="OpenLinkHandler"/>).</summary>
    internal bool OpenLink(string target, bool isTrusted)
    {
        if (OpenLinkHandler is { } handler)
        {
            return handler(target, isTrusted);
        }

        if (target.StartsWith("command:", StringComparison.OrdinalIgnoreCase))
        {
            if (!isTrusted)
            {
                return false;
            }

            var commandId = target["command:".Length..];
            var query = commandId.IndexOf('?', StringComparison.Ordinal);
            if (query >= 0)
            {
                commandId = commandId[..query];
            }

            HideContentHover();
            _ = _editor.ExecuteCommand(Uri.UnescapeDataString(commandId));
            return true;
        }

        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && TopLevel.GetTopLevel(_editor) is { } topLevel)
        {
            _ = topLevel.Launcher.LaunchUriAsync(uri);
            return true;
        }

        return false;
    }

    /// <summary>Starts a hover at a range (upstream's showContentHover / startShowingAtRange).</summary>
    public void ShowContentHover(TextRange range, HoverStartMode mode, HoverStartSource source, bool focus) =>
        StartShowingOrUpdateHover(new HoverRangeAnchor(0, range, null, null), mode, source, focus, null);

    /// <summary>Moves the focus into the hover.</summary>
    public void Focus() => _contentWidget?.Focus();

    public void ScrollUp() => _contentWidget?.ScrollUp();

    public void ScrollDown() => _contentWidget?.ScrollDown();

    public void ScrollLeft() => _contentWidget?.ScrollLeft();

    public void ScrollRight() => _contentWidget?.ScrollRight();

    public void PageUp() => _contentWidget?.PageUp();

    public void PageDown() => _contentWidget?.PageDown();

    public void GoToTop() => _contentWidget?.GoToTop();

    public void GoToBottom() => _contentWidget?.GoToBottom();

    /// <summary>The text of the hover for a screen reader (upstream's getAccessibleWidgetContent).</summary>
    public string? GetAccessibleWidgetContent() => _contentWidget?.RenderedHover?.GetAccessibleWidgetContent();

    /// <summary>Changes the verbosity of the Markdown part at <paramref name="index"/> (upstream's updateHoverVerbosityLevel).</summary>
    public Task UpdateHoverVerbosityLevel(HoverVerbosityAction action, int index, bool focus = false) =>
        _contentWidget?.RenderedHover?.UpdateHoverVerbosityLevel(action, index, focus) ?? Task.CompletedTask;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _editor.MouseDown -= OnEditorMouseDown;
        _editor.MouseUp -= OnEditorMouseUp;
        _editor.MouseMove -= OnEditorMouseMove;
        _editor.MouseLeave -= OnEditorMouseLeave;
        _editor.EditorKeyDown -= OnKeyDown;
        _editor.DidChangeModelContent -= OnModelContentChanged;
        _editor.DidScrollChange -= OnEditorScrollChanged;
        _editor.DidChangeModelLanguage -= OnModelLanguageChanged;
        _reactToEditorMouseMoveRunner?.Dispose();
        _hoverOperation?.Dispose();
        if (_contentWidget is { } widget)
        {
            widget.HideRequested -= HideContentHover;
            widget.Dispose();
        }

        _currentResult = null;
    }

    // ---- actions (hoverActions.ts)

    /// <summary>
    /// Upstream's ShowOrFocusHoverAction: shows the hover at the cursor, or focuses a visible one. Args: a
    /// <see cref="ShowHoverFocus"/> (focusIfVisible by default).
    /// </summary>
    public static readonly EditorAction ShowOrFocusHoverAction = EditorAction.Create(
        ShowOrFocusHoverActionId, "Show or Focus Hover", null, (editor, args) =>
        {
            if (Get(editor) is not { } controller || editor.GetPosition() is not { } position)
            {
                return;
            }

            var focusOption = args as ShowHoverFocus? ?? ShowHoverFocus.FocusIfVisible;
            var range = new TextRange(position.LineNumber, position.Column, position.LineNumber, position.Column);
            if (controller.IsHoverVisible)
            {
                if (focusOption != ShowHoverFocus.NoAutoFocus)
                {
                    controller.Focus();
                }
                else
                {
                    controller.ShowContentHover(range, HoverStartMode.Immediate, HoverStartSource.Keyboard, false);
                }
            }
            else
            {
                controller.ShowContentHover(range, HoverStartMode.Immediate, HoverStartSource.Keyboard, focusOption == ShowHoverFocus.AutoFocusImmediately);
            }
        },
        new EditorKeybinding(KeyCodes.KeyChord(KeyMod.CtrlCmd | (int)EditorKeyCode.KeyK, KeyMod.CtrlCmd | (int)EditorKeyCode.KeyI))
        {
            When = EditorContextKeys.EditorTextFocus,
        });

    /// <summary>Upstream's HideContentHoverAction.</summary>
    public static readonly EditorAction HideHoverAction = EditorAction.Create(
        HideHoverActionId, "Hide Hover", null, (editor, _) => Get(editor)?.HideContentHover());

    public static readonly EditorAction ScrollUpHoverAction = FocusedHoverAction(ScrollUpHoverActionId, "Scroll Up Hover", EditorKeyCode.UpArrow, c => c.ScrollUp());

    public static readonly EditorAction ScrollDownHoverAction = FocusedHoverAction(ScrollDownHoverActionId, "Scroll Down Hover", EditorKeyCode.DownArrow, c => c.ScrollDown());

    public static readonly EditorAction ScrollLeftHoverAction = FocusedHoverAction(ScrollLeftHoverActionId, "Scroll Left Hover", EditorKeyCode.LeftArrow, c => c.ScrollLeft());

    public static readonly EditorAction ScrollRightHoverAction = FocusedHoverAction(ScrollRightHoverActionId, "Scroll Right Hover", EditorKeyCode.RightArrow, c => c.ScrollRight());

    public static readonly EditorAction PageUpHoverAction = FocusedHoverAction(PageUpHoverActionId, "Page Up Hover", EditorKeyCode.PageUp, c => c.PageUp(), KeyMod.Alt | (int)EditorKeyCode.PageUp);

    public static readonly EditorAction PageDownHoverAction = FocusedHoverAction(PageDownHoverActionId, "Page Down Hover", EditorKeyCode.PageDown, c => c.PageDown(), KeyMod.Alt | (int)EditorKeyCode.PageDown);

    public static readonly EditorAction GoToTopHoverAction = FocusedHoverAction(GoToTopHoverActionId, "Go To Top Hover", EditorKeyCode.Home, c => c.GoToTop(), KeyMod.CtrlCmd | (int)EditorKeyCode.Home);

    public static readonly EditorAction GoToBottomHoverAction = FocusedHoverAction(GoToBottomHoverActionId, "Go To Bottom Hover", EditorKeyCode.End, c => c.GoToBottom(), KeyMod.CtrlCmd | (int)EditorKeyCode.End);

    /// <summary>Upstream's IncreaseHoverVerbosityLevel (args: the part index, focused part by default 0).</summary>
    public static readonly EditorAction IncreaseHoverVerbosityAction = EditorAction.Create(
        IncreaseHoverVerbosityActionId, "Increase Hover Verbosity Level", EditorContextKeys.HoverVisible,
        (editor, args) => Get(editor)?.UpdateHoverVerbosityLevel(HoverVerbosityAction.Increase, args as int? ?? 0, true) ?? Task.CompletedTask);

    /// <summary>Upstream's DecreaseHoverVerbosityLevel.</summary>
    public static readonly EditorAction DecreaseHoverVerbosityAction = EditorAction.Create(
        DecreaseHoverVerbosityActionId, "Decrease Hover Verbosity Level", EditorContextKeys.HoverVisible,
        (editor, args) => Get(editor)?.UpdateHoverVerbosityLevel(HoverVerbosityAction.Decrease, args as int? ?? 0, true) ?? Task.CompletedTask);

    private static EditorAction FocusedHoverAction(string id, string label, EditorKeyCode key, Action<ContentHoverController> run, params int[] secondary) =>
        EditorAction.Create(id, label, EditorContextKeys.HoverFocused, (editor, _) =>
        {
            if (Get(editor) is { } controller)
            {
                run(controller);
            }
        },
        new EditorKeybinding((int)key) { When = EditorContextKeys.HoverFocused, Secondary = secondary.Length > 0 ? secondary : null });
}

/// <summary>The focus argument of <see cref="ContentHoverController.ShowOrFocusHoverAction"/> (upstream's HoverFocusBehavior).</summary>
internal enum ShowHoverFocus
{
    /// <summary>Show without focusing; a visible hover is not focused either.</summary>
    NoAutoFocus,

    /// <summary>Show without focusing; focus a hover that is visible already (the default).</summary>
    FocusIfVisible,

    /// <summary>Show and focus at once.</summary>
    AutoFocusImmediately,
}
