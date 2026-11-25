using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Mixins;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaSelectingItemsControl = Avalonia.Controls.Primitives.SelectingItemsControl;

namespace TKCode.Arc;

/// <summary>
/// One tab of a <see cref="Tabs"/> control: <see cref="HeaderedContentControl.Header"/> is the trigger label and
/// <see cref="ContentControl.Content"/> the panel shown while the tab is selected. Disabled tabs are skipped by the
/// arrow keys and cannot be selected. A closable tab (<see cref="IsClosable"/>, or every tab with
/// <see cref="Tabs.AreTabsClosable"/>) shows a close button after its header and asks its <see cref="Tabs"/> to close it
/// through <see cref="Tabs.TabCloseRequested"/>.
/// </summary>
[TemplatePart(ItemsPart, typeof(ArcShrinkRow))]
[PseudoClasses(SelectedPseudoClass, PressedPseudoClass, ClosablePseudoClass, CardPseudoClass)]
public class TabItem : HeaderedContentControl, ISelectable
{
    public const string SelectedPseudoClass = ":selected";
    public const string PressedPseudoClass = ":pressed";

    /// <summary>Set while the tab shows a close button.</summary>
    public const string ClosablePseudoClass = ":closable";

    /// <summary>Set while the owning <see cref="Tabs"/> uses <see cref="TabsVariant.Card"/>.</summary>
    public const string CardPseudoClass = ":card";

    /// <summary>The row that holds the header and, on a closable tab, the close button.</summary>
    public const string ItemsPart = "PART_Items";

    /// <summary>Name of the close button, built the first time the tab is closable.</summary>
    public const string CloseButtonPart = "PART_CloseButton";

    /// <summary>Whether this tab is the selected one.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaSelectingItemsControl.IsSelectedProperty.AddOwner<TabItem>();

    /// <summary>
    /// Whether the tab shows a close button (on hover, and on the selected or keyboard-focused tab, like a browser tab) and
    /// can be closed with a middle click or Delete. Closing raises <see cref="Tabs.TabCloseRequested"/>; the app removes the
    /// tab. Default false.
    /// </summary>
    public static readonly StyledProperty<bool> IsClosableProperty =
        AvaloniaProperty.Register<TabItem, bool>(nameof(IsClosable));

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <inheritdoc cref="IsClosableProperty"/>
    public bool IsClosable
    {
        get => GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    static TabItem()
    {
        SelectableMixin.Attach<TabItem>(IsSelectedProperty);
        PressedMixin.Attach<TabItem>();
        FocusableProperty.OverrideDefaultValue<TabItem>(true);
    }

    private LazyFocusRing? _focusRing;
    private ArcShrinkRow? _items;
    private Panel? _closeSlot;
    private TabGlyphButton? _close;
    private bool _ownerClosable;
    private bool _isCard;
    private string _closeLabel = "Close tab";

    private Tabs? Owner => ItemsControl.ItemsControlFromItemContainer(this) as Tabs;

    /// <summary>Whether the tab shows its close button: <see cref="IsClosable"/> or the owner's <see cref="Tabs.AreTabsClosable"/>.</summary>
    internal bool IsEffectivelyClosable => IsClosable || _ownerClosable;

    /// <summary>The close button once the tab has been closable; null before.</summary>
    internal TabGlyphButton? CloseButton => _close;

    /// <summary>Called by <see cref="Tabs"/> when the tab is prepared and when the owner's settings change.</summary>
    internal void ApplyOwnerState(bool closable, bool card, string closeLabel)
    {
        _ownerClosable = closable;
        _closeLabel = closeLabel;
        if (_close is not null)
        {
            AutomationProperties.SetName(_close, closeLabel);
        }
        if (_isCard != card)
        {
            _isCard = card;
            PseudoClasses.Set(CardPseudoClass, card);
            UpdateRingShape();
        }
        UpdateCloseParts();
    }

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
        else if (change.Property == IsClosableProperty || change.Property == IsSelectedProperty || change.Property == IsPointerOverProperty)
        {
            UpdateCloseParts();
        }
    }

