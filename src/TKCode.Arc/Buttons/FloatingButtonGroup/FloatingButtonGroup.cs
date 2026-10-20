using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>Surface of a <see cref="FloatingButtonGroup"/> tray.</summary>
public enum FloatingButtonGroupVariant
{
    /// <summary>A quiet translucent tint that still reads on muted surfaces (default).</summary>
    Muted,

    /// <summary>Raised for overlays such as a selection toolbar: the floating shadow and a hairline edge.</summary>
    Floating
}

/// <summary>Arguments of <see cref="FloatingButtonGroup.ActionInvoked"/>.</summary>
public sealed class FloatingButtonGroupActionEventArgs : RoutedEventArgs
{
    public FloatingButtonGroupActionEventArgs(RoutedEvent routedEvent, FloatingButtonGroupItem item, object? data) : base(routedEvent)
    {
        Item = item;
        Data = data;
    }

    /// <summary>The button that ran.</summary>
    public FloatingButtonGroupItem Item { get; }

    /// <summary>The source item (the button itself for inline items, the data object for <c>ItemsSource</c>).</summary>
    public object? Data { get; }
}

/// <summary>
/// Separate soft buttons in a quiet tray for floating editors and canvas toolbars (Undo, Redo, Share, Present). One
/// shared highlight (P7 <see cref="GlidingIndicator"/>) morphs in position and size to whichever button the pointer or
/// keyboard focus is on, holds still across gaps and separators, fades when the pointer leaves the tray, and settles
/// darker in place while a button is pressed; the buttons themselves never scale. Icon-only buttons and shortcut hints
/// show the Arc <see cref="Tooltip"/>. One Tab stop; arrow keys move and wrap, Home and End jump, Enter and Space act.
/// </summary>
[TemplatePart(HighlightPart, typeof(GlidingIndicator))]
[PseudoClasses(PressingPseudoClass, VerticalPseudoClass, SmallPseudoClass)]
public class FloatingButtonGroup : ItemsControl
{
    public const string HighlightPart = "PART_Highlight";
    public const string PressingPseudoClass = ":pressing";
    public const string VerticalPseudoClass = ":vertical";
    public const string SmallPseudoClass = ":small";

    #region 公共属性定义

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<FloatingButtonGroup, string>(nameof(Label), "Actions");

    public static readonly StyledProperty<FloatingButtonGroupVariant> VariantProperty =
        AvaloniaProperty.Register<FloatingButtonGroup, FloatingButtonGroupVariant>(nameof(Variant));

    public static readonly StyledProperty<ControlSize> SizeProperty =
        AvaloniaProperty.Register<FloatingButtonGroup, ControlSize>(nameof(Size), ControlSize.Medium);

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<FloatingButtonGroup, Orientation>(nameof(Orientation), Orientation.Horizontal);

    public static readonly StyledProperty<bool> IsIconOnlyProperty =
        AvaloniaProperty.Register<FloatingButtonGroup, bool>(nameof(IsIconOnly));

    public static readonly StyledProperty<TooltipSide?> TooltipPlacementProperty =
        AvaloniaProperty.Register<FloatingButtonGroup, TooltipSide?>(nameof(TooltipPlacement));

    public static readonly RoutedEvent<FloatingButtonGroupActionEventArgs> ActionInvokedEvent =
        RoutedEvent.Register<FloatingButtonGroup, FloatingButtonGroupActionEventArgs>(nameof(ActionInvoked), RoutingStrategies.Bubble);

    /// <summary>Accessible name of the toolbar, such as "Board actions".</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public FloatingButtonGroupVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>Small (30px buttons) or Medium (36px, default). Large is treated as Medium.</summary>
    public ControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>Shows every item as an icon with its label in a tooltip; items with their own setting win.</summary>
    public bool IsIconOnly { get => GetValue(IsIconOnlyProperty); set => SetValue(IsIconOnlyProperty, value); }

    /// <summary>Side of the item tooltips; null uses Top for a row and Right for a column.</summary>
    public TooltipSide? TooltipPlacement { get => GetValue(TooltipPlacementProperty); set => SetValue(TooltipPlacementProperty, value); }

    /// <summary>Raised after an item's own click handlers and command.</summary>
    public event EventHandler<FloatingButtonGroupActionEventArgs>? ActionInvoked
    {
        add => AddHandler(ActionInvokedEvent, value);
        remove => RemoveHandler(ActionInvokedEvent, value);
    }

    #endregion

    private GlidingIndicator? _highlight;
    private FloatingButtonGroupItem? _hover;
    private FloatingButtonGroupItem? _focus;
    private FloatingButtonGroupItem? _placed;
    private bool _pressing;
    private bool _traveling;

