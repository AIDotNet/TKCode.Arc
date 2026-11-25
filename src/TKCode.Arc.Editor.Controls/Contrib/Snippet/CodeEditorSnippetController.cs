// Ported from vscode (src/vs/editor/contrib/snippet/browser/snippetController2.ts: the editor contribution, its
// commands jumpToNextSnippetPlaceholder, jumpToPrevSnippetPlaceholder, leaveSnippet and acceptSnippet with their
// keybindings, and src/vs/editor/contrib/snippet/browser/snippetSession.css: the placeholder decorations; commit
// b57d5c9e), MIT license.
// Differences:
//  - the snippet logic is the engine's SnippetController2 over a CodeEditorSnippetEditor; this contribution owns it,
//    registers the placeholder styles and shows a choice placeholder's options;
//  - upstream shows the choices in the suggest widget (showSimpleSuggestions); here ShowSimpleSuggestions lets the
//    suggest feature take them, else the contribution shows its own SnippetChoiceList under the choice, driven by the
//    editor's keys (Up / Down / Page Up / Page Down / Home / End, Enter or Tab accepts, Escape closes it);
//  - the contribution is created eagerly (upstream: lazily), so that snippets inserted before the editor was shown
//    (CodeEditor.InsertSnippet) are inserted once it has a view;
//  - the placeholder styles are the theme colors SnippetTabstopHighlight (a background; upstream's
//    editor.snippetTabstopHighlightBackground) and SnippetFinalTabstopHighlightBorder (an outline of a non-empty final
//    tab stop); upstream's snippetTabstopHighlightBorder is transparent and not ported.

using Avalonia;
using Avalonia.Input;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Snippet;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.ViewModel;
using EditorKeyCode = TKCode.Arc.Editor.KeyCode;

namespace TKCode.Arc;

