// Ported from vscode (commit b57d5c9e), MIT license, as a reduced port of
//  - src/vs/editor/contrib/inlineCompletions/browser/controller/inlineCompletionsController.ts (the model per editor,
//    stopping on explicit cursor moves, the context keys);
//  - src/vs/editor/contrib/inlineCompletions/browser/controller/commands.ts (commit / Tab, acceptNextWord /
//    Ctrl+Right, acceptNextLine, showNext / Alt+], showPrevious / Alt+[, hide / Escape, trigger);
//  - src/vs/editor/contrib/inlineCompletions/browser/controller/inlineCompletionContextKeys.ts (the keys used here).
//
// Differences: the model lives in the engine (InlineCompletionsModel) behind an IInlineCompletionsEditor; the view is
// GhostTextView (inline text as injected text, more lines in a view zone); no hints widget (the "1/3" toolbar), no
// inline edits, no suggest widget preview, no accessibility signals. Settings are properties of the controller
// (upstream: editor.inlineSuggest.enabled / mode).

using System.Text.RegularExpressions;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.InlineSuggestions;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.ViewModel;
using EditorKeyCode = TKCode.Arc.Editor.KeyCode;
using LanguageCommand = TKCode.Arc.Editor.Languages.LanguageCommand;

namespace TKCode.Arc;

