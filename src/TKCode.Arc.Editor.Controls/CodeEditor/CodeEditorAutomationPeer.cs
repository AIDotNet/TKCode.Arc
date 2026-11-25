// The editor for assistive technology. Behavior reference: vscode (src/vs/editor/browser/controller/editContext/
// textArea/textAreaEditContext.ts, the attributes of the hidden textarea: role textbox, aria-roledescription
// "editor", aria-multiline, aria-label from the ariaLabel option, aria-readonly; commit b57d5c9e, MIT license).
// Avalonia 12 has no text pattern (ITextProvider), so the editor is an Edit element with the value pattern: its value is
// the whole document, as TextBox's; what upstream's textarea lets a screen reader read on its own (the line or the
// selection under the cursor) is announced through live regions instead (CodeEditor.Accessibility.cs).

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using TKCode.Arc.Editor;

namespace TKCode.Arc;

/// <summary>
/// The automation peer of <see cref="CodeEditor"/>: an edit control whose value is the document, with its name from
/// <see cref="AutomationProperties.NameProperty"/> (default "Editor content", Monaco's ariaLabel), "editor" as the
/// localized control type and the cursor's line and column as its item status.
/// </summary>
internal sealed class CodeEditorAutomationPeer : ControlAutomationPeer, IValueProvider
{
    /// <summary>Monaco's default ariaLabel (nls key editorViewAccessibleLabel).</summary>
    internal const string DefaultName = "Editor content";

    private const string AutomationSource = "automation";

    private string? _lastValue;
    private string? _lastItemStatus;

    /// <summary>Creates the peer of <paramref name="owner"/>.</summary>
    public CodeEditorAutomationPeer(CodeEditor owner)
        : base(owner)
    {
        _lastValue = owner.Text;
        owner.TextChanged += (_, _) => OnTextChanged();
    }

    /// <summary>The editor.</summary>
    public new CodeEditor Owner => (CodeEditor)base.Owner;

    /// <inheritdoc />
    public bool IsReadOnly => Owner.IsReadOnly;

    /// <inheritdoc />
    public string? Value => Owner.Text;

    /// <inheritdoc />
    public void SetValue(string? value)
    {
        if (Owner.IsReadOnly)
        {
            throw new InvalidOperationException("The editor is read-only.");
        }

        // one undo step that replaces the document (setting Text would clear the undo history); without a view yet,
        // the text is simply set
        var editor = Owner;
        editor.PushUndoStop();
        if (!editor.ExecuteEdits(AutomationSource, [new SingleEditOperation(editor.Model.GetFullModelRange(), value ?? string.Empty)]))
        {
            editor.Text = value ?? string.Empty;
        }

        editor.PushUndoStop();
    }

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(CodeEditor);

    /// <inheritdoc />
    protected override string? GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrEmpty(name) ? DefaultName : name;
    }

    /// <inheritdoc />
    protected override string GetLocalizedControlTypeCore() => "editor";

    /// <inheritdoc />
    protected override string? GetItemStatusCore() => Owner.GetCursorStatusText();

    /// <summary>The cursor moved: the item status (line and column) follows.</summary>
    internal void OnCursorChanged()
    {
        var status = Owner.GetCursorStatusText();
        if (status != _lastItemStatus)
        {
            var old = _lastItemStatus;
            _lastItemStatus = status;
            RaisePropertyChangedEvent(AutomationElementIdentifiers.ItemStatusProperty, old, status);
        }
    }

    private void OnTextChanged()
    {
        var value = Owner.Text;
        var old = _lastValue;
        _lastValue = value;
        RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, old, value);
    }
}
