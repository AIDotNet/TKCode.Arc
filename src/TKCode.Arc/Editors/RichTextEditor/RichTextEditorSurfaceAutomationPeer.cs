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

/// <summary>Exposes the surface as a multi-line edit field whose value is the plain text.</summary>
internal sealed class RichTextEditorSurfaceAutomationPeer : ControlAutomationPeer, Avalonia.Automation.Provider.IValueProvider
{
    public RichTextEditorSurfaceAutomationPeer(RichTextEditorSurface owner) : base(owner)
    {
    }

    private RichTextEditor? Editor => ((RichTextEditorSurface)Owner).Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;

    protected override string? GetNameCore() => Editor is { } editor ? Avalonia.Automation.AutomationProperties.GetName(editor) ?? RichTextEditorStrings.Get(TKCode.Arc.Localization.RichTextEditorLangResourceKind.EditorName) : base.GetNameCore();

    protected override string? GetHelpTextCore() => Editor is { } editor ? Avalonia.Automation.AutomationProperties.GetHelpText(editor) : base.GetHelpTextCore();

    public bool IsReadOnly => Editor?.IsReadOnly ?? true;

    public string? Value => Editor?.GetText();

    public void SetValue(string? value)
    {
        if (Editor is { IsReadOnly: false } editor)
        {
            editor.SetMarkdown(value ?? string.Empty);
        }
    }
}
