using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Base of the item containers of the toggle groups: <see cref="RadioGroupItem"/>, <see cref="RadioCard"/>,
/// <see cref="ToggleGroupItem"/> and the internal segments and billing options. The group creates one for each item; a
/// press is answered on release inside the item (a click), Space / Enter select the focused item, and the group keeps the
/// selection, which the container mirrors through <see cref="IsSelected"/> (<c>:selected</c>). Only this library derives
/// from it.
/// </summary>
[PseudoClasses(SelectedPseudoClass, PressedPseudoClass)]
public abstract class TogglesItem : ContentControl
{
    /// <summary>Set while the item is selected (pressed in a toggle group).</summary>
    public const string SelectedPseudoClass = ":selected";

    /// <summary>Set while a pointer holds the item down.</summary>
    public const string PressedPseudoClass = ":pressed";

    /// <summary>Whether the item is selected. The group sets it; change the group's selection to change it.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        SelectingItemsControl.IsSelectedProperty.AddOwner<TogglesItem>();

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    private IPointer? _pressPointer;
    private LazyFocusRing? _focusRing;

    static TogglesItem()
    {
        FocusableProperty.OverrideDefaultValue<TogglesItem>(true);
    }

    /// <summary>Only the toggle groups of this library derive item containers from this class.</summary>
    private protected TogglesItem()
    {
    }

    private protected SelectingItemsControl? Owner => ItemsControl.ItemsControlFromItemContainer(this) as SelectingItemsControl;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(SelectedPseudoClass, IsSelected);
            OnIsSelectedChanged();
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
        {
            EndPress();
        }
    }

    private protected virtual void OnIsSelectedChanged()
    {
    }

    /// <summary>Selects this item through its owner (click, Space, Enter, automation).</summary>
    internal void Select()
    {
        if (!IsEffectivelyEnabled || Owner is not { } owner)
        {
            return;
        }
        var index = owner.IndexFromContainer(this);
        if (index >= 0)
        {
            owner.SelectedIndex = index;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !IsEffectivelyEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressPointer = e.Pointer;
        e.Pointer.Capture(this);
        PseudoClasses.Set(PressedPseudoClass, true);
        // Pointer focus never shows the ring (focus-visible stays off).
        Focus(NavigationMethod.Pointer);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressPointer is null || e.Pointer != _pressPointer)
        {
            return;
        }
        var inside = new Rect(Bounds.Size).Contains(e.GetPosition(this));
        EndPress();
        e.Pointer.Capture(null);
        if (inside)
        {
            Select();
        }
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndPress();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && (e.Key == Key.Space || e.Key == Key.Enter) && IsEffectivelyEnabled)
        {
            Select();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    /// <summary>
    /// Resource keys of the item's keyboard focus ring (margin, radius), or null when the template draws its own. The
    /// ring is built the first time the item is keyboard focused, as the last child of the template's root panel.
    /// </summary>
    private protected virtual (object Margin, object Radius)? FocusRingKeys => null;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (FocusRingKeys is not { } keys)
        {
            return;
        }
        _focusRing ??= new LazyFocusRing(this, keys.Margin, keys.Radius);
        _focusRing.Attach(VisualChildren.Count > 0 ? VisualChildren[0] as Panel : null);
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

    private void EndPress()
    {
        _pressPointer = null;
        PseudoClasses.Set(PressedPseudoClass, false);
    }
}
