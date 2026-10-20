using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Mixins;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using AvaloniaSelectingItemsControl = Avalonia.Controls.Primitives.SelectingItemsControl;

namespace TKCode.Arc;

/// <summary>
/// One tab of a <see cref="Tabs"/> control: <see cref="HeaderedContentControl.Header"/> is the trigger label and
/// <see cref="ContentControl.Content"/> the panel shown while the tab is selected. Disabled tabs are skipped by the
/// arrow keys and cannot be selected.
/// </summary>
[PseudoClasses(SelectedPseudoClass, PressedPseudoClass)]
public class TabItem : HeaderedContentControl, ISelectable
{
    public const string SelectedPseudoClass = ":selected";
    public const string PressedPseudoClass = ":pressed";

    /// <summary>Whether this tab is the selected one.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaSelectingItemsControl.IsSelectedProperty.AddOwner<TabItem>();

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    static TabItem()
    {
        SelectableMixin.Attach<TabItem>(IsSelectedProperty);
        PressedMixin.Attach<TabItem>();
        FocusableProperty.OverrideDefaultValue<TabItem>(true);
    }

    private Tabs? Owner => ItemsControl.ItemsControlFromItemContainer(this) as Tabs;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty || change.Property == ContentTemplateProperty)
        {
            Owner?.OnTabContentChanged(this);
        }
        else if (change.Property == HeaderProperty)
        {
            if (change.NewValue is string header)
            {
                AutomationProperties.SetName(this, header);
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Radix activates on mouse down with the primary button; touch activates on release (see below).
        if (e.Handled || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            Owner?.OnTabPressed(this);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Handled || e.Pointer.Type != PointerType.Touch)
        {
            return;
        }
        var point = e.GetPosition(this);
        if (new Rect(Bounds.Size).Contains(point))
        {
            e.Handled = true;
            Owner?.OnTabPressed(this);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Owner?.OnTabFocused(this, e.NavigationMethod);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TabItemAutomationPeer(this);

    private sealed class TabItemAutomationPeer : ListItemAutomationPeer
    {
        public TabItemAutomationPeer(TabItem owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;
    }
}
