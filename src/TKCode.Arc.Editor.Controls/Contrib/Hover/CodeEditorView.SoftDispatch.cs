// Adaptation of vscode (src/vs/platform/keybinding/common/abstractKeybindingService.ts: softDispatch, commit b57d5c9e),
// MIT license: what a key would do without doing it. The content hover asks it so that the first key of a chord
// (Ctrl+K of Ctrl+K Ctrl+I) and the hover's own commands do not close the hover.

using Avalonia.Input;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Platform;
using EditorKeyCode = TKCode.Arc.Editor.KeyCode;

namespace TKCode.Arc;

internal sealed partial class CodeEditorView
{
    /// <summary>Resolves a key press against the keybindings without running anything (upstream's softDispatch).</summary>
    internal ResolutionResult? SoftDispatch(Key key, KeyModifiers modifiers)
    {
        if (_editor is null || CodeEditorKeys.IsModifier(key))
        {
            return null;
        }

        var keyCode = CodeEditorKeys.ToKeyCode(key);
        if (keyCode == EditorKeyCode.Unknown)
        {
            return null;
        }

        var chord = new KeyCodeChord(
            modifiers.HasFlag(KeyModifiers.Control),
            modifiers.HasFlag(KeyModifiers.Shift),
            modifiers.HasFlag(KeyModifiers.Alt),
            modifiers.HasFlag(KeyModifiers.Meta),
            keyCode);
        if (USLayoutResolvedKeybinding.GetDispatchStr(chord) is not { } dispatch)
        {
            return null;
        }

        UpdateContextKeys();
        return Resolver.Resolve(_editor.ContextKeyService.GetContext(), _currentChords, dispatch);
    }
}
