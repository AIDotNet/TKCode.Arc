// Ported from vscode (src/vs/editor/contrib/message/browser/messageController.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the message is plain text (Markdown messages are shown as their source);
//  - the widget is a CodeEditorWidgetSurface content widget above (else below) the position; no fade-out animation;
//  - closes on cursor and model changes, when the editor's text loses focus (unless the pointer is over the message), on
//    mouse moves more than three lines away, and with the leaveEditorMessage command (Escape), as upstream.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc;

/// <summary>Shows a short message at a position of the editor, such as "No definition found" (upstream's MessageController).</summary>
internal sealed class MessageController : ICodeEditorContribution
{
    public const string Id = "editor.contrib.messageController";

    /// <summary>Whether a message shows (upstream's MessageController.MESSAGE_VISIBLE).</summary>
    public static readonly RawContextKey<bool> MessageVisible = new("messageVisible", false, "Whether the editor is currently showing an inline message");

    public static readonly EditorContributionDescription Description = new(Id, editor => new MessageController(editor), EditorContributionInstantiation.Lazy);

    public static readonly EditorCommand LeaveEditorMessageCommand = EditorCommand.BindToContribution<MessageController>(
        Id, "leaveEditorMessage", MessageVisible, static (controller, _) => controller.CloseMessage(),
        new EditorKeybinding((int)KeyCode.Escape) { Weight = KeybindingWeight.EditorContrib + 30 });

    private readonly CodeEditor _editor;
    private readonly IContextKey<bool> _visible;
    private MessageWidget? _widget;
    private TextRange? _bounds;
    private bool _mouseOverMessage;

    private MessageController(CodeEditor editor)
    {
        _editor = editor;
        _visible = editor.CreateContextKey(MessageVisible);
    }

    public static MessageController? Get(CodeEditor editor) => editor.GetContribution<MessageController>(Id);

    public bool IsVisible => _widget is not null;

    /// <summary>The text of the message that shows, or null.</summary>
    internal string? Text => _widget?.Text;

    public void Dispose() => CloseMessage();

    /// <summary>Shows <paramref name="message"/> at <paramref name="position"/>, replacing a message that shows.</summary>
    public void ShowMessage(string message, TextPosition position)
    {
        CloseMessage();
        _visible.Set(true);
        _widget = new MessageWidget(position, message);
        _widget.Control.PointerEntered += OnPointerEntered;
        _widget.Control.PointerExited += OnPointerExited;
        _editor.AddContentWidget(_widget);

        // close on blur, cursor, model change, and mouse moves away
        _bounds = null;
        _editor.DidChangeTextFocus += OnTextFocusChanged;
        _editor.DidChangeCursorSelection += OnCursorChanged;
        _editor.DidChangeModelLanguage += OnModelChanged;
        _editor.MouseMove += OnMouseMove;
    }

    public void CloseMessage()
    {
        _visible.Reset();
        if (_widget is not { } widget)
        {
            return;
        }

        _widget = null;
        _mouseOverMessage = false;
        widget.Control.PointerEntered -= OnPointerEntered;
        widget.Control.PointerExited -= OnPointerExited;
        _editor.DidChangeTextFocus -= OnTextFocusChanged;
        _editor.DidChangeCursorSelection -= OnCursorChanged;
        _editor.DidChangeModelLanguage -= OnModelChanged;
        _editor.MouseMove -= OnMouseMove;
        _editor.RemoveContentWidget(widget);
    }

    private void OnPointerEntered(object? sender, Avalonia.Input.PointerEventArgs e) => _mouseOverMessage = true;

    private void OnPointerExited(object? sender, Avalonia.Input.PointerEventArgs e) => _mouseOverMessage = false;

    private void OnTextFocusChanged(bool focused)
    {
        if (!focused && !_mouseOverMessage)
        {
            CloseMessage();
        }
    }

    private void OnCursorChanged(Editor.ViewModel.CursorStateChangedEvent e) => CloseMessage();

    private void OnModelChanged(Editor.Model.ModelLanguageChangedEvent e) => CloseMessage();

    private void OnMouseMove(CodeEditorMouseEvent e)
    {
        // outside the text area
        if (e.Target.Position is not { } position || _widget is null)
        {
            return;
        }

        if (_bounds is null)
        {
            // a box around the message's position and the first pointer position
            _bounds = new TextRange(_widget.Position.LineNumber - 3, 1, position.LineNumber + 3, 1);
        }
        else if (!_bounds.Value.ContainsPosition(position))
        {
            CloseMessage();
        }
    }

    /// <summary>The message box (upstream's MessageWidget).</summary>
    private sealed class MessageWidget : ICodeEditorContentWidget
    {
        private readonly CodeEditorWidgetSurface _surface;

        public MessageWidget(TextPosition position, string text)
        {
            Position = position;
            Text = text;
            _surface = new CodeEditorWidgetSurface
            {
                Padding = new Thickness(8, 4),
                MaxWidth = 500,
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
            };
        }

        public string Id => "editor.contrib.messageWidget";

        public Control Control => _surface;

        public TextPosition Position { get; }

        public string Text { get; }

        public ContentWidgetPosition? GetPosition() =>
            new(Position, [ContentWidgetPositionPreference.Above, ContentWidgetPositionPreference.Below]);
    }
}
