// Ported from vscode (src/vs/editor/contrib/codeAction/browser/codeActionController.ts, applyCodeAction of
// codeAction.ts, and the gutter part of lightBulbWidget.ts; commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the CodeActionModel is the engine's, on an adapter of the CodeEditor (ICodeActionEditor); the lightbulb option is
//    OnCode and editor.codeActionWidget.includeNearbyQuickFixes / showHeaders are true, upstream's defaults;
//  - the lightbulb is a glyph margin decoration (a lightbulb glyph in the warning color, the autofix one in the info
//    color) on the line of the request; the controller shows the glyph margin while the model has code action
//    providers; a press on the glyph shows the menu. Upstream also draws it in the text when there is no glyph margin;
//  - applying an action goes through CodeEditor.ApplyWorkspaceEdit (this model's edits as one undo step, the rest to the
//    host) and CodeEditor.RunLanguageCommand; a failure shows as a message at the cursor (upstream: a notification);
//  - no telemetry, progress, accessibility signals, AI lightbulb flows or the lightbulb's hover and its observable state.

using Avalonia.Media;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.CodeAction;
using TKCode.Arc.Editor.ViewModel;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc;

/// <summary>Shows code actions: the lightbulb, the menu (Ctrl+.), and applies the chosen action (upstream's CodeActionController).</summary>
internal sealed partial class CodeActionController : ICodeEditorContribution
{
    public const string Id = "editor.contrib.codeActionController";

