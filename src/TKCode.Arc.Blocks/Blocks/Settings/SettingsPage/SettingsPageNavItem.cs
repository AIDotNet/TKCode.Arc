using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Mixins;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaSelectingItemsControl = Avalonia.Controls.Primitives.SelectingItemsControl;

namespace TKCode.Arc;

/// <summary>
/// A navigation item of a <see cref="SettingsPage"/>, generated for each <see cref="SettingsSection"/>: icon, header,
/// badge and an unsaved-changes dot. Selected on press (mouse) or release (touch); the arrow keys move between items.
/// </summary>
[PseudoClasses(SelectedPseudoClass, PressedPseudoClass, IconPseudoClass, BadgePseudoClass, DirtyPseudoClass)]
public sealed class SettingsPageNavItem : TemplatedControl, ISelectable
{
    public const string SelectedPseudoClass = ":selected";
    public const string PressedPseudoClass = ":pressed";
    public const string IconPseudoClass = ":icon";
    public const string BadgePseudoClass = ":badge";
    public const string DirtyPseudoClass = ":dirty";

    /// <summary>Whether this item's section is the one shown.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaSelectingItemsControl.IsSelectedProperty.AddOwner<SettingsPageNavItem>();

    /// <summary>The section's header.</summary>
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<SettingsPageNavItem, string?>(nameof(Header));

    /// <summary>The section's icon.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SettingsPageNavItem, Geometry?>(nameof(Icon));

    /// <summary>The section's badge.</summary>
    public static readonly StyledProperty<string?> BadgeProperty =
        AvaloniaProperty.Register<SettingsPageNavItem, string?>(nameof(Badge));

    /// <summary>Whether the section has unsaved changes (shows a dot).</summary>
    public static readonly StyledProperty<bool> IsDirtyProperty =
        AvaloniaProperty.Register<SettingsPageNavItem, bool>(nameof(IsDirty));

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <inheritdoc cref="HeaderProperty"/>
    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
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

    /// <inheritdoc cref="IsDirtyProperty"/>
    public bool IsDirty
    {
        get => GetValue(IsDirtyProperty);
        set => SetValue(IsDirtyProperty, value);
    }

    static SettingsPageNavItem()
    {
        SelectableMixin.Attach<SettingsPageNavItem>(IsSelectedProperty);
        PressedMixin.Attach<SettingsPageNavItem>();
        FocusableProperty.OverrideDefaultValue<SettingsPageNavItem>(true);
    }

    private SettingsPage? Owner => ItemsControl.ItemsControlFromItemContainer(this) as SettingsPage;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty)
        {
            PseudoClasses.Set(IconPseudoClass, Icon is not null);
        }
        else if (change.Property == BadgeProperty)
        {
            PseudoClasses.Set(BadgePseudoClass, !string.IsNullOrEmpty(Badge));
        }
        else if (change.Property == IsDirtyProperty)
        {
            PseudoClasses.Set(DirtyPseudoClass, IsDirty);
        }
        if (change.Property == HeaderProperty || change.Property == BadgeProperty || change.Property == IsDirtyProperty)
        {
            UpdateAccessibleName();
        }
    }

    /// <summary>What the dot on a section with unsaved changes means, read after the header (from the page's labels).</summary>
    internal string? UnsavedText
    {
        get => _unsavedText;
        set
        {
            _unsavedText = value;
            UpdateAccessibleName();
        }
    }

    private string? _unsavedText;

    private void UpdateAccessibleName()
    {
        var name = Header ?? string.Empty;
        if (!string.IsNullOrEmpty(Badge))
        {
            name += ", " + Badge;
        }
        if (IsDirty && !string.IsNullOrEmpty(_unsavedText))
        {
            name += ", " + _unsavedText;
        }
        AutomationProperties.SetName(this, name);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Like tabs: the mouse selects on press; touch waits for the release so a scroll is not a selection.
        if (e.Handled || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            Owner?.OnNavItemPressed(this);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Handled || e.Pointer.Type != PointerType.Touch)
        {
            return;
        }
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            Owner?.OnNavItemPressed(this);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Owner?.OnNavItemFocused(this, e.NavigationMethod);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SettingsPageNavItemAutomationPeer(this);

    private sealed class SettingsPageNavItemAutomationPeer : ControlAutomationPeer, ISelectionItemProvider
    {
        public SettingsPageNavItemAutomationPeer(SettingsPageNavItem owner) : base(owner)
        {
        }

        private SettingsPageNavItem Item => (SettingsPageNavItem)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

        protected override string? GetNameCore() => AutomationProperties.GetName(Item) ?? Item.Header;

        public bool IsSelected => Item.IsSelected;

        public ISelectionProvider? SelectionContainer => null;

        public void Select() => Item.Owner?.OnNavItemPressed(Item);

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }
    }
}
