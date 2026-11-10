using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A top-level trigger of a <see cref="MegaMenuMorph"/>: the item's label and, when the item has a panel, a chevron that
/// turns over while the panel is open. Built by the menu, one per item.
/// </summary>
[TemplatePart(RootPart, typeof(Panel))]
[PseudoClasses(OpenPseudoClass, HasPanelPseudoClass, CurrentPseudoClass)]
internal sealed class MegaMenuMorphTrigger : AvaloniaButton
{
    internal const string RootPart = "PART_Root";
    internal const string OpenPseudoClass = ":open";
    internal const string HasPanelPseudoClass = ":has-panel";
    internal const string CurrentPseudoClass = ":current";

    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<MegaMenuMorphTrigger, string?>(nameof(Header));

    public static readonly StyledProperty<bool> HasPanelProperty =
        AvaloniaProperty.Register<MegaMenuMorphTrigger, bool>(nameof(HasPanel));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<MegaMenuMorphTrigger, bool>(nameof(IsOpen));

    public static readonly StyledProperty<bool> IsCurrentProperty =
        AvaloniaProperty.Register<MegaMenuMorphTrigger, bool>(nameof(IsCurrent));

    private LazyFocusRing? _focusRing;

    static MegaMenuMorphTrigger()
    {
        ClipToBoundsProperty.OverrideDefaultValue<MegaMenuMorphTrigger>(false);
    }

    public MegaMenuMorphTrigger(MegaMenuMorph owner, MegaMenuMorphItem item, int index)
    {
        Owner = owner;
        Item = item;
        Index = index;
        ClickMode = ClickMode.Release;
    }

    public MegaMenuMorph Owner { get; }

    public MegaMenuMorphItem Item { get; }

    public int Index { get; set; }

    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public bool HasPanel
    {
        get => GetValue(HasPanelProperty);
        set => SetValue(HasPanelProperty, value);
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public bool IsCurrent
    {
        get => GetValue(IsCurrentProperty);
        set => SetValue(IsCurrentProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(MegaMenuMorphTrigger);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, MegaMenuMorphTokens.Key(MegaMenuMorphTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill));
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart), below: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            PseudoClasses.Set(OpenPseudoClass, IsOpen);
            if (HasPanel)
            {
                ControlAutomationPeer.FromElement(this)?.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                    IsOpen ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
                    IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
            }
        }
        else if (change.Property == HasPanelProperty)
        {
            PseudoClasses.Set(HasPanelPseudoClass, HasPanel);
        }
        else if (change.Property == IsCurrentProperty)
        {
            PseudoClasses.Set(CurrentPseudoClass, IsCurrent);
        }
        else if (change.Property == HeaderProperty)
        {
            AutomationProperties.SetName(this, Header ?? string.Empty);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TriggerPeer(this);

    private sealed class TriggerPeer(MegaMenuMorphTrigger owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        private MegaMenuMorphTrigger Trigger => (MegaMenuMorphTrigger)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() =>
            Trigger.HasPanel ? AutomationControlType.Button : AutomationControlType.Hyperlink;

        public ExpandCollapseState ExpandCollapseState =>
            Trigger.HasPanel ? (Trigger.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed) : ExpandCollapseState.LeafNode;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (Trigger.HasPanel)
            {
                Trigger.Owner.Open(Trigger.Index);
            }
        }

        public void Collapse()
        {
            if (Trigger.IsOpen)
            {
                Trigger.Owner.Close();
            }
        }
    }
}