    /// <summary>The class names of the lightbulb and of the ranges an action fixes.</summary>
    public const string LightBulbClassName = "codicon-light-bulb";
    public const string LightBulbAutoFixClassName = "codicon-lightbulb-autofix";
    public const string QuickFixHighlightClassName = "quickfix-edit-highlight";

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new CodeActionController(editor), EditorContributionInstantiation.Eventually);

    private static readonly Geometry s_lightBulb = Geometry.Parse("M8,1.5 C5.2,1.5 3.5,3.6 3.5,6 C3.5,7.7 4.4,8.8 5.3,9.7 C5.8,10.2 6,10.7 6,11.3 L6,12 L10,12 L10,11.3 C10,10.7 10.2,10.2 10.7,9.7 C11.6,8.8 12.5,7.7 12.5,6 C12.5,3.6 10.8,1.5 8,1.5 Z M6,13 L10,13 L10,13.6 C10,14.2 9.5,14.5 9,14.5 L7,14.5 C6.5,14.5 6,14.2 6,13.6 Z");

    private readonly CodeEditor _editor;
    private readonly EditorAdapter _adapter;
    private readonly CodeActionModel _model;
    private readonly CodeActionKeybindingResolver _resolver;
    private readonly IContextKey<bool> _menuVisible;
    private readonly List<IDisposable> _styles = new();
    private CodeActionSet? _activeCodeActions;
    private CodeActionMenuWidget? _menu;
    private string[] _highlightDecorations = [];
    private string[] _lightBulbDecorations = [];
    private (CodeActionSet Actions, TextPosition Position)? _lightBulb;
    private bool _showsGlyphMargin;
    private bool _disposed;

    private CodeActionController(CodeEditor editor)
    {
        _editor = editor;
        _adapter = new EditorAdapter(editor);
        _menuVisible = editor.CreateContextKey(CodeEditorContextKeys.CodeActionMenuVisible);
        _styles.Add(editor.DecorationStyles.Register(LightBulbClassName, new CodeEditorDecorationStyle { Glyph = s_lightBulb, GlyphBrush = CodeEditorThemeColor.Warning }));
        _styles.Add(editor.DecorationStyles.Register(LightBulbAutoFixClassName, new CodeEditorDecorationStyle { Glyph = s_lightBulb, GlyphBrush = CodeEditorThemeColor.Info }));
        _styles.Add(editor.DecorationStyles.Register(QuickFixHighlightClassName, new CodeEditorDecorationStyle { Background = CodeEditorThemeColor.FindMatchHighlight }));
        _resolver = new CodeActionKeybindingResolver(() => editor.View?.Resolver.GetKeybindings() ?? []);

        _model = new CodeActionModel(_adapter, editor.LanguageFeatures.CodeActionProvider, editor.MarkerService, editor.CreateContextKey(CodeActionModel.SupportedCodeActions));
        _model.DidChangeState += OnStateChanged;
        editor.LanguageFeatures.CodeActionProvider.DidChange += OnProvidersChanged;
        editor.DidChangeModelLanguage += OnLanguageChanged;
        editor.MouseDown += OnMouseDown;
        editor.DidLayoutChange += OnLayoutChanged;
        editor.DidChangeModelContent += OnContentChanged;
        editor.DidChangeTextFocus += OnTextFocusChanged;
        UpdateGlyphMargin();
        if (_model.State is CodeActionsState.Triggered triggered)
        {
            // the model asked at once (upstream subscribes before its first trigger)
            OnStateChanged(triggered);
        }
    }

    public static CodeActionController? Get(CodeEditor editor) => editor.GetContribution<CodeActionController>(Id);

    /// <summary>The menu that shows, or null.</summary>
    internal CodeActionMenuWidget? Menu => _menu;

    /// <summary>The line of the lightbulb, or null when it does not show.</summary>
    internal int? LightBulbLine => _lightBulb?.Position.LineNumber;

    /// <summary>The code action model (for tests).</summary>
    internal CodeActionModel Model => _model;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        HideCodeActions();
        HideLightBulbWidget();
        _model.DidChangeState -= OnStateChanged;
        _model.Dispose();
        _editor.LanguageFeatures.CodeActionProvider.DidChange -= OnProvidersChanged;
        _editor.DidChangeModelLanguage -= OnLanguageChanged;
        _editor.MouseDown -= OnMouseDown;
        _editor.DidLayoutChange -= OnLayoutChanged;
        _editor.DidChangeModelContent -= OnContentChanged;
        _editor.DidChangeTextFocus -= OnTextFocusChanged;
        _adapter.Dispose();
        _activeCodeActions?.Dispose();
        _activeCodeActions = null;
        if (_showsGlyphMargin)
        {
            _editor.Gutter.GlyphMargin.IsVisible = false;
        }

        foreach (var style in _styles)
        {
            style.Dispose();
        }
    }

    /// <summary>Asks for code actions at the cursor (upstream's manualTriggerAtCurrentPosition).</summary>
    public void ManualTriggerAtCurrentPosition(string notAvailableMessage, CodeActionTriggerSource triggerAction, CodeActionFilter? filter = null, CodeActionAutoApply? autoApply = null)
    {
        if (_editor.GetPosition() is not { } triggerPosition)
        {
            return;
        }

        MessageController.Get(_editor)?.CloseMessage();
        _model.Trigger(new CodeActionTrigger(CodeActionTriggerType.Invoke, triggerAction)
        {
            Filter = filter,
            AutoApply = autoApply,
            Context = new CodeActionTriggerContext(notAvailableMessage, triggerPosition),
        });
    }

    /// <summary>Hides the menu (upstream's hideCodeActions).</summary>
    public void HideCodeActions()
    {
        if (_menu is not { } menu)
        {
            return;
        }

        _menu = null;
        _menuVisible.Reset();
        _editor.RemoveContentWidget(menu);
        ClearHighlights();
    }

    /// <summary>Resolves and applies an action: its edit, then its command (upstream's applyCodeAction).</summary>
    public async Task ApplyCodeAction(CodeActionItem action, bool retrigger, bool preview)
    {
        try
        {
            await action.Resolve(CancellationToken.None).ConfigureAwait(true);
            if (action.Action.Edit is { Edits.Count: > 0 } edit)
            {
                var (applied, error) = await _editor.ApplyWorkspaceEdit(edit, action.Action.Title).ConfigureAwait(true);
                if (!applied)
                {
                    if (error is not null && _editor.GetPosition() is { } position)
                    {
                        MessageController.Get(_editor)?.ShowMessage(error, position);
                    }

                    return;
                }
            }

            if (action.Action.Command is { } command)
            {
                try
                {
                    await _editor.RunLanguageCommand(command).ConfigureAwait(true);
                }
                catch (Exception error)
                {
                    if (_editor.GetPosition() is { } position)
                    {
                        MessageController.Get(_editor)?.ShowMessage(
                            string.IsNullOrEmpty(error.Message) ? "An unknown error occurred while applying the code action" : error.Message, position);
                    }
                }
            }
        }
        finally
        {
            if (retrigger && !_disposed)
            {
                _model.Trigger(new CodeActionTrigger(CodeActionTriggerType.Auto, CodeActionTriggerSource.QuickFix) { Filter = new CodeActionFilter() });
            }
        }
    }

    /// <summary>Shows the menu of <paramref name="actions"/> below <paramref name="position"/> (upstream's showCodeActionList).</summary>
    public void ShowCodeActionList(CodeActionSet actions, TextPosition position, bool includeDisabledActions, bool fromLightbulb)
    {
        var actionsToShow = includeDisabledActions && actions.ValidActions.Count == 0 ? actions.AllActions : actions.ValidActions;
        if (actionsToShow.Count == 0)
        {
            return;
        }

        HideCodeActions();
        _editor.RevealRange(new TextRange(position), Editor.ViewModel.VerticalRevealType.Simple, Editor.ViewModel.ScrollType.Immediate);
        var items = CodeActionMenu.ToMenuItems(actionsToShow, showHeaders: true, _resolver.GetResolver());
        _menu = new CodeActionMenuWidget(position, items, item =>
        {
            HideCodeActions();
            _ = ApplyCodeAction(item, retrigger: true, preview: false);
            _editor.Focus();
        }, HighlightFixedRanges);
        _menuVisible.Set(true);
        _editor.AddContentWidget(_menu);
        HighlightFixedRanges(_menu.FocusedItem);
    }

    // the ranges or diagnostics the focused action fixes (upstream's onFocus of the action list delegate)
    private void HighlightFixedRanges(CodeActionItem? item)
    {
        var ranges = new List<TextRange>();
        if (item is { Action: var action })
        {
            if (action.Ranges is { Count: > 0 } actionRanges)
            {
                // fix all: several diagnostics
                ranges.AddRange(action.Diagnostics is { Count: > 1 } diagnostics
                    ? diagnostics.Select(static d => new TextRange(d.StartLineNumber, d.StartColumn, d.EndLineNumber, d.EndColumn))
                    : actionRanges);
            }
            else if (action.Diagnostics is { Count: > 0 } diagnostics)
            {
                ranges.AddRange(diagnostics.Select(static d => new TextRange(d.StartLineNumber, d.StartColumn, d.EndLineNumber, d.EndColumn)));
            }
        }

        _highlightDecorations = _editor.Model.DeltaDecorations(_highlightDecorations,
            [.. ranges.Select(range => new ModelDeltaDecoration(range, new ModelDecorationOptions("quickfix-highlight") { ClassName = QuickFixHighlightClassName }))]);
    }

    private void ClearHighlights()
    {
        if (_highlightDecorations.Length > 0)
        {
            _highlightDecorations = _editor.Model.DeltaDecorations(_highlightDecorations, []);
        }
    }

    // upstream's update(newState)
    private async void OnStateChanged(CodeActionsState newState)
    {
        if (newState is not CodeActionsState.Triggered triggered)
        {
            HideLightBulbWidget();
            return;
        }

        CodeActionSet? actions;
        try
        {
            actions = _model.TakeCodeActions(await triggered.Actions.ConfigureAwait(true));
        }
        catch (Exception error)
        {
            UnexpectedErrors.Report(error);
            return;
        }

        if (actions is null)
        {
            return;
        }

        if (_disposed)
        {
            actions.Dispose();
            return;
        }

        if (_editor.GetSelection() is not { } selection || selection.StartLineNumber != triggered.Position.LineNumber)
        {
            actions.Dispose();
            return;
        }

        UpdateLightBulb(actions, triggered.Trigger, triggered.Position);

        if (triggered.Trigger.Type == CodeActionTriggerType.Invoke)
        {
            if (triggered.Trigger.Filter?.Include is not null)
            {
                // triggered for a specific scope: apply it at once?
                if (TryGetValidActionToApply(triggered.Trigger, actions) is { } validActionToApply)
                {
                    try
                    {
                        HideLightBulbWidget();
                        await ApplyCodeAction(validActionToApply, retrigger: false, preview: false).ConfigureAwait(true);
                    }
                    finally
                    {
                        actions.Dispose();
                    }

                    return;
                }

                // an action that would have been applied were it not disabled
                if (triggered.Trigger.Context is { } context
                    && GetInvalidActionThatWouldHaveBeenApplied(triggered.Trigger, actions) is { Action.Disabled: { } disabled })
                {
                    MessageController.Get(_editor)?.ShowMessage(disabled, context.Position);
                    actions.Dispose();
                    return;
                }
            }

            var includeDisabledActions = triggered.Trigger.Filter?.Include is not null;
            if (triggered.Trigger.Context is { } notAvailable
                && (actions.AllActions.Count == 0 || (!includeDisabledActions && actions.ValidActions.Count == 0)))
            {
                MessageController.Get(_editor)?.ShowMessage(notAvailable.NotAvailableMessage, notAvailable.Position);
                SetActiveCodeActions(actions);
                return;
            }

            SetActiveCodeActions(actions);
            ShowCodeActionList(actions, triggered.Position, includeDisabledActions, fromLightbulb: false);
        }
        else if (_menu is not null)
        {
            // automatically triggered while the menu shows: the menu stays as it is
            actions.Dispose();
        }
        else
        {
            SetActiveCodeActions(actions);
        }
    }

    private void SetActiveCodeActions(CodeActionSet actions)
    {
        if (!ReferenceEquals(_activeCodeActions, actions))
        {
            _activeCodeActions?.Dispose();
        }

        _activeCodeActions = actions;
    }

    private static CodeActionItem? GetInvalidActionThatWouldHaveBeenApplied(CodeActionTrigger trigger, CodeActionSet actions)
    {
        if (actions.AllActions.Count == 0)
        {
            return null;
        }

        if ((trigger.AutoApply == CodeActionAutoApply.First && actions.ValidActions.Count == 0)
            || (trigger.AutoApply == CodeActionAutoApply.IfSingle && actions.AllActions.Count == 1))
        {
            return actions.AllActions.FirstOrDefault(static item => item.Action.Disabled is not null);
        }

        return null;
    }

    private static CodeActionItem? TryGetValidActionToApply(CodeActionTrigger trigger, CodeActionSet actions)
    {
        if (actions.ValidActions.Count == 0)
        {
            return null;
        }

        if ((trigger.AutoApply == CodeActionAutoApply.First && actions.ValidActions.Count > 0)
            || (trigger.AutoApply == CodeActionAutoApply.IfSingle && actions.ValidActions.Count == 1))
        {
            return actions.ValidActions[0];
        }

        return null;
    }

    // ---- the lightbulb (upstream's LightBulbWidget, in the glyph margin)

    private void UpdateLightBulb(CodeActionSet actions, CodeActionTrigger trigger, TextPosition atPosition)
    {
        if (actions.ValidActions.Count <= 0 || !_editor.IsTextFocused || !_showsGlyphMargin && !_editor.Gutter.GlyphMargin.IsVisible)
        {
            HideLightBulbWidget();
            return;
        }

        var position = _editor.Model.ValidatePosition(atPosition);
        _lightBulb = (actions, position);
        var className = actions.HasAutoFix ? LightBulbAutoFixClassName : LightBulbClassName;
        _lightBulbDecorations = _editor.Model.DeltaDecorations(_lightBulbDecorations,
        [
            new ModelDeltaDecoration(new TextRange(position.LineNumber, 1, position.LineNumber, 1), new ModelDecorationOptions("lightbulb") { GlyphMarginClassName = className }),
        ]);
    }

    /// <summary>Hides the lightbulb (upstream's hideLightBulbWidget).</summary>
    public void HideLightBulbWidget()
    {
        _lightBulb = null;
        if (_lightBulbDecorations.Length > 0 && !_editor.Model.IsDisposed())
        {
            _lightBulbDecorations = _editor.Model.DeltaDecorations(_lightBulbDecorations, []);
        }
    }

    private void OnMouseDown(CodeEditorMouseEvent e)
    {
        if (_menu is not null && e.Target.Type is not CodeEditorMouseTargetType.ContentWidget)
        {
            HideCodeActions();
        }

        if (e.Target.Type == CodeEditorMouseTargetType.GutterGlyphMargin
            && _lightBulb is { } lightBulb
            && e.Target.Position?.LineNumber == lightBulb.Position.LineNumber
            && e.Event is Avalonia.Input.PointerPressedEventArgs { } pressed
            && pressed.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
        {
            // upstream's lightbulb click: the menu of the lightbulb's actions, without disabled ones
            e.Handled = true;
            _editor.SetPosition(new TextPosition(lightBulb.Position.LineNumber, _editor.Model.GetLineFirstNonWhitespaceColumn(lightBulb.Position.LineNumber) is var c and > 0 ? c : 1));
            ShowCodeActionList(lightBulb.Actions, _editor.GetPosition() ?? lightBulb.Position, includeDisabledActions: false, fromLightbulb: true);
        }
    }

    private void OnLayoutChanged(Avalonia.Size size) => HideCodeActions();

    private void OnContentChanged(ModelContentChangedEvent e) => HideCodeActions();

    private void OnTextFocusChanged(bool focused)
    {
        if (!focused && _menu is not null && !_editor.IsKeyboardFocusWithin)
        {
            HideCodeActions();
        }
    }

    private void OnProvidersChanged(int count) => UpdateGlyphMargin();

    private void OnLanguageChanged(ModelLanguageChangedEvent e) => UpdateGlyphMargin();

    // the lightbulb needs the glyph margin: show it while the model has code action providers
    private void UpdateGlyphMargin()
    {
        var wanted = _editor.LanguageFeatures.CodeActionProvider.Has(_editor.Model);
        if (wanted == _showsGlyphMargin)
        {
            return;
        }

        _showsGlyphMargin = wanted;
        _editor.Gutter.GlyphMargin.IsVisible = wanted;
    }

    /// <summary>The CodeEditor as the code action model's editor.</summary>
    private sealed class EditorAdapter : ICodeActionEditor, IDisposable
    {
        private readonly CodeEditor _editor;

        public EditorAdapter(CodeEditor editor)
        {
            _editor = editor;
            _editor.DidChangeCursorSelection += OnCursor;
            _editor.DidChangeModelLanguage += OnLanguage;
            _editor.PropertyChanged += OnPropertyChanged;
        }

        public TextModel? Model => _editor.Model;

        public bool IsReadOnly => _editor.IsReadOnly;

        public ShowLightbulbIconMode Lightbulb => ShowLightbulbIconMode.OnCode;

        public bool IncludeNearbyQuickFixes => true;

        public IdleScheduler Scheduler => CodeEditor.IdleScheduler;

        public event Action? DidChangeCursorPosition;

        public event Action? DidChangeModel
        {
            add
            {
            }

            remove
            {
            }
        }

        public event Action? DidChangeModelLanguage;

        public event Action? DidChangeConfiguration;

        public TextSelection? GetSelection() => _editor.GetSelection();

        public void Dispose()
        {
            _editor.DidChangeCursorSelection -= OnCursor;
            _editor.DidChangeModelLanguage -= OnLanguage;
            _editor.PropertyChanged -= OnPropertyChanged;
        }

        private void OnCursor(CursorStateChangedEvent e) => DidChangeCursorPosition?.Invoke();

        private void OnLanguage(ModelLanguageChangedEvent e) => DidChangeModelLanguage?.Invoke();

        private void OnPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == CodeEditor.IsReadOnlyProperty)
            {
                DidChangeConfiguration?.Invoke();
            }
        }
    }
}