/// <summary>Shows inline completions as ghost text and accepts them (upstream's InlineCompletionsController).</summary>
internal sealed class InlineCompletionsController : ICodeEditorContribution
{
    public const string Id = "editor.contrib.inlineCompletionsController";

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new InlineCompletionsController(editor), EditorContributionInstantiation.Eventually);

    /// <summary>Whether the inline suggestion starts with whitespace (upstream's inlineSuggestionHasIndentation).</summary>
    public static readonly RawContextKey<bool> InlineSuggestionHasIndentation = new("inlineSuggestionHasIndentation", false, "Whether the inline suggestion starts with whitespace");

    /// <summary>Whether the suggestion starts with less whitespace than a tab inserts.</summary>
    public static readonly RawContextKey<bool> InlineSuggestionHasIndentationLessThanTabSize = new("inlineSuggestionHasIndentationLessThanTabSize", true, "Whether the inline suggestion starts with whitespace that is less than what would be inserted by tab");

    /// <summary>Whether the cursor is at the ghost text.</summary>
    public static readonly RawContextKey<bool> CursorBeforeGhostText = new("cursorBeforeGhostText", false, "Whether the cursor is at ghost text");

    private readonly CodeEditor _editor;
    private readonly IContextKey<bool> _inlineSuggestionVisible;
    private readonly IContextKey<bool> _hasIndentation;
    private readonly IContextKey<bool> _hasIndentationLessThanTabSize;
    private readonly IContextKey<bool> _cursorBeforeGhostText;
    private readonly GhostTextView _view;
    private bool _accepting;

    private InlineCompletionsController(CodeEditor editor)
    {
        _editor = editor;
        _inlineSuggestionVisible = editor.CreateContextKey(CodeEditorContextKeys.InlineSuggestionVisible);
        _hasIndentation = editor.CreateContextKey(InlineSuggestionHasIndentation);
        _hasIndentationLessThanTabSize = editor.CreateContextKey(InlineSuggestionHasIndentationLessThanTabSize);
        _cursorBeforeGhostText = editor.CreateContextKey(CursorBeforeGhostText);
        Model = new InlineCompletionsModel(new EditorHost(editor), editor.LanguageFeatures.InlineCompletionsProvider, CodeEditor.IdleScheduler);
        _view = new GhostTextView(editor);
        Model.DidChangeState += OnStateChanged;
        editor.DidChangeCursorSelection += OnCursorChanged;
    }

    public static InlineCompletionsController? Get(CodeEditor editor) => editor.GetContribution<InlineCompletionsController>(Id);

    /// <summary>The model: providers, items, the ghost text state.</summary>
    public InlineCompletionsModel Model { get; }

    /// <summary>The view of the ghost text (for tests).</summary>
    internal GhostTextView View => _view;

    private void OnCursorChanged(CursorStateChangedEvent e)
    {
        if (!_accepting && (e.Reason == CursorChangeReason.Explicit || e.Source == "api"))
        {
            Model.Stop();
        }

        Model.HandleCursorChanged();
        UpdateContextKeys();
    }

    private void OnStateChanged()
    {
        _view.Update(Model.State);
        UpdateContextKeys();
    }

    private void UpdateContextKeys()
    {
        var state = Model.State;
        var ghostText = state?.PrimaryGhostText;
        _inlineSuggestionVisible.Set(ghostText is not null && !ghostText.IsEmpty());

        var (startsWithIndentation, lessThanTabSize) = GetIndentationInfo(ghostText);
        _hasIndentation.Set(startsWithIndentation);
        _hasIndentationLessThanTabSize.Set(lessThanTabSize);

        var cursorBefore = false;
        if (ghostText is { } g && !g.IsEmpty() && _editor.GetPosition() is { } cursor)
        {
            cursorBefore = cursor == new TextPosition(g.LineNumber, g.Parts[0].Column);
        }

        _cursorBeforeGhostText.Set(cursorBefore);
    }

    // upstream's InlineCompletionsModel.getIndentationInfo
    private (bool StartsWithIndentation, bool LessThanTabSize) GetIndentationInfo(GhostText? ghostText)
    {
        var startsWithIndentation = false;
        var startsWithIndentationLessThanTabSize = true;
        if (ghostText is { Parts.Count: > 0 })
        {
            var part = ghostText.Parts[0];
            var firstLine = part.Lines[0];
            var model = _editor.Model;
            var indentationEndColumn = model.GetLineIndentColumn(ghostText.LineNumber);
            var inIndentation = part.Column <= indentationEndColumn;
            if (inIndentation)
            {
                var firstNonWsIdx = Strings.FirstNonWhitespaceIndex(firstLine);
                if (firstNonWsIdx == -1)
                {
                    firstNonWsIdx = firstLine.Length - 1;
                }

                startsWithIndentation = firstNonWsIdx > 0;
                var tabSize = model.GetOptions().TabSize;
                var visibleColumnIndentation = CursorColumns.VisibleColumnFromColumn(firstLine, firstNonWsIdx + 1, tabSize);
                startsWithIndentationLessThanTabSize = visibleColumnIndentation < tabSize;
            }
        }

        return (startsWithIndentation, startsWithIndentationLessThanTabSize);
    }

    /// <summary>Accepts the ghost text (Tab).</summary>
    public void Commit() => WhileAccepting(Model.Accept);

    /// <summary>Accepts the next word of the ghost text (Ctrl+Right).</summary>
    public void AcceptNextWord() => WhileAccepting(Model.AcceptNextWord);

    /// <summary>Accepts the next line of the ghost text.</summary>
    public void AcceptNextLine() => WhileAccepting(Model.AcceptNextLine);

    private void WhileAccepting(Action action)
    {
        _accepting = true;
        try
        {
            action();
        }
        finally
        {
            _accepting = false;
        }

        UpdateContextKeys();
    }

    /// <summary>Hides the ghost text (Escape).</summary>
    public void Hide() => Model.Stop(explicitCancel: true);

    public void Dispose()
    {
        _editor.DidChangeCursorSelection -= OnCursorChanged;
        Model.DidChangeState -= OnStateChanged;
        Model.Dispose();
        _view.Dispose();
        _inlineSuggestionVisible.Reset();
        _cursorBeforeGhostText.Reset();
    }

    // ---- commands (upstream's commands.ts)

    public static readonly EditorAction TriggerInlineSuggestionAction = EditorAction.Create(
        "editor.action.inlineSuggest.trigger", "Trigger Inline Suggestion", EditorContextKeys.Writable,
        (editor, _) => Get(editor)?.Model.TriggerExplicitly() ?? Task.CompletedTask);

    public static readonly EditorAction ShowNextInlineSuggestionAction = EditorAction.Create(
        "editor.action.inlineSuggest.showNext", "Show Next Inline Suggestion",
        ContextKeyExpr.And(EditorContextKeys.Writable, CodeEditorContextKeys.InlineSuggestionVisible),
        (editor, _) => Get(editor)?.Model.Next() ?? Task.CompletedTask,
        new EditorKeybinding(KeyMod.Alt | (int)EditorKeyCode.BracketRight) { Weight = 100 });

    public static readonly EditorAction ShowPreviousInlineSuggestionAction = EditorAction.Create(
        "editor.action.inlineSuggest.showPrevious", "Show Previous Inline Suggestion",
        ContextKeyExpr.And(EditorContextKeys.Writable, CodeEditorContextKeys.InlineSuggestionVisible),
        (editor, _) => Get(editor)?.Model.Previous() ?? Task.CompletedTask,
        new EditorKeybinding(KeyMod.Alt | (int)EditorKeyCode.BracketLeft) { Weight = 100 });

    public static readonly EditorAction AcceptNextWordAction = EditorAction.Create(
        "editor.action.inlineSuggest.acceptNextWord", "Accept Next Word Of Inline Suggestion",
        ContextKeyExpr.And(EditorContextKeys.Writable, CodeEditorContextKeys.InlineSuggestionVisible),
        (editor, _) => Get(editor)?.AcceptNextWord(),
        new EditorKeybinding(KeyMod.CtrlCmd | (int)EditorKeyCode.RightArrow)
        {
            Weight = KeybindingWeight.EditorContrib + 1,
            When = ContextKeyExpr.And(EditorContextKeys.Writable, CodeEditorContextKeys.InlineSuggestionVisible, CursorBeforeGhostText),
        });

    public static readonly EditorAction AcceptNextLineAction = EditorAction.Create(
        "editor.action.inlineSuggest.acceptNextLine", "Accept Next Line Of Inline Suggestion",
        ContextKeyExpr.And(EditorContextKeys.Writable, CodeEditorContextKeys.InlineSuggestionVisible),
        (editor, _) => Get(editor)?.AcceptNextLine());

    public static readonly EditorAction AcceptInlineCompletionAction = EditorAction.Create(
        "editor.action.inlineSuggest.commit", "Accept Inline Suggestion",
        CodeEditorContextKeys.InlineSuggestionVisible,
        (editor, _) => Get(editor)?.Commit(),
        new EditorKeybinding((int)EditorKeyCode.Tab)
        {
            Weight = 200,
            When = ContextKeyExpr.And(
                CodeEditorContextKeys.InlineSuggestionVisible,
                EditorContextKeys.TabMovesFocus.ToNegated(),
                CodeEditorContextKeys.SuggestWidgetVisible.ToNegated(),
                EditorContextKeys.HoverFocused.ToNegated(),
                EditorContextKeys.HasNonEmptySelection.ToNegated(),
                InlineSuggestionHasIndentationLessThanTabSize),
        });

    public static readonly EditorAction HideInlineCompletionAction = EditorAction.Create(
        "editor.action.inlineSuggest.hide", "Hide Inline Suggestion",
        CodeEditorContextKeys.InlineSuggestionVisible,
        (editor, _) => Get(editor)?.Hide(),
        new EditorKeybinding((int)EditorKeyCode.Escape)
        {
            Weight = KeybindingWeight.EditorContrib + 90,
            When = EditorContextKeys.EditorTextFocus,
        });

    /// <summary>The editor as the engine model sees it.</summary>
    private sealed class EditorHost(CodeEditor editor) : IInlineCompletionsEditor
    {
        public TextModel Model => editor.Model;

        public IReadOnlyList<TextSelection> GetSelections() => editor.GetSelections();

        public void PushUndoStop() => editor.PushUndoStop();

        public bool ExecuteEdits(string source, IReadOnlyList<SingleEditOperation> edits, IReadOnlyList<TextSelection>? endCursorState) =>
            editor.ExecuteEdits(source, edits, endCursorState);

        public Regex GetWordDefinition(TextPosition position) =>
            editor.LanguageConfigurationService.GetLanguageConfiguration(editor.Model.GetLanguageIdAtPosition(position.LineNumber, position.Column)).GetWordDefinition();

        public void ExecuteCommand(LanguageCommand command) =>
            _ = editor.ExecuteCommand(command.Id, command.Arguments is { Count: > 0 } args ? args[0] : null);
    }
}
