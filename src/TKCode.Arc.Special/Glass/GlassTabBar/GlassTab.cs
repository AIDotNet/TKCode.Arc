using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One destination of a <see cref="GlassTabBar"/>: an icon over a short label, with an optional badge on the icon. Write it
/// inline (<c>&lt;arc:GlassTab Content="Home" Icon="{x:Static arc:ArcIcons.House}" /&gt;</c>) or let the bar create it
/// for each item of <c>ItemsSource</c>.
/// </summary>
[TemplatePart(RootPart, typeof(Panel))]
[PseudoClasses(SelectedPseudoClass, UnderLensPseudoClass, CompactPseudoClass, BadgeDotPseudoClass, BadgeCountPseudoClass)]
public class GlassTab : ContentControl, IGlassLensItem
{
    public const string RootPart = "PART_Root";
    public const string SelectedPseudoClass = ":selected";
    public const string UnderLensPseudoClass = ":under-lens";
    public const string CompactPseudoClass = ":compact";
    public const string BadgeDotPseudoClass = ":badge-dot";
    public const string BadgeCountPseudoClass = ":badge-count";

    /// <summary>Whether this tab is the selected one (set by the owning <see cref="GlassTabBar"/>).</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        SelectingItemsControl.IsSelectedProperty.AddOwner<GlassTab>();

    /// <summary>The tab's 24×24 stroke icon (for example <c>ArcIcons.House</c>).</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<GlassTab, Geometry?>(nameof(Icon));

    /// <summary>
    /// A badge on the icon: null shows none, an empty string a dot (something new), any other text a small count pill
    /// ("3", "9+"). It is read out after the tab's name.
    /// </summary>
    public static readonly StyledProperty<string?> BadgeProperty =
        AvaloniaProperty.Register<GlassTab, string?>(nameof(Badge));

    private LazyFocusRing? _focusRing;

    static GlassTab()
    {
        FocusableProperty.OverrideDefaultValue<GlassTab>(true);
    }

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="BadgeProperty"/>
    public string? Badge
    {
        get => GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    /// <summary>True while the lens is over this tab (tests read it).</summary>
    internal bool IsUnderLens => PseudoClasses.Contains(UnderLensPseudoClass);

    /// <summary>Whether the keyboard focus ring shows (tests read it).</summary>
    internal bool IsFocusRingShown => _focusRing?.Ring?.IsVisible == true;

    void IGlassLensItem.SetUnderLens(bool value) => PseudoClasses.Set(UnderLensPseudoClass, value);

    /// <summary>Hides the label (the bar shows icons only).</summary>
    internal void SetCompact(bool compact) => PseudoClasses.Set(CompactPseudoClass, compact);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(SelectedPseudoClass, IsSelected);
        }
        else if (change.Property == BadgeProperty)
        {
            var badge = Badge;
            PseudoClasses.Set(BadgeDotPseudoClass, badge is { Length: 0 });
            PseudoClasses.Set(BadgeCountPseudoClass, badge is { Length: > 0 });
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, GlassTabTokens.Key(GlassTabTokenKind.FocusRingMargin), ArcDesignTokenKind.RadiusPill);
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart), below: true);
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

    protected override AutomationPeer OnCreateAutomationPeer() => new GlassTabAutomationPeer(this);

    private sealed class GlassTabAutomationPeer : ContentControlAutomationPeer, ISelectionItemProvider
    {
        public GlassTabAutomationPeer(GlassTab owner) : base(owner)
        {
        }

        private GlassTab Tab => (GlassTab)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

        protected override string? GetNameCore()
        {
            var name = AutomationProperties.GetName(Tab) is { Length: > 0 } set ? set : Tab.Content as string ?? base.GetNameCore();
            return Tab.Badge switch
            {
                null => name,
                { Length: 0 } => $"{name}, new",
                var count => $"{name}, {count}"
            };
        }

        public bool IsSelected => Tab.IsSelected;

        public ISelectionProvider? SelectionContainer => null;

        public void Select()
        {
            if (ItemsControl.ItemsControlFromItemContainer(Tab) is SelectingItemsControl owner &&
                owner.IndexFromContainer(Tab) is var index and >= 0 && Tab.IsEffectivelyEnabled)
            {
                owner.SelectedIndex = index;
            }
        }

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }
    }
}
