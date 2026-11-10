using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// A hidden polite live region (Arc's visually hidden <c>aria-live="polite"</c> span) for the avatar controls. Screen
/// readers say its name whenever it changes; announcing the same text twice still announces it again.
/// <para>
/// Its automation peer is a text element that raises a Name change with every announcement: the platform layers fire
/// their live-region events only on that event, which a plain control's peer never raises.
/// </para>
/// </summary>
internal sealed class AvatarsLiveRegion : Control
{
    private string? _last;
    private bool _toggle;

    public AvatarsLiveRegion()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>The last announced text (without the repeat marker), or null before the first.</summary>
    public string? Announcement => _last;

    /// <summary>Every announcement since the control was created, oldest first (at most 64; tests read it).</summary>
    public List<string> History { get; } = new();

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

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override AutomationPeer OnCreateAutomationPeer() => new RegionPeer(this);

    /// <summary>A text element whose name is the announcement; it reports every change of the name.</summary>
    private sealed class RegionPeer : ControlAutomationPeer
    {
        public RegionPeer(AvatarsLiveRegion owner)
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
