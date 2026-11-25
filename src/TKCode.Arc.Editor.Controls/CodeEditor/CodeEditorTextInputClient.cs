using Avalonia;
using Avalonia.Input.TextInput;

namespace TKCode.Arc;

/// <summary>
/// The platform input method's view of a <see cref="CodeEditor"/> (upstream's TextAreaHandler with its hidden textarea):
/// where the candidate window goes (the caret inside the composition, at the primary cursor) and the composition text
/// (preedit) the platform pushes while the user composes. Committed text arrives as a TextInput event and is typed like
/// any other text. Surrounding text is not offered: the document is not one string, and the editor's own cursors decide
/// what a deletion removes.
/// </summary>
internal sealed class CodeEditorTextInputClient(CodeEditorView view) : TextInputMethodClient
{
    private TextSelection _selection;

    public override Visual TextViewVisual => view;

    public override bool SupportsPreedit => true;

    public override bool SupportsSurroundingText => false;

    public override string SurroundingText => string.Empty;

    public override Rect CursorRectangle => view.ImeCursorRectangle;

    public override TextSelection Selection
    {
        get => _selection;
        set => _selection = value;
    }

    public override void SetPreeditText(string? preeditText) => view.SetPreedit(preeditText, null);

    public override void SetPreeditText(string? preeditText, int? cursorPos) => view.SetPreedit(preeditText, cursorPos);

    /// <summary>Tells the platform that the caret moved (the candidate window follows it).</summary>
    public void NotifyCursorRectangleChanged() => RaiseCursorRectangleChanged();

    /// <summary>Asks the platform to drop the composition (the user clicked elsewhere).</summary>
    public void Reset() => RequestReset();
}
