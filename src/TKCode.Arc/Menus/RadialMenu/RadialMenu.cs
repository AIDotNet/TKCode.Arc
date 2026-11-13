using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Rendering;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A pie menu: commands laid out around a centre point in a full circle or a sector. It opens at the pointer (right
/// press, press and hold, a click, the ContextMenu key or code), and an item is chosen with a click, with the keyboard,
/// or with a marking gesture: hold the button, move toward an item and release (the direction chooses, even beyond the
/// ring; releasing back in the centre cancels). Near the edge of the window the ring shifts so it is never cut off.
/// <para>
/// Attach it to any control with <c>arc:RadialMenu.Menu</c>, or open it from code with <see cref="Open(Control)"/> /
/// <see cref="OpenAt"/> or through <see cref="IsOpen"/> and <see cref="PlacementTarget"/>. Items are declared as
/// <see cref="RadialMenuItem"/> children or generated from <see cref="ItemsControl.ItemsSource"/>; <see cref="ItemInvoked"/>,
/// each item's <see cref="RadialMenuItem.Command"/> and <see cref="ItemInvokedCommand"/> report the choice. A menu placed
/// directly in a layout shows its ring in place, as a static dial.
/// </para>
/// </summary>
[TemplatePart(PlatePart, typeof(Control))]
[TemplatePart(CenterLabelPart, typeof(ArcContentSwap))]
[PseudoClasses(OpenPseudoClass, SectorPseudoClass, GesturePseudoClass, HasHighlightPseudoClass)]
public partial class RadialMenu : ItemsControl, ICustomHitTest
{
    public const string PlatePart = "PART_Plate";
    public const string CenterLabelPart = "PART_CenterLabel";
    public const string OpenPseudoClass = ":open";
    public const string SectorPseudoClass = ":sector";
    public const string GesturePseudoClass = ":gesture";
    public const string HasHighlightPseudoClass = ":has-highlight";

    #region 公共属性定义

    /// <summary>Radius of the centre, where the highlighted item's label shows; it is also the gesture's dead zone (default 44, from the theme).</summary>
    public static readonly StyledProperty<double> InnerRadiusProperty =
        AvaloniaProperty.Register<RadialMenu, double>(nameof(InnerRadius), 44);

    /// <summary>Radius of the sectors' outer edge (default 124, from the theme).</summary>
    public static readonly StyledProperty<double> OuterRadiusProperty =
        AvaloniaProperty.Register<RadialMenu, double>(nameof(OuterRadius), 124);

    /// <summary>
    /// Where the items start, in degrees clockwise from 12 o'clock. In a full circle the first item is centred on this
    /// direction (four items sit at north, east, south and west); in a sector the first item's leading edge sits on it.
    /// </summary>
    public static readonly StyledProperty<double> StartAngleProperty =
        AvaloniaProperty.Register<RadialMenu, double>(nameof(StartAngle));

    /// <summary>The arc the items share, in degrees: 360 (the default) is a full circle, less is a sector. Values are limited to (0, 360].</summary>
    public static readonly StyledProperty<double> SweepAngleProperty =
        AvaloniaProperty.Register<RadialMenu, double>(nameof(SweepAngle), 360);

    /// <summary>The gap between neighbouring sectors, in pixels (the same width all along; default 4, from the theme).</summary>
    public static readonly StyledProperty<double> SectorGapProperty =
        AvaloniaProperty.Register<RadialMenu, double>(nameof(SectorGap), 4);

    /// <summary>Whether the menu is open (two-way). Setting it opens the menu on <see cref="PlacementTarget"/> (or the control it is attached to), placed as <see cref="Placement"/> says.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<RadialMenu, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The control <see cref="IsOpen"/> and <see cref="Open()"/> open the menu on (default: the control the menu is attached to).</summary>
    public static readonly StyledProperty<Control?> PlacementTargetProperty =
        AvaloniaProperty.Register<RadialMenu, Control?>(nameof(PlacementTarget));

    /// <summary>
    /// Where <see cref="IsOpen"/>, <see cref="Open()"/> and the click trigger centre the menu: the target's centre (the
    /// default) or the pointer's last position over it. Right press and hold always open at the pointer, the keyboard
    /// always at the target's centre.
    /// </summary>
    public static readonly StyledProperty<RadialMenuPlacement> PlacementProperty =
        AvaloniaProperty.Register<RadialMenu, RadialMenuPlacement>(nameof(Placement));

    /// <summary>Which gestures open the menu on the control it is attached to (default: right press and press-and-hold). The ContextMenu key and Shift+F10 always do.</summary>
    public static readonly StyledProperty<RadialMenuTrigger> TriggerProperty =
        AvaloniaProperty.Register<RadialMenu, RadialMenuTrigger>(nameof(Trigger), RadialMenuTrigger.RightPress | RadialMenuTrigger.Hold);

    /// <summary>
    /// The highlighted item (two-way): the item under the pointer or at the keyboard position, as the data item or the
    /// declared <see cref="RadialMenuItem"/>. Setting it highlights that item; after a choice it holds the chosen item.
    /// </summary>
    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<RadialMenu, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Runs after any item is chosen, with the item (the data item, or the declared <see cref="RadialMenuItem"/>) as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> ItemInvokedCommandProperty =
        AvaloniaProperty.Register<RadialMenu, ICommand?>(nameof(ItemInvokedCommand));

    /// <summary>The text of a data item: the sector's default label, the centre label and the name read by screen readers.</summary>
    public static readonly StyledProperty<Func<object?, string?>?> TextSelectorProperty =
        AvaloniaProperty.Register<RadialMenu, Func<object?, string?>?>(nameof(TextSelector));

    /// <summary>What the centre shows while no item is highlighted, such as the menu's title or an icon.</summary>
    public static readonly StyledProperty<object?> CenterContentProperty =
        AvaloniaProperty.Register<RadialMenu, object?>(nameof(CenterContent));

    /// <summary>Whether the centre shows the highlighted item's text (default true).</summary>
    public static readonly StyledProperty<bool> IsCenterLabelVisibleProperty =
        AvaloniaProperty.Register<RadialMenu, bool>(nameof(IsCenterLabelVisible), true);

    /// <summary>Whether sectors show their text under the icon (default true). Turn it off for an icon ring whose labels show only in the centre.</summary>
    public static readonly StyledProperty<bool> IsItemLabelVisibleProperty =
        AvaloniaProperty.Register<RadialMenu, bool>(nameof(IsItemLabelVisible), true);

    /// <summary>The menu's accessible name (default "Radial menu").</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<RadialMenu, string?>(nameof(Label), "Radial menu");

    /// <summary>Raised when the user chooses an item (click, release of a gesture, Enter or Space), before the item's command and <see cref="ItemInvokedCommand"/> run and the menu closes.</summary>
    public static readonly RoutedEvent<RadialMenuItemInvokedEventArgs> ItemInvokedEvent =
        RoutedEvent.Register<RadialMenu, RadialMenuItemInvokedEventArgs>(nameof(ItemInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="SelectedItem"/> (the highlighted item) changes, from the pointer, the keyboard or code.</summary>
    public static readonly RoutedEvent<RadialMenuSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<RadialMenu, RadialMenuSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the menu opens.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenedEvent =
        RoutedEvent.Register<RadialMenu, RoutedEventArgs>(nameof(Opened), RoutingStrategies.Direct);

    /// <summary>Raised when the menu has closed, after its exit motion.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClosedEvent =
        RoutedEvent.Register<RadialMenu, RoutedEventArgs>(nameof(Closed), RoutingStrategies.Direct);

    /// <inheritdoc cref="InnerRadiusProperty"/>
    public double InnerRadius
    {
        get => GetValue(InnerRadiusProperty);
        set => SetValue(InnerRadiusProperty, value);
    }

    /// <inheritdoc cref="OuterRadiusProperty"/>
    public double OuterRadius
    {
        get => GetValue(OuterRadiusProperty);
        set => SetValue(OuterRadiusProperty, value);
    }

    /// <inheritdoc cref="StartAngleProperty"/>
    public double StartAngle
    {
        get => GetValue(StartAngleProperty);
        set => SetValue(StartAngleProperty, value);
    }

    /// <inheritdoc cref="SweepAngleProperty"/>
    public double SweepAngle
    {
        get => GetValue(SweepAngleProperty);
        set => SetValue(SweepAngleProperty, value);
    }

    /// <inheritdoc cref="SectorGapProperty"/>
    public double SectorGap
    {
        get => GetValue(SectorGapProperty);
        set => SetValue(SectorGapProperty, value);
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <inheritdoc cref="PlacementTargetProperty"/>
    public Control? PlacementTarget
    {
        get => GetValue(PlacementTargetProperty);
        set => SetValue(PlacementTargetProperty, value);
    }

    /// <inheritdoc cref="PlacementProperty"/>
    public RadialMenuPlacement Placement
    {
        get => GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <inheritdoc cref="TriggerProperty"/>
    public RadialMenuTrigger Trigger
    {
        get => GetValue(TriggerProperty);
        set => SetValue(TriggerProperty, value);
    }

    /// <inheritdoc cref="SelectedItemProperty"/>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <inheritdoc cref="ItemInvokedCommandProperty"/>
    public ICommand? ItemInvokedCommand
    {
        get => GetValue(ItemInvokedCommandProperty);
        set => SetValue(ItemInvokedCommandProperty, value);
    }

    /// <inheritdoc cref="TextSelectorProperty"/>
    public Func<object?, string?>? TextSelector
    {
        get => GetValue(TextSelectorProperty);
        set => SetValue(TextSelectorProperty, value);
    }

    /// <inheritdoc cref="CenterContentProperty"/>
    public object? CenterContent
    {
        get => GetValue(CenterContentProperty);
        set => SetValue(CenterContentProperty, value);
    }

    /// <inheritdoc cref="IsCenterLabelVisibleProperty"/>
    public bool IsCenterLabelVisible
    {
        get => GetValue(IsCenterLabelVisibleProperty);
        set => SetValue(IsCenterLabelVisibleProperty, value);
    }

    /// <inheritdoc cref="IsItemLabelVisibleProperty"/>
    public bool IsItemLabelVisible
    {
        get => GetValue(IsItemLabelVisibleProperty);
        set => SetValue(IsItemLabelVisibleProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Occurs when the user chooses an item.</summary>
    public event EventHandler<RadialMenuItemInvokedEventArgs>? ItemInvoked
    {
        add => AddHandler(ItemInvokedEvent, value);
        remove => RemoveHandler(ItemInvokedEvent, value);
    }

    /// <summary>Occurs when the highlighted item changes.</summary>
    public event EventHandler<RadialMenuSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <summary>Occurs when the menu opens.</summary>
    public event EventHandler<RoutedEventArgs>? Opened
    {
        add => AddHandler(OpenedEvent, value);
        remove => RemoveHandler(OpenedEvent, value);
    }

    /// <summary>Occurs when the menu has closed.</summary>
    public event EventHandler<RoutedEventArgs>? Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    #endregion

    /// <summary>How far the plate reaches past the sectors (theme token).</summary>
    internal static readonly StyledProperty<double> PlatePaddingProperty =
        AvaloniaProperty.Register<RadialMenu, double>(nameof(PlatePadding), 6);

    /// <summary>The least distance kept from the viewport edge when the ring is shifted into view (theme token).</summary>
    internal static readonly StyledProperty<double> ViewportMarginProperty =
        AvaloniaProperty.Register<RadialMenu, double>(nameof(ViewportMargin), 8);

    /// <summary>Distance between the centre label and the edge of the centre (theme token).</summary>
    internal static readonly StyledProperty<double> CenterLabelPaddingProperty =
        AvaloniaProperty.Register<RadialMenu, double>(nameof(CenterLabelPadding), 10);

    internal static readonly DirectProperty<RadialMenu, double> CenterLabelWidthProperty =
        AvaloniaProperty.RegisterDirect<RadialMenu, double>(nameof(CenterLabelWidth), o => o.CenterLabelWidth);

    internal static readonly DirectProperty<RadialMenu, bool> ShowsCenterContentProperty =
        AvaloniaProperty.RegisterDirect<RadialMenu, bool>(nameof(ShowsCenterContent), o => o.ShowsCenterContent);

    private RadialMenuPlate? _plate;
    private ArcContentSwap? _centerLabel;
    private RadialMenuItem? _highlighted;
    private string? _centerText;
    private bool _syncingSelection;
    private double _centerLabelWidth = 68;
    private bool _showsCenterContent = true;

    static RadialMenu()
    {
        ItemsPanelProperty.OverrideDefaultValue<RadialMenu>(new FuncTemplate<Panel?>(() => new RadialMenuPanel()));
        FocusableProperty.OverrideDefaultValue<RadialMenu>(true);
        // Tab enters a menu placed in a layout once (the items are focusable for the roaming keyboard focus).
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<RadialMenu>(KeyboardNavigationMode.Once);
        AffectsMeasure<RadialMenu>(InnerRadiusProperty, OuterRadiusProperty, PlatePaddingProperty);
        MenuProperty.Changed.AddClassHandler<Control>(OnMenuChanged);
    }

    public RadialMenu()
    {
        AutomationProperties.SetName(this, Label);
    }

    internal double PlatePadding => Math.Max(0, GetValue(PlatePaddingProperty));

    internal double ViewportMargin => Math.Max(0, GetValue(ViewportMarginProperty));

    internal double CenterLabelPadding => GetValue(CenterLabelPaddingProperty);

    /// <summary>Width the centre label may use.</summary>
    internal double CenterLabelWidth
    {
        get => _centerLabelWidth;
        private set => SetAndRaise(CenterLabelWidthProperty, ref _centerLabelWidth, value);
    }

    /// <summary>Whether the centre shows <see cref="CenterContent"/> (no label is shown).</summary>
    internal bool ShowsCenterContent
    {
        get => _showsCenterContent;
        private set => SetAndRaise(ShowsCenterContentProperty, ref _showsCenterContent, value);
    }

    /// <summary>The inner radius actually used (never negative, never inside the half gap).</summary>
    internal double EffectiveInnerRadius => Math.Max(Math.Max(0, InnerRadius), Math.Max(0, SectorGap) / 2 + 1);

    /// <summary>The outer radius actually used (at least 8px past the inner radius).</summary>
    internal double EffectiveOuterRadius => Math.Max(OuterRadius, EffectiveInnerRadius + 8);

    /// <summary>Side of the ring's square: the plate's diameter.</summary>
    internal double LayoutSide => 2 * (EffectiveOuterRadius + PlatePadding);

    /// <summary>The highlighted sector.</summary>
    internal RadialMenuItem? HighlightedContainer => _highlighted;

    /// <summary>The text the centre shows (tests).</summary>
    internal string? CenterText => _centerText;

    /// <summary>True when the menu sits in an ordinary layout instead of floating.</summary>
    internal bool IsInline => this.GetVisualParent() is { } parent && parent is not RadialMenuHost;

    #region Template and layout

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _plate = e.NameScope.Find<RadialMenuPlate>(PlatePart);
        _centerLabel = e.NameScope.Find<ArcContentSwap>(CenterLabelPart);
        _centerText = null;
        UpdateCenterLabel();
        _plate?.Refresh();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var side = LayoutSide;
        var size = new Size(side, side);
        base.MeasureOverride(size);
        return size;
    }

    bool ICustomHitTest.HitTest(Point point)
    {
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var distance = RadialMenuLayout.Distance(center, point);
        if (distance <= EffectiveInnerRadius)
        {
            return true;
        }
        var outer = EffectiveOuterRadius + PlatePadding;
        if (distance > outer)
        {
            return false;
        }
        var margin = Math.Asin(Math.Min(1, PlatePadding / outer)) * 180 / Math.PI;
        return RadialMenuLayout.InSweep(RadialMenuLayout.AngleOf(center, point), StartAngle, SweepAngle, margin);
    }

    /// <summary>Called by the panel after it gave every item its sector.</summary>
    internal void OnItemsLaidOut() => _plate?.Refresh();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StartAngleProperty || change.Property == SweepAngleProperty ||
            change.Property == SectorGapProperty || change.Property == IsItemLabelVisibleProperty ||
            change.Property == InnerRadiusProperty || change.Property == OuterRadiusProperty ||
            change.Property == PlatePaddingProperty)
        {
            PseudoClasses.Set(SectorPseudoClass, !RadialMenuLayout.IsFullCircle(SweepAngle));
            ItemsPanelRoot?.InvalidateMeasure();
            _plate?.Refresh();
            UpdateCenterLabelWidth();
            if (_host is not null && _state != PopupState.Closed)
            {
                PlaceHost();
            }
        }
        else if (change.Property == CenterLabelPaddingProperty)
        {
            UpdateCenterLabelWidth();
        }
        else if (change.Property == SelectedItemProperty)
        {
            if (!_syncingSelection)
            {
                HighlightItem(change.NewValue);
            }
            RaiseEvent(new RadialMenuSelectionChangedEventArgs(SelectionChangedEvent, change.OldValue, change.NewValue));
        }
        else if (change.Property == IsCenterLabelVisibleProperty || change.Property == TextSelectorProperty ||
                 change.Property == CenterContentProperty)
        {
            UpdateCenterLabel();
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
        }
        else if (change.Property == IsOpenProperty)
        {
            OnIsOpenChanged(change.GetNewValue<bool>());
        }
        else if (change.Property == PlacementTargetProperty || change.Property == PlacementProperty)
        {
            UpdatePointerTracking();
        }
    }

    private void UpdateCenterLabelWidth() => CenterLabelWidth = Math.Max(0, 2 * (EffectiveInnerRadius - CenterLabelPadding));

    #endregion

    #region Containers

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<RadialMenuItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new RadialMenuItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is not RadialMenuItem menuItem)
        {
            return;
        }
        menuItem.Item = item;
        if (ReferenceEquals(container, item))
        {
            return;
        }
        // Like Avalonia's own containers: only fill what a style or theme of the container has not set.
        if (ItemTemplate is { } template)
        {
            SetIfUnset(menuItem, RadialMenuItem.HeaderProperty, item);
            SetIfUnset(menuItem, RadialMenuItem.HeaderTemplateProperty, template);
        }
        else
        {
            SetIfUnset(menuItem, RadialMenuItem.HeaderProperty, TextSelector?.Invoke(item) ?? item);
        }
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        // SelectedItem set before the items had containers (before the first open) highlights its item once it has one.
        if (_highlighted is null && container is RadialMenuItem menuItem && SelectedItem is { } selected &&
            Equals(ItemOf(menuItem), selected) && menuItem.IsEffectivelyEnabled && menuItem.IsVisible)
        {
            _highlighted = menuItem;
            menuItem.SetHighlighted(true);
            UpdateCenterLabel();
        }
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is not RadialMenuItem menuItem)
        {
            return;
        }
        if (ReferenceEquals(menuItem, _highlighted))
        {
            SetHighlight(null);
        }
        if (ReferenceEquals(menuItem, _pressed))
        {
            ClearPress();
        }
        if (!ReferenceEquals(menuItem.Item, menuItem))
        {
            menuItem.ClearValue(RadialMenuItem.HeaderProperty);
            menuItem.ClearValue(RadialMenuItem.HeaderTemplateProperty);
        }
        menuItem.Item = null;
        if (menuItem.IsKeyboardFocusWithin && !IsInline)
        {
            Focus(NavigationMethod.Pointer);
        }
    }

    private static void SetIfUnset<T>(AvaloniaObject target, StyledProperty<T> property, T value)
    {
        if (!target.IsSet(property))
        {
            target.SetCurrentValue(property, value);
        }
    }

    /// <summary>The item a container shows (the data item, or the declared item itself).</summary>
    internal static object? ItemOf(RadialMenuItem container) => container.Item ?? container;

    /// <summary>The visible sectors in ring order.</summary>
    internal List<RadialMenuItem> VisibleItems()
    {
        var list = new List<RadialMenuItem>();
        if (ItemsPanelRoot is { } panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is RadialMenuItem item && item.IsVisible)
                {
                    list.Add(item);
                }
            }
        }
        return list;
    }

    private RadialMenuItem? ContainerOf(object? item)
    {
        if (item is null)
        {
            return null;
        }
        if (item is RadialMenuItem declared && ReferenceEquals(ItemsControlFromItemContainer(declared), this))
        {
            return declared;
        }
        return ContainerFromItem(item) as RadialMenuItem;
    }

    internal void OnItemAvailabilityChanged(RadialMenuItem item)
    {
        if (ReferenceEquals(item, _highlighted) && (!item.IsEffectivelyEnabled || !item.IsVisible))
        {
            // A hidden or disabled item keeps the selected value but loses the highlight.
            item.SetHighlighted(false);
            _highlighted = null;
            UpdateCenterLabel();
        }
        if (ReferenceEquals(item, _pressed) && !item.IsEffectivelyEnabled)
        {
            ClearPress();
        }
    }

    internal void OnItemTextChanged(RadialMenuItem item)
    {
        if (ReferenceEquals(item, _highlighted))
        {
            UpdateCenterLabel();
        }
    }

    #endregion

    #region Highlight and choice

    /// <summary>Highlights <paramref name="container"/> (null clears) and reports it as <see cref="SelectedItem"/>.</summary>
    internal void SetHighlight(RadialMenuItem? container, bool moveFocus = false)
    {
        if (container is not null && (!container.IsEffectivelyEnabled || !container.IsVisible))
        {
            container = null;
        }
        if (!ReferenceEquals(container, _highlighted))
        {
            _highlighted?.SetHighlighted(false);
            _highlighted = container;
            container?.SetHighlighted(true);
            var item = container is null ? null : ItemOf(container);
            if (!Equals(SelectedItem, item))
            {
                _syncingSelection = true;
                try
                {
                    SetCurrentValue(SelectedItemProperty, item);
                }
                finally
                {
                    _syncingSelection = false;
                }
            }
            UpdateCenterLabel();
        }
        if (moveFocus && container is not null)
        {
            container.Focus(NavigationMethod.Directional);
        }
    }

    private void HighlightItem(object? item)
    {
        var container = ContainerOf(item);
        if (container is not null && (!container.IsEffectivelyEnabled || !container.IsVisible))
        {
            container = null;
        }
        if (ReferenceEquals(container, _highlighted))
        {
            return;
        }
        _highlighted?.SetHighlighted(false);
        _highlighted = container;
        container?.SetHighlighted(true);
        UpdateCenterLabel();
    }

    /// <summary>Chooses <paramref name="container"/>: raises <see cref="ItemInvoked"/>, runs the commands and closes the menu.</summary>
    internal void Invoke(RadialMenuItem container, bool fromKeyboard = false)
    {
        if (!container.IsEffectivelyEnabled || !container.IsVisible || !ReferenceEquals(ItemsControlFromItemContainer(container), this))
        {
            return;
        }
        SetHighlight(container);
        var item = ItemOf(container);
        RaiseEvent(new RadialMenuItemInvokedEventArgs(ItemInvokedEvent, item, container));
        if (container.Command is { } command && command.CanExecute(container.CommandParameter))
        {
            command.Execute(container.CommandParameter);
        }
        if (ItemInvokedCommand is { } menuCommand && menuCommand.CanExecute(item))
        {
            menuCommand.Execute(item);
        }
        if (fromKeyboard)
        {
            _keyboardModality = true;
        }
        CloseCore(returnFocus: true);
    }

    private void UpdateCenterLabel()
    {
        var text = IsCenterLabelVisible && _highlighted is { } item ? item.GetText(TextSelector) : null;
        PseudoClasses.Set(HasHighlightPseudoClass, _highlighted is not null);
        ShowsCenterContent = text is null;
        if (text == _centerText)
        {
            return;
        }
        _centerText = text;
        if (_centerLabel is null)
        {
            return;
        }
        _centerLabel.Content = text is null
            ? null
            : new TextBlock
            {
                Text = text,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 2,
                HorizontalAlignment = HorizontalAlignment.Center
            };
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new RadialMenuAutomationPeer(this);

    private sealed class RadialMenuAutomationPeer : ItemsControlAutomationPeer
    {
        public RadialMenuAutomationPeer(RadialMenu owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Menu;

        protected override string? GetNameCore() => ((RadialMenu)Owner).Label ?? base.GetNameCore();
    }
}
