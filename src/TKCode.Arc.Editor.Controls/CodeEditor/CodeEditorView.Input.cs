using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.ViewModel;
using EditorKeyCode = TKCode.Arc.Editor.KeyCode;

namespace TKCode.Arc;

// Input: keys go through the keybinding resolver to the editor's commands (upstream's StandaloneKeybindingService),
// typed text to viewModel.type; the clipboard commands copy, cut and paste through the top level's clipboard
// (upstream's ClipboardEventUtils). The context keys the commands' `when` clauses read follow the focus, the read-only
// option and the selections (upstream's EditorContextKeysManager). The pointer is in CodeEditorView.Mouse.cs, the input
// method in CodeEditorView.Ime.cs.
internal sealed partial class CodeEditorView
{
    private const string CommandSource = "keyboard";

    private readonly List<string> _currentChords = new();
    private KeybindingResolver? _resolver;
    private DispatcherTimer? _blinkTimer;
    private bool _cursorVisible = true;
    private bool _editorFocused;

    internal KeybindingResolver Resolver => _resolver ??= CreateResolver();

    /// <summary>Builds the keybinding resolver again from the registry (after keybindings were added or removed).</summary>
    internal void InvalidateKeybindings() => _resolver = null;

    private KeybindingResolver CreateResolver()
    {
        var items = new List<ResolvedKeybindingItem>();
        foreach (var item in _editor!.KeybindingsRegistry.GetDefaultKeybindings())
        {
            ResolvedKeybinding? resolved = null;
            if (item.Keybinding is { } keybinding)
            {
                var resolvedKeybindings = USLayoutResolvedKeybinding.ResolveKeybinding(keybinding, OperatingSystemKinds.OS);
                resolved = resolvedKeybindings.Count > 0 ? resolvedKeybindings[0] : null;
            }

            items.Add(new ResolvedKeybindingItem(resolved, item.Command, item.CommandArgs, item.When, true, item.ExtensionId, item.IsBuiltinExtension));
        }

        return new KeybindingResolver(items, [], static _ => { });
    }

    /// <summary>
    /// Handles a key press the way a focused editor does; returns whether a keybinding handled it. A key from a widget
    /// in the editor (<paramref name="fromWidget"/>) only goes through the keybindings: no typing, no clipboard.
    /// </summary>
    internal bool HandleKey(Key key, KeyModifiers modifiers, bool fromWidget = false)
    {
        if (_editor is null || CodeEditorKeys.IsModifier(key))
        {
            return false;
        }

        if (IsComposing && !fromWidget)
        {
            // the input method owns the keys while it composes (upstream's TextAreaInput ignores keydown during
            // composition); the commit arrives as text
            return true;
        }

        var keyCode = CodeEditorKeys.ToKeyCode(key);
        if (keyCode == EditorKeyCode.Unknown)
        {
            return false;
        }

        var chord = new KeyCodeChord(
            modifiers.HasFlag(KeyModifiers.Control),
            modifiers.HasFlag(KeyModifiers.Shift),
            modifiers.HasFlag(KeyModifiers.Alt),
            modifiers.HasFlag(KeyModifiers.Meta),
            keyCode);
        var dispatch = USLayoutResolvedKeybinding.GetDispatchStr(chord);
        if (dispatch is null)
        {
            return false;
        }

        UpdateContextKeys();
        var result = Resolver.Resolve(_editor.ContextKeyService.GetContext(), _currentChords, dispatch);
        switch (result.Kind)
        {
            case ResultKind.MoreChordsNeeded:
                _currentChords.Add(dispatch);
                return true;
            case ResultKind.KbFound when result.CommandId is { } commandId:
                _currentChords.Clear();
                _ = _editor.ExecuteCommand(commandId, result.CommandArgs);
                RestartBlink();
                return true;
            default:
                _currentChords.Clear();
                return !fromWidget && HandleUnboundKey(keyCode, modifiers);
        }
    }

