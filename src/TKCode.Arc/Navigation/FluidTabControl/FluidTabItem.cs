using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Mixins;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaSelectingItemsControl = Avalonia.Controls.Primitives.SelectingItemsControl;

namespace TKCode.Arc;

/// <summary>
/// One tab of a <see cref="FluidTabControl"/>: <see cref="HeaderedContentControl.Header"/> (with an optional
/// <see cref="Icon"/>) is shown in the tab list, and <see cref="ContentControl.Content"/> is shown as the tab's section
/// in the control's scroll area, stacked with the sections of the other tabs. Disabled tabs are skipped by the arrow keys
/// and cannot be selected; a hidden tab hides its section too.
/// </summary>
[PseudoClasses(SelectedPseudoClass, PressedPseudoClass, HasIconPseudoClass)]
public class FluidTabItem : HeaderedContentControl, ISelectable
{
    public const string SelectedPseudoClass = ":selected";
    public const string PressedPseudoClass = ":pressed";

    /// <summary>Set while the tab has an <see cref="Icon"/>.</summary>
    public const string HasIconPseudoClass = ":has-icon";

    /// <summary>Whether this tab is the selected one.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaSelectingItemsControl.IsSelectedProperty.AddOwner<FluidTabItem>();

    /// <summary>Optional icon shown before the header in the tab list: a glyph, a control or a value for <see cref="IconTemplate"/>.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<FluidTabItem, object?>(nameof(Icon));

    /// <summary>Template that renders <see cref="Icon"/>.</summary>
    public static readonly StyledProperty<IDataTemplate?> IconTemplateProperty =
        AvaloniaProperty.Register<FluidTabItem, IDataTemplate?>(nameof(IconTemplate));

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <inheritdoc cref="IconProperty"/>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="IconTemplateProperty"/>
    public IDataTemplate? IconTemplate
    {
        get => GetValue(IconTemplateProperty);
        set => SetValue(IconTemplateProperty, value);
    }

    static FluidTabItem()
    {
        SelectableMixin.Attach<FluidTabItem>(IsSelectedProperty);
        PressedMixin.Attach<FluidTabItem>();
        FocusableProperty.OverrideDefaultValue<FluidTabItem>(true);
    }

    private LazyFocusRing? _focusRing;

    private FluidTabControl? Owner => ItemsControl.ItemsControlFromItemContainer(this) as FluidTabControl;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty || change.Property == ContentTemplateProperty || change.Property == IsVisibleProperty)
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
        else if (change.Property == IconProperty)
        {
            PseudoClasses.Set(HasIconPseudoClass, change.NewValue is not null);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Mouse and pen select on press, like Tabs; touch selects on release so a drag can still scroll the list.
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
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            Owner?.OnTabPressed(this);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, FluidTabItemTokens.Key(FluidTabItemTokenKind.FocusRingMargin),
            FluidTabItemTokens.Key(FluidTabItemTokenKind.FocusRingRadius));
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
        Owner?.OnTabFocused(this);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FluidTabItemAutomationPeer(this);

    private sealed class FluidTabItemAutomationPeer : ListItemAutomationPeer
    {
        public FluidTabItemAutomationPeer(FluidTabItem owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;
    }
}