    public FloatingButtonGroup()
    {
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);
        AddHandler(Avalonia.Controls.Button.ClickEvent, OnItemClick);
    }

    static FloatingButtonGroup()
    {
        FocusableProperty.OverrideDefaultValue<FloatingButtonGroup>(false);
    }

    /// <summary>The item that holds the highlight (pointer wins over keyboard focus while it is in the tray).</summary>
    public FloatingButtonGroupItem? ActiveItem => _hover is { IsEffectivelyEnabled: true } ? _hover : _focus;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _highlight = e.NameScope.Find<GlidingIndicator>(HighlightPart);
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        AutomationProperties.SetName(this, Label);
        UpdateLayoutClasses();
    }

    #region 容器

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        if (item is FloatingButtonGroupItem or FloatingButtonGroupSeparator)
        {
            recycleKey = null;
            return false;
        }
        recycleKey = DefaultRecycleKey;
        return true;
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new FloatingButtonGroupItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is FloatingButtonGroupItem button)
        {
            if (!ReferenceEquals(item, button) && string.IsNullOrEmpty(button.Label))
            {
                button.Label = item?.ToString() ?? string.Empty;
            }
            button.Group = this;
            button.PropertyChanged += OnItemPropertyChanged;
            button.SyncFromGroup();
        }
        else if (container is FloatingButtonGroupSeparator separator)
        {
            separator.Sync(Orientation == Orientation.Vertical, IsSmall);
        }
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is FloatingButtonGroupItem button)
        {
            button.PropertyChanged -= OnItemPropertyChanged;
            button.Group = null;
            if (ReferenceEquals(_hover, button))
            {
                _hover = null;
            }
            if (ReferenceEquals(_focus, button))
            {
                _focus = null;
            }
            Place(follow: true);
        }
    }

    private IEnumerable<FloatingButtonGroupItem> Buttons => GetRealizedContainers().OfType<FloatingButtonGroupItem>();

    #endregion

    internal bool IsSmall => Size == ControlSize.Small;

    internal TooltipSide EffectiveTooltipSide => TooltipPlacement ?? (Orientation == Orientation.Vertical ? TooltipSide.Right : TooltipSide.Top);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == SizeProperty || change.Property == OrientationProperty || change.Property == IsIconOnlyProperty ||
                 change.Property == TooltipPlacementProperty)
        {
            UpdateLayoutClasses();
            foreach (var container in GetRealizedContainers())
            {
                if (container is FloatingButtonGroupItem button)
                {
                    button.SyncFromGroup();
                }
                else if (container is FloatingButtonGroupSeparator separator)
                {
                    separator.Sync(Orientation == Orientation.Vertical, IsSmall);
                }
            }
        }
        else if (change.Property == IsKeyboardFocusWithinProperty && !change.GetNewValue<bool>())
        {
            _focus = null;
            SetPressing(false);
            Place(follow: false);
        }
    }

    private void UpdateLayoutClasses()
    {
        PseudoClasses.Set(VerticalPseudoClass, Orientation == Orientation.Vertical);
        PseudoClasses.Set(SmallPseudoClass, IsSmall);
    }

    #region 高亮

    private void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not FloatingButtonGroupItem item)
        {
            return;
        }
        if (e.Property == BoundsProperty && ReferenceEquals(item, _placed))
        {
            // A font that loads or a container that reflows moves the buttons; the highlight follows without travel.
            Place(follow: true);
        }
        else if (e.Property == Avalonia.Controls.Button.IsPressedProperty && ReferenceEquals(item, ActiveItem))
        {
            SetPressing(item.IsPressed);
        }
        else if (e.Property == IsEffectivelyEnabledProperty)
        {
            Place(follow: false);
        }
    }

    private Rect? Measure(FloatingButtonGroupItem item)
    {
        if (_highlight is null || !item.IsVisible)
        {
            return null;
        }
        var origin = item.TranslatePoint(default, _highlight);
        return origin is { } p ? new Rect(p, item.Bounds.Size) : null;
    }

    private static Rect Shrink(Rect rect, double scale)
    {
        var w = rect.Width * scale;
        var h = rect.Height * scale;
        return new Rect(rect.Center.X - w / 2, rect.Center.Y - h / 2, w, h);
    }

    /// <summary>Moves the highlight to the active item: travel on the morph spring, or land in place when appearing.</summary>
    private void Place(bool follow)
    {
        var active = ActiveItem;
        foreach (var button in Buttons)
        {
            button.SetActive(ReferenceEquals(button, active));
        }
        if (_highlight is null)
        {
            return;
        }
        var rect = active is null ? null : Measure(active);
        if (rect is not { } target)
        {
            _placed = null;
            _traveling = false;
            _highlight.Hide(IsLoaded);
            return;
        }
        var full = IsLoaded && ArcMotion.IsFull(this);
        var visible = _highlight.HasTarget;
        var pressed = _pressing ? Shrink(target, 0.965) : target;
        _placed = active;
        if (!visible)
        {
            // Appearing from nothing lands in place and grows from 94%.
            _traveling = false;
            if (full)
            {
                _highlight.MoveTo(Shrink(target, 0.94), animate: true);
                _highlight.MoveTo(pressed, animate: true, ArcSpring.Snappy);
            }
            else
            {
                _highlight.MoveTo(pressed, animate: false);
            }
            return;
        }
        var travel = full && (!follow || _traveling);
        _traveling = travel;
        _highlight.MoveTo(pressed, travel);
    }

    private void SetPressing(bool pressing)
    {
        pressing &= ActiveItem is not null;
        if (_pressing == pressing)
        {
            return;
        }
        _pressing = pressing;
        PseudoClasses.Set(PressingPseudoClass, pressing);
        if (_highlight is not null && _placed is not null && Measure(_placed) is { } rect)
        {
            _highlight.MoveTo(pressing ? Shrink(rect, 0.965) : rect, IsLoaded && ArcMotion.IsFull(this), ArcSpring.Snappy);
        }
    }

    private FloatingButtonGroupItem? ItemAt(Point point)
    {
        foreach (var button in Buttons)
        {
            if (button.IsVisible && button.TranslatePoint(default, this) is { } origin && new Rect(origin, button.Bounds.Size).Contains(point))
            {
                return button;
            }
        }
        return null;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        // Gaps and separators keep the highlight where it is.
        var item = ItemAt(e.GetPosition(this));
        if (item is null || ReferenceEquals(item, _hover))
        {
            return;
        }
        _hover = item;
        Place(follow: false);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (ItemAt(e.GetPosition(this)) is { IsEffectivelyEnabled: true } item && !ReferenceEquals(item, _hover))
        {
            _hover = item;
            Place(follow: false);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = null;
        SetPressing(false);
        Place(follow: false);
    }

    #endregion

    #region 键盘

    internal void OnItemFocused(FloatingButtonGroupItem item, NavigationMethod method)
    {
        KeyboardNavigation.SetTabOnceActiveElement(this, item);
        // Only keyboard focus takes the highlight; a click already has the pointer's.
        _focus = method is NavigationMethod.Tab or NavigationMethod.Directional ? item : null;
        Place(follow: false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        var vertical = Orientation == Orientation.Vertical;
        var all = Buttons.Where(b => b.IsVisible).ToList();
        var index = all.FindIndex(b => b.IsFocused);
        if (index < 0)
        {
            return;
        }
        FloatingButtonGroupItem? target = null;
        var next = vertical ? Key.Down : Key.Right;
        var previous = vertical ? Key.Up : Key.Left;
        if (e.Key == Key.Home)
        {
            target = all.FirstOrDefault(b => b.IsEffectivelyEnabled);
        }
        else if (e.Key == Key.End)
        {
            target = all.LastOrDefault(b => b.IsEffectivelyEnabled);
        }
        else if (e.Key == next || e.Key == previous)
        {
            var step = e.Key == next ? 1 : -1;
            for (var offset = 1; offset <= all.Count; offset++)
            {
                var candidate = all[((index + step * offset) % all.Count + all.Count) % all.Count];
                if (candidate.IsEffectivelyEnabled)
                {
                    target = candidate;
                    break;
                }
            }
        }
        else
        {
            return;
        }
        e.Handled = true;
        if (target is null)
        {
            return;
        }
        // The keyboard takes the highlight back until the pointer moves again.
        _hover = null;
        target.Focus(NavigationMethod.Directional);
    }

    #endregion

    private void OnItemClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is FloatingButtonGroupItem item && ReferenceEquals(item.Group, this))
        {
            var data = ItemFromContainer(item) ?? item;
            RaiseEvent(new FloatingButtonGroupActionEventArgs(ActionInvokedEvent, item, data));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FloatingButtonGroupAutomationPeer(this);

    private sealed class FloatingButtonGroupAutomationPeer : ItemsControlAutomationPeer
    {
        public FloatingButtonGroupAutomationPeer(FloatingButtonGroup owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolBar;
    }
}
