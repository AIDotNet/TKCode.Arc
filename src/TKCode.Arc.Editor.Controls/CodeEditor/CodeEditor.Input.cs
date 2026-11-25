using Avalonia.Input;
using Avalonia.Input.TextInput;

namespace TKCode.Arc;

// The editor takes keyboard focus (the view is not focusable) and hands keys, typed text, the input method and focus
// changes to its view, as upstream's TextAreaHandler hands the hidden textarea's events to the view controller.
public sealed partial class CodeEditor
{
    private static void RegisterInputHandlers()
    {
        TextInputMethodClientRequestedEvent.AddClassHandler<CodeEditor>(static (editor, e) => editor.OnTextInputMethodClientRequested(e));
    }

    // A writable editor composes through the input method; a read-only one has nothing to compose into.
    private void OnTextInputMethodClientRequested(TextInputMethodClientRequestedEventArgs e)
    {
        if (_view is not null && !IsReadOnly)
        {
            e.Client = _view.InputMethodClient;
            e.Handled = true;
        }
    }

    private void OnReadOnlyChanged()
    {
        _view?.OnReadOnlyChanged();
        if (IsKeyboardFocusWithin)
        {
            // the platform asks again for the client (none when read-only)
            RaiseEvent(new TextInputMethodClientRequeryRequestedEventArgs { RoutedEvent = InputMethod.TextInputMethodClientRequeryRequestedEvent, Source = this });
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _view is null)
        {
            return;
        }

        OnBeforeInteractionEvent();
        _onKeyDown.Fire(e);
        if (e.Handled)
        {
            return;
        }

        if (!ReferenceEquals(e.Source, this))
        {
            // a key a widget in the editor did not handle (the find input): only the keybindings, against the context
            // keys of a focused widget (editorTextFocus is false), as upstream's keybinding service does
            if (_view.HandleKey(e.Key, e.KeyModifiers, fromWidget: true))
            {
                e.Handled = true;
            }

            return;
        }

        if (_view.HandleKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
        else if (e.Key == Key.Tab && !IsReadOnly && !TabFocusMode && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            // Tab and Shift+Tab indent and outdent in a writable editor: they do not move the focus out of it unless the
            // tab focus mode is on (Ctrl+M); a read-only editor lets them move the focus, as upstream does
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || _view is null || string.IsNullOrEmpty(e.Text) || !ReferenceEquals(e.Source, this))
        {
            // text typed into a widget stays there
            return;
        }

        OnBeforeInteractionEvent();

        // the input method commits its composition as text: the preedit goes, the text is typed
        _view.CommitComposition();

        // control characters arrive as keys (Enter, Tab, Backspace), not as text
        if (e.Text.Length == 1 && char.IsControl(e.Text[0]))
        {
            return;
        }

        _view.TypeText(e.Text);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);

        // focus inside a widget (the find input) bubbles here too: the text has focus only when the editor itself has
        UpdateTextFocus(ReferenceEquals(e.Source, this));
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        UpdateTextFocus(false);
    }

    private void UpdateTextFocus(bool textFocused)
    {
        var changed = _textFocused != textFocused;
        _textFocused = textFocused;
        _view?.OnEditorFocusChanged(textFocused);
        if (changed)
        {
            RaiseTextFocusChanged(textFocused);
        }
    }
}
