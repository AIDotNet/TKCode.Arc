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
            if (_menu is not null)
            {
                _menu.PropertyChanged -= OnMenuPropertyChanged;
            }
            _menu = value;
            DropdownMenu.SetMenu(this, value);
            if (value is not null)
            {
                value.PropertyChanged += OnMenuPropertyChanged;
            }
            SyncOpen(value?.IsOpen == true);
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
