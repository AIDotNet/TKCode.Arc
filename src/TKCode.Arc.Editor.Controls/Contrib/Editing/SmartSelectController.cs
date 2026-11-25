// Ported from vscode (src/vs/editor/contrib/smartSelect/browser/smartSelect.ts, commit b57d5c9e), MIT license:
// SmartSelectController's editor wiring and the GrowSelectionAction / ShrinkSelectionAction actions.
// Differences from upstream: the state and run are the engine's SmartSelectState; the controller forgets it when the
// cursor moves (upstream's onDidChangeCursorPosition listener) unless the move is its own; the command alias
// editor.action.smartSelect.grow is a command of its own.

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.SmartSelect;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

/// <summary>Expand / shrink selection (upstream's SmartSelectController).</summary>
internal sealed class SmartSelectController : ICodeEditorContribution
{
    public const string Id = "editor.contrib.smartSelectController";

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new SmartSelectController(editor), EditorContributionInstantiation.Lazy);

    private readonly CodeEditor _editor;
    private readonly SmartSelectState _state = new();
    private bool _listening;

    private SmartSelectController(CodeEditor editor)
    {
        _editor = editor;
    }

    public static SmartSelectController? Get(CodeEditor editor) => editor.GetContribution<SmartSelectController>(Id);

    /// <summary>Upstream's run: expand (<paramref name="forward"/>) or shrink the selections.</summary>
    public async Task Run(bool forward)
    {
        await _state.Run(_editor, forward);
        if (_state.HasState && !_listening)
        {
            // listen to caret move and forget about state
            _listening = true;
            _editor.DidChangeCursorSelection += OnCursorSelectionChanged;
        }
    }

    private void OnCursorSelectionChanged(CursorStateChangedEvent e)
    {
        if (!_state.IsSettingSelections)
        {
            StopListening();
            _state.Reset();
        }
    }

    private void StopListening()
    {
        if (_listening)
        {
            _listening = false;
            _editor.DidChangeCursorSelection -= OnCursorSelectionChanged;
        }
    }

    public void Dispose() => StopListening();

    private static EditorKeybinding Keybinding(int arrow) =>
        new(KeyMod.Shift | KeyMod.Alt | arrow)
        {
            When = EditorContextKeys.EditorTextFocus,
            Mac = new PlatformKeybindings
            {
                Primary = KeyMod.CtrlCmd | KeyMod.WinCtrl | KeyMod.Shift | arrow,
                Secondary = [KeyMod.WinCtrl | KeyMod.Shift | arrow],
            },
        };

    private static Task RunOn(CodeEditor editor, bool forward) => Get(editor)?.Run(forward) ?? Task.CompletedTask;

    /// <summary>editor.action.smartSelect.expand (Shift+Alt+Right).</summary>
    public static readonly EditorAction GrowSelectionAction = EditorAction.Create(
        "editor.action.smartSelect.expand", "Expand Selection", null,
        (editor, _) => RunOn(editor, true),
        Keybinding((int)KeyCode.RightArrow));

    /// <summary>The renamed command id editor.action.smartSelect.grow.</summary>
    public static readonly EditorCommand GrowSelectionAlias = EditorCommand.Create(
        "editor.action.smartSelect.grow", null, (editor, _) => RunOn(editor, true));

    /// <summary>editor.action.smartSelect.shrink (Shift+Alt+Left).</summary>
    public static readonly EditorAction ShrinkSelectionAction = EditorAction.Create(
        "editor.action.smartSelect.shrink", "Shrink Selection", null,
        (editor, _) => RunOn(editor, false),
        Keybinding((int)KeyCode.LeftArrow));
}
