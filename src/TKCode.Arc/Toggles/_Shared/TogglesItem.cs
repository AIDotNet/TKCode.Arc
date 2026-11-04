using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Base of the internal item containers of the toggle groups (radio rows, radio cards, segments, billing options).
/// A press is answered on release inside the item (a click), Space / Enter select the focused item, and the owner
/// keeps the selection; the container only mirrors it through <see cref="IsSelected"/> (<c>:selected</c>).
/// </summary>
[PseudoClasses(SelectedPseudoClass, PressedPseudoClass)]
internal abstract class TogglesItem : ContentControl
{
    public const string SelectedPseudoClass = ":selected";
    public const string PressedPseudoClass = ":pressed";

    public static readonly StyledProperty<bool> IsSelectedProperty =
        SelectingItemsControl.IsSelectedProperty.AddOwner<TogglesItem>();

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

    protected SelectingItemsControl? Owner => ItemsControl.ItemsControlFromItemContainer(this) as SelectingItemsControl;

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

    protected virtual void OnIsSelectedChanged()
    {
    }

    /// <summary>Selects this item through its owner (click, Space, Enter, automation).</summary>
    public void Select()
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
    protected virtual (object Margin, object Radius)? FocusRingKeys => null;

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
