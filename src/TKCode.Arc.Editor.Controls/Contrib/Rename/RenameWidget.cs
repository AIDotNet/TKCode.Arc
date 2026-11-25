// Ported from vscode (src/vs/editor/contrib/rename/browser/renameWidget.ts: RenameWidget's getPosition, getInput,
// acceptInput, cancelInput and _getSelection, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the box is an Arc Input on a CodeEditorWidgetSurface; there are no rename suggestions (new symbol name providers are
//    not ported) and no preview, so the hint below the input says only "Enter to Rename";
//  - getInput returns a Task of a RenameInputResult (null: cancelled) instead of `RenameWidgetResult | boolean`; the
//    focus flag of a cancel is FocusEditor;
//  - the widget prefers to show below the word, above when there is no room (upstream decides by the room for six rename
//    suggestions); it is not cancelled when its line scrolls out of view (upstream's afterRender).

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc;

/// <summary>What the rename box returned: the new name, or a cancel (and whether the editor should get the focus back).</summary>
internal sealed record RenameInputResult(string? NewName, bool FocusEditor);

/// <summary>The rename box: an input over the symbol (upstream's RenameWidget).</summary>
internal sealed class RenameWidget : ICodeEditorContentWidget
{
    private readonly CodeEditor _editor;
    private readonly IContextKey<bool> _visibleKey;
    private readonly CodeEditorWidgetSurface _surface;
    private readonly Input _input;
    private TextPosition? _position;
    private Action? _currentAcceptInput;
    private Action<bool>? _currentCancelInput;
    private bool _visible;

    public RenameWidget(CodeEditor editor)
    {
        _editor = editor;
        _visibleKey = editor.CreateContextKey(CodeEditorContextKeys.RenameInputVisible);
        _input = new Input
        {
            Size = ControlSize.Small,
            FontFamily = editor.FontFamily,
            FontSize = editor.FontSize,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var hint = new TextBlock { Text = "Enter to Rename", FontSize = 11, Margin = new Thickness(2, 4, 2, 0) };
        _surface = new CodeEditorWidgetSurface
        {
            Padding = new Thickness(4),
            HorizontalAlignment = HorizontalAlignment.Left,
            IsVisible = false,
            Child = new StackPanel { Children = { _input, hint } },
        };
        _surface.BindBrush(hint, TextBlock.ForegroundProperty, CodeEditorWidgetSurface.MutedForegroundProperty);

        // the text box handles Enter and Escape itself: they go to the editor's keybindings first (acceptRenameInput,
        // cancelRenameInput), as upstream's keybinding service sees them before the input does
        _input.AddHandler(InputElement.KeyDownEvent, OnInputKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape && _editor.View?.HandleKey(e.Key, e.KeyModifiers, fromWidget: true) == true)
        {
            e.Handled = true;
        }
    }

    public string Id => "__renameInputWidget";

    public Control Control => _surface;

    /// <summary>The input (for tests).</summary>
    internal Input Input => _input;

    public bool IsVisible => _visible;

    public ContentWidgetPosition? GetPosition() =>
        _visible && _position is { } position
            ? new ContentWidgetPosition(position, [ContentWidgetPositionPreference.Below, ContentWidgetPositionPreference.Above])
            : null;

    /// <summary>Accepts the name in the box (Enter).</summary>
    public void AcceptInput() => _currentAcceptInput?.Invoke();

    /// <summary>Closes the box without renaming (Escape).</summary>
    public void CancelInput(bool focusEditor) => _currentCancelInput?.Invoke(focusEditor);

    /// <summary>
    /// Shows the box over <paramref name="where"/> with <paramref name="currentName"/> and waits for the user: the new
    /// name, or null when cancelled (the name unchanged or empty, Escape, the editor lost focus, <paramref name="token"/>).
    /// </summary>
    public Task<RenameInputResult> GetInput(TextRange where, string currentName, CancellationToken token)
    {
        var (selectionStart, selectionEnd) = GetSelection(where, currentName);
        _position = new TextPosition(where.StartLineNumber, where.StartColumn);
        _input.Text = currentName;
        // upstream's input size: 1.1 times the symbol's length, at least 20 characters (and 200 pixels)
        var charWidth = _editor.View?.CharWidth ?? 8;
        _input.Width = Math.Max(200, (Math.Max((where.EndColumn - where.StartColumn) * 1.1, 20) * charWidth) + 24);

        var inputResult = new TaskCompletionSource<RenameInputResult>();
        CancellationTokenRegistration registration = default;
        void Done()
        {
            _currentAcceptInput = null;
            _currentCancelInput = null;
            registration.Dispose();
            _editor.PropertyChanged -= OnEditorPropertyChanged;
            Hide();
        }

        _currentCancelInput = focusEditor =>
        {
            Done();
            inputResult.TrySetResult(new RenameInputResult(null, focusEditor));
        };

        _currentAcceptInput = () =>
        {
            var newName = _input.Text ?? string.Empty;
            if (newName == currentName || newName.Trim().Length == 0)
            {
                CancelInput(focusEditor: true);
                return;
            }

            Done();
            inputResult.TrySetResult(new RenameInputResult(newName, true));
        };

        registration = token.Register(() => Dispatcher.UIThread.Post(() => CancelInput(focusEditor: true)));
        _editor.PropertyChanged += OnEditorPropertyChanged;
        Show(selectionStart, selectionEnd);
        return inputResult.Task;
    }

    // upstream's onDidBlurEditorWidget: the focus left the editor and its widgets
    private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == InputElement.IsKeyboardFocusWithinProperty && !_editor.IsKeyboardFocusWithin)
        {
            CancelInput(focusEditor: false);
        }
    }

    private (int Start, int End) GetSelection(TextRange where, string currentName)
    {
        var start = 0;
        var end = currentName.Length;
        if (_editor.GetSelection() is { IsEmpty: false } selection
            && selection.StartLineNumber == selection.EndLineNumber
            && where.ContainsRange(selection.Range))
        {
            start = Math.Max(0, selection.StartColumn - where.StartColumn);
            end = Math.Min(where.EndColumn, selection.EndColumn) - where.StartColumn;
        }

        return (start, end);
    }

    private void Show(int selectionStart, int selectionEnd)
    {
        _visible = true;
        _visibleKey.Set(true);
        _surface.IsVisible = true;
        if (!(_editor.WidgetHost?.Children.Contains(_surface) ?? false))
        {
            _editor.AddContentWidget(this);
        }

        _editor.LayoutContentWidget(this);
        void FocusInput()
        {
            if (!_visible)
            {
                return;
            }

            _input.Focus();
            _input.SelectionStart = selectionStart;
            _input.SelectionEnd = selectionEnd;
        }

        FocusInput();
        Dispatcher.UIThread.Post(FocusInput, DispatcherPriority.Input);
    }

    private void Hide()
    {
        if (!_visible)
        {
            return;
        }

        _visible = false;
        _visibleKey.Reset();
        _surface.IsVisible = false;
        _editor.LayoutContentWidget(this);
    }

    public void Dispose()
    {
        CancelInput(focusEditor: false);
        _editor.RemoveContentWidget(this);
    }
}