    /// <summary>
    /// Builds the close slot the first time the tab is closable and shows the button by state: on hover, and on the
    /// selected or keyboard-focused tab. The slot keeps its room while the button is hidden, so the header never shifts.
    /// </summary>
    private void UpdateCloseParts()
    {
        var closable = IsEffectivelyClosable;
        PseudoClasses.Set(ClosablePseudoClass, closable);
        if (_items is null)
        {
            return;
        }
        if (closable && _closeSlot is null)
        {
            BuildCloseSlot(_items);
        }
        if (_closeSlot is null || _close is null)
        {
            return;
        }
        _closeSlot.IsVisible = closable;
        // Hidden rather than transparent: a hidden button builds its template only once it shows.
        _close.IsVisible = closable && (IsPointerOver || IsSelected || PseudoClasses.Contains(":focus-visible"));
    }

    private void BuildCloseSlot(ArcShrinkRow items)
    {
        _closeSlot = new Panel { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        _closeSlot.Bind(WidthProperty, _closeSlot.GetResourceObservable(TabItemTokens.Key(TabItemTokenKind.CloseButtonSize)));
        _closeSlot.Bind(HeightProperty, _closeSlot.GetResourceObservable(TabItemTokens.Key(TabItemTokenKind.CloseButtonSize)));
        _close = new TabGlyphButton { Name = CloseButtonPart, Glyph = ArcIcons.X, IsVisible = false };
        _close.Classes.Add(TabGlyphButton.CloseClass);
        _close.Click += OnCloseClick;
        AutomationProperties.SetName(_close, _closeLabel);
        _closeSlot.Children.Add(_close);
        items.Children.Add(_closeSlot);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Owner?.RequestClose(this, NavigationMethod.Pointer);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Radix activates on mouse down with the primary button; touch activates on release (see below).
        if (e.Handled || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsLeftButtonPressed && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            Owner?.OnTabPressed(this);
        }
        else if (properties.IsMiddleButtonPressed && IsEffectivelyClosable)
        {
            // A middle click closes on release, like a browser tab.
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Handled)
        {
            return;
        }
        var inside = new Rect(Bounds.Size).Contains(e.GetPosition(this));
        if (e.InitialPressMouseButton == MouseButton.Middle && e.Pointer.Type == PointerType.Mouse)
        {
            if (inside && IsEffectivelyClosable)
            {
                e.Handled = true;
                Owner?.RequestClose(this, NavigationMethod.Pointer);
            }
            return;
        }
        if (e.Pointer.Type != PointerType.Touch)
        {
            return;
        }
        if (inside)
        {
            e.Handled = true;
            Owner?.OnTabPressed(this);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _close?.Click -= OnCloseClick;
        _closeSlot = null;
        _close = null;
        _items = e.NameScope.Find<ArcShrinkRow>(ItemsPart);
        UpdateCloseParts();
        _focusRing ??= new LazyFocusRing(this, TabItemTokens.Key(TabItemTokenKind.FocusRingMargin), TabItemTokens.Key(TabItemTokenKind.FocusRingRadius));
        _focusRing.Attach(AddRing, RemoveRing);
    }

    private void UpdateFocusRing()
    {
        _focusRing?.Update();
        UpdateRingShape();
    }

    // The card variant keeps the ring flush with the tab's bottom edge (the strip clips below it) and follows the card's
    // corners; the pill variant uses the ring's own token bindings.
    private void UpdateRingShape()
    {
        if (_focusRing?.Ring is not { } ring)
        {
            return;
        }
        if (_isCard)
        {
            ring.Bind(MarginProperty, ring.GetResourceObservable(TabItemTokens.Key(TabItemTokenKind.CardFocusRingMargin)), Avalonia.Data.BindingPriority.LocalValue);
            ring.Bind(Border.CornerRadiusProperty, ring.GetResourceObservable(TabItemTokens.Key(TabItemTokenKind.CardFocusRingRadius)), Avalonia.Data.BindingPriority.LocalValue);
        }
        else
        {
            ring.ClearValue(MarginProperty);
            ring.ClearValue(Border.CornerRadiusProperty);
        }
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
        UpdateFocusRing();
        UpdateCloseParts();
        Owner?.OnTabFocused(this, e.NavigationMethod);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        UpdateFocusRing();
        UpdateCloseParts();
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
