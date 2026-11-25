// Ported from vscode (src/vs/base/browser/ui/aria/aria.ts: setARIAContainer, alert, status, insertMessage; commit
// b57d5c9e), MIT license.
// Differences from upstream:
//  - one set of containers per editor (upstream has one per window, in a module-level variable; the editor keeps no
//    shared mutable static state), created on the first message and put into the editor's widget host;
//  - a container is a control that draws nothing (CodeEditorAriaContainer) whose automation name is the message, with
//    AutomationProperties.LiveSetting Assertive (alert) or Polite (status); its peer reports each name change, which
//    Avalonia's platform layer turns into a live region change for a live element (UIA's LiveRegionChanged on Windows;
//    ControlAutomationPeer itself does not report name changes);
//  - upstream's visibility toggle that makes old browsers notice the change has no equivalent and is left out.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The editor's live regions for screen readers (upstream's aria.ts): <see cref="Alert"/> interrupts,
/// <see cref="Status"/> waits until the screen reader is idle. Two containers each, used in turn, so that the same
/// message twice in a row is read twice.
/// </summary>
internal sealed class CodeEditorAria
{
    // a long message can freeze the screen reader (upstream #94233)
    internal const int MaxMessageLength = 20000;

    private readonly CodeEditorAriaContainer _alertContainer = CreateContainer(AutomationLiveSetting.Assertive, "alert1");
    private readonly CodeEditorAriaContainer _alertContainer2 = CreateContainer(AutomationLiveSetting.Assertive, "alert2");
    private readonly CodeEditorAriaContainer _statusContainer = CreateContainer(AutomationLiveSetting.Polite, "status1");
    private readonly CodeEditorAriaContainer _statusContainer2 = CreateContainer(AutomationLiveSetting.Polite, "status2");
    private Panel? _host;

    /// <summary>The last message of either kind (for tests).</summary>
    internal string? LastMessage { get; private set; }

    /// <summary>The four containers, alerts first (for tests).</summary>
    internal IReadOnlyList<CodeEditorAriaContainer> Containers => [_alertContainer, _alertContainer2, _statusContainer, _statusContainer2];

    /// <summary>Puts the containers into <paramref name="host"/> (the editor's widget host), out of any other host.</summary>
    internal void AttachTo(Panel? host)
    {
        if (ReferenceEquals(host, _host))
        {
            return;
        }

        foreach (var container in Containers)
        {
            _host?.Children.Remove(container);
            host?.Children.Add(container);
        }

        _host = host;
    }

    /// <summary>A message the screen reader reads at once (upstream's alert).</summary>
    internal void Alert(string message)
    {
        if (GetText(_alertContainer) != message)
        {
            Clear(_alertContainer2);
            InsertMessage(_alertContainer, message);
        }
        else
        {
            Clear(_alertContainer);
            InsertMessage(_alertContainer2, message);
        }
    }

    /// <summary>A message the screen reader reads when it is idle (upstream's status).</summary>
    internal void Status(string message)
    {
        if (GetText(_statusContainer) != message)
        {
            Clear(_statusContainer2);
            InsertMessage(_statusContainer, message);
        }
        else
        {
            Clear(_statusContainer);
            InsertMessage(_statusContainer2, message);
        }
    }

    private void InsertMessage(CodeEditorAriaContainer target, string message)
    {
        if (message.Length > MaxMessageLength)
        {
            message = message[..MaxMessageLength];
        }

        LastMessage = message;
        AutomationProperties.SetName(target, message);
    }

    private static string? GetText(CodeEditorAriaContainer container) => AutomationProperties.GetName(container);

    private static void Clear(CodeEditorAriaContainer container) => container.ClearValue(AutomationProperties.NameProperty);

    private static CodeEditorAriaContainer CreateContainer(AutomationLiveSetting liveSetting, string id)
    {
        var container = new CodeEditorAriaContainer();
        AutomationProperties.SetLiveSetting(container, liveSetting);
        AutomationProperties.SetAutomationId(container, "monaco-aria-" + id);
        return container;
    }
}

/// <summary>A live region of <see cref="CodeEditorAria"/>: draws nothing; its automation name is the message.</summary>
internal sealed class CodeEditorAriaContainer : Control
{
    public CodeEditorAriaContainer()
    {
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ContainerPeer(this);

    private sealed class ContainerPeer : ControlAutomationPeer
    {
        public ContainerPeer(CodeEditorAriaContainer owner)
            : base(owner)
        {
            owner.PropertyChanged += (_, e) =>
            {
                if (e.Property == AutomationProperties.NameProperty)
                {
                    RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, e.OldValue, e.NewValue);
                }
            };
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
    }
}
