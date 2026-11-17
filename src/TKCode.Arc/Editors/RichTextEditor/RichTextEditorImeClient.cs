using System.Collections.Immutable;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.Utilities;

namespace TKCode.Arc;

/// <summary>Connects the platform input method (composition, candidate window placement, surrounding text) to the surface.</summary>
internal sealed class RichTextEditorImeClient : TextInputMethodClient
{
    private readonly RichTextEditorSurface _surface;

    public RichTextEditorImeClient(RichTextEditorSurface surface)
    {
        _surface = surface;
    }

    public override Visual TextViewVisual => _surface;

    public override bool SupportsPreedit => true;

    public override bool SupportsSurroundingText => true;

    public override string SurroundingText => _surface.ImeSurroundingText;

    public override Rect CursorRectangle => _surface.ImeCursorRect;

    public override TextSelection Selection
    {
        get => _surface.ImeSelection;
        set => _surface.ImeSetSelection(value);
    }

    public override void SetPreeditText(string? preeditText) => SetPreeditText(preeditText, null);

    public override void SetPreeditText(string? preeditText, int? cursorPos) => _surface.SetPreedit(preeditText, cursorPos);

    public void NotifyCursor() => RaiseCursorRectangleChanged();

    public void NotifySurroundingText() => RaiseSurroundingTextChanged();

    public void NotifySelection() => RaiseSelectionChanged();

    public void Reset() => RequestReset();
}