/// <summary>The snippet feature of a <see cref="CodeEditor"/> (upstream's SnippetController2 contribution).</summary>
internal sealed class CodeEditorSnippetController : ICodeEditorContribution, ISnippetInserter
{
    public const string Id = SnippetController2.ID;

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new CodeEditorSnippetController(editor), EditorContributionInstantiation.Eager);

    public static readonly EditorCommand JumpToNextSnippetPlaceholderCommand = EditorCommand.BindToContribution<CodeEditorSnippetController>(
        Id,
        "jumpToNextSnippetPlaceholder",
        ContextKeyExpr.And(SnippetController2.InSnippetMode, SnippetController2.HasNextTabstop),
        (controller, _) => controller.Next(),
        new EditorKeybinding((int)EditorKeyCode.Tab) { Weight = KeybindingWeight.EditorContrib + 30, When = EditorContextKeys.TextInputFocus });

    public static readonly EditorCommand JumpToPrevSnippetPlaceholderCommand = EditorCommand.BindToContribution<CodeEditorSnippetController>(
        Id,
        "jumpToPrevSnippetPlaceholder",
        ContextKeyExpr.And(SnippetController2.InSnippetMode, SnippetController2.HasPrevTabstop),
        (controller, _) => controller.Prev(),
        new EditorKeybinding(KeyMod.Shift | (int)EditorKeyCode.Tab) { Weight = KeybindingWeight.EditorContrib + 30, When = EditorContextKeys.TextInputFocus },
        // when a choice is active the suggest widget would claim Shift+Tab (#236489): this one wins
        new EditorKeybinding(KeyMod.Shift | (int)EditorKeyCode.Tab)
        {
            Weight = KeybindingWeight.EditorContrib + 91,
            When = ContextKeyExpr.And(SnippetController2.InSnippetChoice, EditorContextKeys.TextInputFocus),
        });

    public static readonly EditorCommand LeaveSnippetCommand = EditorCommand.BindToContribution<CodeEditorSnippetController>(
        Id,
        "leaveSnippet",
        SnippetController2.InSnippetMode,
        (controller, _) => controller.Cancel(true),
        new EditorKeybinding((int)EditorKeyCode.Escape)
        {
            Secondary = [KeyMod.Shift | (int)EditorKeyCode.Escape],
            Weight = KeybindingWeight.EditorContrib + 30,
            When = EditorContextKeys.TextInputFocus,
        });

    public static readonly EditorCommand AcceptSnippetCommand = EditorCommand.BindToContribution<CodeEditorSnippetController>(
        Id,
        "acceptSnippet",
        SnippetController2.InSnippetMode,
        (controller, _) => controller.Finish());

    private readonly CodeEditor _editor;
    private readonly CodeEditorSnippetEditor _snippetEditor;
    private readonly SnippetController2 _controller;
    private readonly IDisposable _placeholderStyle;
    private readonly IDisposable _finalPlaceholderStyle;
    private SnippetChoiceWidget? _choiceWidget;
    private bool _disposed;

    private CodeEditorSnippetController(CodeEditor editor)
    {
        _editor = editor;
        _snippetEditor = new CodeEditorSnippetEditor(editor, ShowChoices);
        _controller = new SnippetController2(_snippetEditor);
        _controller.IsInSnippetChanged += OnIsInSnippetChanged;
        _placeholderStyle = editor.DecorationStyles.Register(
            SnippetDecorationClassNames.Placeholder,
            new CodeEditorDecorationStyle { Background = CodeEditorThemeColor.SnippetTabstopHighlight });
        _finalPlaceholderStyle = editor.DecorationStyles.Register(
            SnippetDecorationClassNames.FinalPlaceholder,
            new CodeEditorDecorationStyle { Border = CodeEditorThemeColor.SnippetFinalTabstopHighlightBorder, BorderThickness = 1 });
        editor.EditorKeyDown += OnEditorKeyDown;
        editor.DidChangeCursorSelection += OnDidChangeCursorSelection;

        foreach (var (template, clipboardText) in editor.TakePendingSnippets())
        {
            Insert(template, new SnippetInsertOptions { ClipboardText = clipboardText });
        }
    }

    /// <summary>The editor's snippet controller, or null when it has none (or no view yet).</summary>
    public static CodeEditorSnippetController? Get(CodeEditor editor) => editor.GetContribution<CodeEditorSnippetController>(Id);

    /// <summary>The engine controller (tests).</summary>
    internal SnippetController2 Controller => _controller;

    /// <summary>The services the snippet variables read (file paths, workspace, clock, random numbers).</summary>
    internal SnippetVariableServices VariableServices
    {
        get => _snippetEditor.VariableServices;
        set => _snippetEditor.VariableServices = value;
    }

    /// <summary>The choice list, when it is showing (tests).</summary>
    internal SnippetChoiceList? VisibleChoiceList => _choiceWidget is { IsVisible: true } widget ? widget.List : null;

    public bool IsInSnippet => _controller.IsInSnippet();

    public Func<ICompletionItemProvider, bool>? ShowSimpleSuggestions { get; set; }

    public void Insert(string template, SnippetInsertOptions? options = null) => _controller.Insert(template, options);

    public void Apply(IReadOnlyList<SnippetEdit> edits, SnippetInsertOptions? options = null) => _controller.Apply(edits, options);

    public void Next() => _controller.Next();

    public void Prev() => _controller.Prev();

    public void Cancel(bool resetSelection = false) => _controller.Cancel(resetSelection);

    /// <summary>Moves through every remaining tab stop, ending snippet mode (the acceptSnippet command).</summary>
    public void Finish() => _controller.Finish();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        HideChoices();
        if (_choiceWidget is not null)
        {
            _editor.RemoveContentWidget(_choiceWidget);
            _choiceWidget.List.ItemInvoked -= OnChoiceInvoked;
            _choiceWidget = null;
        }

        _editor.EditorKeyDown -= OnEditorKeyDown;
        _editor.DidChangeCursorSelection -= OnDidChangeCursorSelection;
        _controller.IsInSnippetChanged -= OnIsInSnippetChanged;
        _controller.Dispose();
        _placeholderStyle.Dispose();
        _finalPlaceholderStyle.Dispose();
    }

    // ---- choices ----

    private void ShowChoices(ICompletionItemProvider provider)
    {
        if (_disposed)
        {
            return;
        }

        if (ShowSimpleSuggestions?.Invoke(provider) == true)
        {
            return;
        }

        if (_controller.ActiveChoice is not { } active || active.Choice.Options.Count == 0)
        {
            HideChoices();
            return;
        }

        if (_choiceWidget is null)
        {
            _choiceWidget = new SnippetChoiceWidget();
            _choiceWidget.List.ItemInvoked += OnChoiceInvoked;
            _editor.AddContentWidget(_choiceWidget);
        }

        var options = active.Choice.Options.Select(static o => o.Value).ToArray();
        var current = _editor.Model.GetValueInRange(active.Range);
        var list = _choiceWidget.List;
        list.Items = options;

        // the list never leaves the editor: below the choice when it fits there (or there is more room), else above,
        // with as many options as fit
        var above = false;
        list.AvailableHeight = double.PositiveInfinity;
        if (_editor.GetScrolledVisiblePosition(active.Range.Start) is { } at)
        {
            var spaceBelow = _editor.GetLayoutSize().Height - (at.Top + at.Height);
            var spaceAbove = at.Top;
            above = spaceBelow < list.FullHeight && spaceAbove > spaceBelow;
            list.AvailableHeight = above ? spaceAbove : spaceBelow;
        }

        _choiceWidget.Show(active.Range.Start, Math.Max(0, Array.IndexOf(options, current)), above);
        _editor.LayoutContentWidget(_choiceWidget);
    }

    private void HideChoices()
    {
        if (_choiceWidget is { IsVisible: true })
        {
            _choiceWidget.Hide();
            _editor.LayoutContentWidget(_choiceWidget);
        }
    }

    private void AcceptChoice(int index)
    {
        if (_controller.ActiveChoice is not { } active || index < 0 || index >= active.Choice.Options.Count)
        {
            HideChoices();
            return;
        }

        var value = active.Choice.Options[index].Value;
        HideChoices();
        var end = active.Range.Start.Delta(0, value.Length);
        var lines = value.Split('\n');
        if (lines.Length > 1)
        {
            end = new TextPosition(active.Range.StartLineNumber + lines.Length - 1, lines[^1].Length + 1);
        }

        // upstream: the suggest widget inserts the completion item over the choice, then its command jumps on
        _editor.ExecuteEdits("snippet", [new SingleEditOperation(active.Range, value)], [new TextSelection(end.LineNumber, end.Column, end.LineNumber, end.Column)]);
        _controller.Next();
    }

    private void OnChoiceInvoked(int index) => AcceptChoice(index);

    private void OnIsInSnippetChanged(bool inSnippet)
    {
        if (!inSnippet)
        {
            HideChoices();
        }
    }

    private void OnDidChangeCursorSelection(CursorStateChangedEvent e)
    {
        if (_choiceWidget is not { IsVisible: true } widget)
        {
            return;
        }

        // the list belongs to the choice under the cursor: gone when the cursor left it
        if (_controller.ActiveChoice is not { } active || !active.Range.ContainsPosition(e.Selections[0].Position) || widget.Anchor != active.Range.Start)
        {
            HideChoices();
        }
    }

    private void OnEditorKeyDown(KeyEventArgs e)
    {
        if (_choiceWidget is not { IsVisible: true } widget || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        var list = widget.List;
        switch (e.Key)
        {
            case Key.Down:
                list.MoveSelection(1);
                break;
            case Key.Up:
                list.MoveSelection(-1);
                break;
            case Key.PageDown:
                list.MoveSelection(list.VisibleCount);
                break;
            case Key.PageUp:
                list.MoveSelection(-list.VisibleCount);
                break;
            case Key.Home:
                list.SelectedIndex = 0;
                break;
            case Key.End:
                list.SelectedIndex = list.Items.Count - 1;
                break;
            case Key.Enter:
            case Key.Tab:
                AcceptChoice(list.SelectedIndex);
                break;
            case Key.Escape:
                HideChoices();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>The content widget that shows the choice list under the choice placeholder.</summary>
    private sealed class SnippetChoiceWidget : ICodeEditorContentWidget
    {
        private TextPosition? _anchor;
        private bool _above;

        public SnippetChoiceWidget()
        {
            List = new SnippetChoiceList { IsVisible = false };
        }

        public string Id => "editor.contrib.snippetChoiceWidget";

        public SnippetChoiceList List { get; }

        public Avalonia.Controls.Control Control => List;

        public TextPosition? Anchor => _anchor;

        public bool IsVisible => _anchor is not null;

        public void Show(TextPosition anchor, int selectedIndex, bool above)
        {
            _anchor = anchor;
            _above = above;
            List.SelectedIndex = selectedIndex;
            List.IsVisible = true;
        }

        public void Hide()
        {
            _anchor = null;
            List.IsVisible = false;
        }

        public ContentWidgetPosition? GetPosition() => _anchor is { } anchor
            ? new ContentWidgetPosition(anchor, _above
                ? [ContentWidgetPositionPreference.Above, ContentWidgetPositionPreference.Below]
                : [ContentWidgetPositionPreference.Below, ContentWidgetPositionPreference.Above])
            : null;
    }
}
