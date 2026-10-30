using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The hidden live region of the Special controls (the explainers and the AI components): screen readers say its name
/// whenever it changes, and saying the same text twice still says it again. Polite by default (<see cref="IsAssertive"/>
/// for an error that must interrupt). It takes no space and no input. The explainers speak through it only when the reader
/// drives the story (step, pause, a control change, the end of a stepped run), never while the story plays on its own; the
/// AI components announce settled outcomes, never streamed text or counts.
/// <para>
/// Its automation peer is a text element that raises a Name change with every announcement: Avalonia's platform layers
/// (UI Automation's LiveRegionChanged, the macOS announcement) fire only on that event, which a plain control's peer never
/// raises when <see cref="AutomationProperties.NameProperty"/> changes.
/// </para>
/// </summary>
internal sealed class ExplainerAnnouncer : Control
{
    private string? _last;
    private bool _toggle;

    public ExplainerAnnouncer()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>The last announced text (without the repeat marker), or null before the first and after <see cref="Clear"/>.</summary>
    public string? Announcement => _last;

    /// <summary>Every announcement since the control was created, oldest first (at most 64; tests read it).</summary>
    public List<string> History { get; } = new();

    /// <summary>Interrupts the reader (assertive) instead of waiting for it (polite, the default).</summary>
    public bool IsAssertive
    {
        get => AutomationProperties.GetLiveSetting(this) == AutomationLiveSetting.Assertive;
        set => AutomationProperties.SetLiveSetting(this, value ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
    }

    /// <summary>Announces <paramref name="text"/>; empty text is ignored.</summary>
    public void Announce(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        // A repeated message toggles an invisible marker so the name still changes and is read again.
        _toggle = text == _last && !_toggle;
        _last = text;
        History.Add(text);
        if (History.Count > 64)
        {
            History.RemoveAt(0);
        }
        AutomationProperties.SetName(this, _toggle ? text + "​" : text);
    }

    /// <summary>Empties the region without saying anything.</summary>
    public void Clear()
    {
        _last = null;
        _toggle = false;
        AutomationProperties.SetName(this, string.Empty);
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override AutomationPeer OnCreateAutomationPeer() => new AnnouncerPeer(this);

    /// <summary>A text element whose name is the announcement; it reports every change of the name.</summary>
    private sealed class AnnouncerPeer : ControlAutomationPeer
    {
        public AnnouncerPeer(ExplainerAnnouncer owner)
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

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