    // Keys the editor handles outside the keybindings of the core commands: Enter types a line break (upstream's
    // TextArea sends it as typed text), and the clipboard keys (upstream's clipboard contribution).
    private bool HandleUnboundKey(EditorKeyCode keyCode, KeyModifiers modifiers)
    {
        var ctrlCmd = OperatingSystem.IsMacOS() ? modifiers.HasFlag(KeyModifiers.Meta) : modifiers.HasFlag(KeyModifiers.Control);
        var others = modifiers & ~(KeyModifiers.Control | KeyModifiers.Meta | KeyModifiers.Shift);
        if (keyCode == EditorKeyCode.Enter && !ctrlCmd && others == KeyModifiers.None)
        {
            TypeText("\n");
            return true;
        }

        if (ctrlCmd && others == KeyModifiers.None)
        {
            switch (keyCode)
            {
                case EditorKeyCode.KeyC:
                    _ = CopyAsync(cut: false);
                    return true;
                case EditorKeyCode.KeyX:
                    _ = CopyAsync(cut: true);
                    return true;
                case EditorKeyCode.KeyV:
                    _ = PasteAsync();
                    return true;
            }
        }

        return false;
    }

    internal void TypeText(string text)
    {
        if (_editor is null || _viewModel is null || _editor.IsReadOnly || text.Length == 0)
        {
            return;
        }

        _editor.TypeText(CommandSource, text);
        RestartBlink();
    }

