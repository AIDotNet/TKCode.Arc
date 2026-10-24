using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace TKCode.Arc;

/// <summary>
/// The chevron half of a split button or button group: an Arc <see cref="Button"/> wired to a <see cref="MenuSurface"/>
/// through <see cref="DropdownMenu.SetMenu"/>. It never press-scales (the menu measures its anchor), shows
/// <c>:open</c> while the menu is open and reports ExpandCollapse to assistive technology.
/// </summary>
[PseudoClasses(OpenPseudoClass)]
internal sealed class ButtonsChevronTrigger : Button
{
    public const string OpenPseudoClass = ":open";

    private MenuSurface? _menu;
    // The menu may outlive this trigger (a ButtonGroup's Menu comes from the app): it is listened to only while shown.
    private MenuSurface? _observedMenu;
    private bool _watching;

    static ButtonsChevronTrigger()
    {
        IsPressScaleEnabledProperty.OverrideDefaultValue<ButtonsChevronTrigger>(false);
    }

    /// <summary>The menu this trigger opens (null detaches it).</summary>
    public MenuSurface? Menu
    {
        get => _menu;
        set
        {
            if (ReferenceEquals(_menu, value))
            {
                return;
            }
            _menu = value;
            DropdownMenu.SetMenu(this, value);
            WatchMenu();
            SyncOpen(value?.IsOpen == true);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watching = true;
        WatchMenu();
        // The menu may have opened or closed while the trigger was away.
        SyncOpen(_menu?.IsOpen == true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watching = false;
        WatchMenu();
    }

    private void WatchMenu()
    {
        if (_observedMenu is not null)
        {
            _observedMenu.PropertyChanged -= OnMenuPropertyChanged;
        }
        _observedMenu = _watching ? _menu : null;
        if (_observedMenu is not null)
        {
            _observedMenu.PropertyChanged += OnMenuPropertyChanged;
        }
    }

    public bool IsMenuOpen => _menu?.IsOpen == true;

    private void OnMenuPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MenuSurface.IsOpenProperty)
        {
            SyncOpen(e.GetNewValue<bool>());
        }
    }

    private void SyncOpen(bool open)
    {
        var was = PseudoClasses.Contains(OpenPseudoClass);
        PseudoClasses.Set(OpenPseudoClass, open);
        if (was != open && ControlAutomationPeer.FromElement(this) is ButtonsChevronTriggerAutomationPeer peer)
        {
            peer.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                was ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                open ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ButtonsChevronTriggerAutomationPeer(this);

    private sealed class ButtonsChevronTriggerAutomationPeer : ButtonAutomationPeer, IExpandCollapseProvider
    {
        public ButtonsChevronTriggerAutomationPeer(ButtonsChevronTrigger owner) : base(owner)
        {
        }

        private ButtonsChevronTrigger Trigger => (ButtonsChevronTrigger)Owner;

        public ExpandCollapseState ExpandCollapseState =>
            Trigger.IsMenuOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => true;

        public void Expand()
        {
            if (Trigger._menu is { IsOpen: false } menu)
            {
                menu.Open(Trigger, MenuOpenFocus.First);
            }
        }

        public void Collapse() => Trigger._menu?.Close();
    }
}
