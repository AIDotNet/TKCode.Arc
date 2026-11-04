using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Mixins;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
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

    private LazyFocusRing? _focusRing;

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

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, TabItemTokens.Key(TabItemTokenKind.FocusRingMargin), TabItemTokens.Key(TabItemTokenKind.FocusRingRadius));
        _focusRing.Attach(AddRing, RemoveRing);
    }

    // The ring is a logical child (for its token lookups) and the last visual child, so it draws over the frame.
    private void AddRing(Control ring)
    {
        LogicalChildren.Add(ring);
        VisualChildren.Add(ring);
        InvalidateMeasure();
    }

    private void RemoveRing(Control ring)
    {
        VisualChildren.Remove(ring);
        LogicalChildren.Remove(ring);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        // The template lays out only its root; the ring follows the tab's box and never changes its size.
        _focusRing?.Ring?.Measure(availableSize);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        _focusRing?.Ring?.Arrange(new Rect(size));
        return size;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
        Owner?.OnTabFocused(this, e.NavigationMethod);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
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