    private async Task CopyAsync(bool cut)
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        var selections = _viewModel.GetCursorStates().Select(s => s.ModelState.Selection.Range).ToList();
        var copy = _viewModel.GetPlainTextToCopy(selections, emptySelectionClipboard: true, forceCRLF: OperatingSystem.IsWindows());
        var text = copy.SourceText ?? string.Join(Environment.NewLine, copy.SourceTexts ?? []);
        await clipboard.SetTextAsync(text);
        if (cut && _editor is { IsReadOnly: false })
        {
            _viewModel.Cut(CommandSource);
        }
    }

    private async Task PasteAsync()
    {
        if (_viewModel is null || _editor is { IsReadOnly: true } || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        var text = await clipboard.TryGetTextAsync();
        if (!string.IsNullOrEmpty(text))
        {
            _editor?.PasteText(CommandSource, text, pasteOnNewLine: false, multicursorText: null);
        }
    }

    private IContextKey<bool>[]? _contextKeys;

    // upstream's EditorContextKeysManager: the keys the core commands' `when` clauses read
    internal void UpdateContextKeys()
    {
        if (_editor is not { } editor)
        {
            return;
        }

        var service = editor.ContextKeyService;
        _contextKeys ??=
        [
            EditorContextKeys.EditorTextFocus.BindTo(service),
            EditorContextKeys.TextInputFocus.BindTo(service),
            EditorContextKeys.Focus.BindTo(service),
            EditorContextKeys.ReadOnly.BindTo(service),
            EditorContextKeys.HasMultipleSelections.BindTo(service),
            EditorContextKeys.HasNonEmptySelection.BindTo(service),
        ];

        var focused = _editorFocused;
        var selections = _viewModel?.GetSelections() ?? [];
        _contextKeys[0].Set(focused);
        _contextKeys[1].Set(focused);

        // editorFocus: the text or a widget in the editor (the find input)
        _contextKeys[2].Set(focused || editor.IsKeyboardFocusWithin);
        _contextKeys[3].Set(editor.IsReadOnly);
        _contextKeys[4].Set(selections.Count > 1);
        _contextKeys[5].Set(selections.Any(static s => !s.IsEmpty));
    }

    // ---- reveal

    private void Reveal(ViewRevealRangeRequestEvent e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var ranges = new List<TextRange>();
        if (e.Selections is { } selections)
        {
            ranges.AddRange(selections.Select(s => s.Range));
        }
        else if (e.Range is { } range)
        {
            ranges.Add(range);
        }

        if (ranges.Count == 0)
        {
            return;
        }

        var startLine = ranges.Min(r => r.StartLineNumber);
        var endLine = ranges.Max(r => r.EndLineNumber);
        var layout = _viewModel.ViewLayout;
        var top = layout.GetVerticalOffsetForLineNumber(startLine);
        var bottom = layout.GetVerticalOffsetForLineNumber(endLine) + layout.GetLineHeightForLineNumber(endLine);
        var scrollTop = layout.GetCurrentScrollTop();
        var outside = top < scrollTop || bottom > scrollTop + Bounds.Height;
        switch (e.VerticalType)
        {
            case VerticalRevealType.Center:
            case VerticalRevealType.CenterIfOutsideViewport when outside:
                // upstream's ViewLines._computeScrollTopToRevealRange: the range in the middle of the viewport
                scrollTop = Math.Max(0, ((top + bottom) / 2) - (Bounds.Height / 2));
                break;
            case VerticalRevealType.Top:
            case VerticalRevealType.NearTop:
            case VerticalRevealType.NearTopIfOutsideViewport when outside:
                scrollTop = Math.Max(0, top - (e.VerticalType == VerticalRevealType.Top ? 0 : _lineHeight * 5));
                break;
            case VerticalRevealType.Bottom:
                scrollTop = Math.Max(0, bottom - Bounds.Height);
                break;
            default:
                if (top < scrollTop)
                {
                    scrollTop = top;
                }
                else if (bottom > scrollTop + Bounds.Height)
                {
                    scrollTop = bottom - Bounds.Height;
                }

                break;
        }

        var scrollLeft = layout.GetCurrentScrollLeft();
        if (e.RevealHorizontal && e.Selections is null && e.Range is { } r)
        {
            var x = GetX(r.StartLineNumber, r.StartColumn) + ScrollLeft - GutterWidth;
            var width = Math.Max(0, TextAreaRight - GutterWidth);
            if (x < scrollLeft)
            {
                scrollLeft = Math.Max(0, x - (_charWidth * 4));
            }
            else if (x + _charWidth > scrollLeft + width)
            {
                scrollLeft = x + (_charWidth * 4) - width;
            }
        }

        layout.SetScrollPosition(new NewScrollPosition(scrollLeft, scrollTop), e.ScrollType);
    }

    // ---- focus and blinking

    internal void OnEditorFocusChanged(bool focused)
    {
        _editorFocused = focused;
        _viewModel?.SetHasFocus(focused);
        if (focused)
        {
            RestartBlink();
        }
        else
        {
            _blinkTimer?.Stop();
            _currentChords.Clear();
            CancelComposition();
            EndMouseDown();
        }

        UpdateContextKeys();
        InvalidateCursors();
    }

    private void RestartBlink()
    {
        _cursorVisible = true;
        _blinkTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(530), DispatcherPriority.Background, (_, _) =>
        {
            _cursorVisible = !_cursorVisible;
            InvalidateCursors();
        });
        _blinkTimer.Stop();
        _blinkTimer.Start();
        InvalidateCursors();
    }

    /// <summary>The read-only option changed: the context keys and the input method follow it.</summary>
    internal void OnReadOnlyChanged()
    {
        if (_editor is { IsReadOnly: true })
        {
            CancelComposition();
        }

        UpdateContextKeys();
    }

    // ---- view events the input side follows

    /// <summary>Called when the view model is created: hooks the input side to it.</summary>
    private void OnViewModelCreated()
    {
        if (_viewModel is null || _editor is null)
        {
            return;
        }

        _inputHandler = new InputViewHandler(this);
        _viewModel.AddViewEventHandler(_inputHandler);
        _editorFocused = _editor.IsKeyboardFocusWithin;
        EnsurePreeditLayer();
        UpdateContextKeys();
    }

    /// <summary>Called before the view model goes away.</summary>
    private void OnViewModelDetaching()
    {
        if (_viewModel is not null && _inputHandler is not null)
        {
            _viewModel.RemoveViewEventHandler(_inputHandler);
        }

        _inputHandler = null;
        OnViewModelDetachingForMinimap();
        EndMouseDown();
        SetPreedit(null, null);
    }

    private InputViewHandler? _inputHandler;

    /// <summary>Keeps the context keys and the input method's caret in step with the cursors and the scroll position.</summary>
    private sealed class InputViewHandler(CodeEditorView view) : ViewEventHandler
    {
        public override bool OnCursorStateChanged(ViewCursorStateChangedEvent e)
        {
            view.UpdateContextKeys();
            view.NotifyImeCursorMoved();
            return false;
        }

        public override bool OnScrollChanged(ViewScrollChangedEvent e)
        {
            view.NotifyImeCursorMoved();
            return false;
        }

        public override bool OnConfigurationChanged(ViewConfigurationChangedEvent e)
        {
            view.NotifyImeCursorMoved();
            return false;
        }
    }
}
