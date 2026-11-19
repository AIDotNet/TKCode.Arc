using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Metadata;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Localization;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A labelled group of commands on a <see cref="RibbonTab"/> (Clipboard, Font, Arrange). Commands are laid out in columns:
/// a large <see cref="RibbonButton"/> takes a column of its own, medium and small ones stack three to a column, and a
/// <see cref="RibbonSeparator"/> draws a line between columns. Any other control (an Arc <c>Select</c>, a
/// <c>NumberField</c>) stacks like a medium command.
/// <para>
/// When the ribbon gets too narrow its groups shrink one step at a time: large commands become medium, then everything
/// becomes small (each command stops at its <see cref="RibbonButton.MinSize"/>), and finally the group collapses into one
/// button that shows the whole group in a drop-down. Groups with a higher <see cref="ReductionPriority"/> shrink first;
/// groups of equal priority take turns from the right. <see cref="CanCollapse"/> keeps a group out of the last step.
/// </para>
/// <para>
/// <see cref="IsLauncherVisible"/> adds the small button at the end of the header that opens the group's full dialog
/// (<see cref="LauncherClick"/>, <see cref="LauncherCommand"/>).
/// </para>
/// </summary>
[TemplatePart(LayoutPart, typeof(Panel))]
[TemplatePart(ExpandedPart, typeof(Control))]
[TemplatePart(HeaderRowPart, typeof(Control))]
[TemplatePart(ItemsSlotPart, typeof(Control))]
[TemplatePart(CollapsedButtonPart, typeof(RibbonButton))]
[TemplatePart(LauncherPart, typeof(AvaloniaButton))]
[PseudoClasses(CollapsedPseudoClass, OpenPseudoClass)]
public class RibbonGroup : TemplatedControl
{
    public const string LayoutPart = "PART_Layout";
    public const string ExpandedPart = "PART_Expanded";
    public const string HeaderRowPart = "PART_HeaderRow";
    public const string ItemsSlotPart = "PART_ItemsSlot";
    public const string CollapsedButtonPart = "PART_CollapsedButton";
    public const string LauncherPart = "PART_Launcher";

    /// <summary>The group is collapsed into one button.</summary>
    public const string CollapsedPseudoClass = ":collapsed";

    /// <summary>The drop-down of a collapsed group is open.</summary>
    public const string OpenPseudoClass = ":open";

    #region 公共属性定义

    /// <summary>The group's name, shown under its commands and on its button when it is collapsed.</summary>
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<RibbonGroup, string?>(nameof(Header));

    /// <summary>Icon of the button the group collapses into: a 24x24 Lucide <see cref="Avalonia.Media.Geometry"/> or any control (default: an ellipsis).</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<RibbonGroup, object?>(nameof(Icon));

    /// <summary>
    /// Order in which groups shrink when the ribbon is too narrow: a higher value shrinks (all the way) before a lower one.
    /// Groups of the same value (default 0) shrink in turns, from the right.
    /// </summary>
    public static readonly StyledProperty<int> ReductionPriorityProperty =
        AvaloniaProperty.Register<RibbonGroup, int>(nameof(ReductionPriority));

    /// <summary>Whether the group may collapse into one button as the last step (default true).</summary>
    public static readonly StyledProperty<bool> CanCollapseProperty =
        AvaloniaProperty.Register<RibbonGroup, bool>(nameof(CanCollapse), true);

    /// <summary>Shows the launcher button at the end of the header, which opens more options of the group (default false).</summary>
    public static readonly StyledProperty<bool> IsLauncherVisibleProperty =
        AvaloniaProperty.Register<RibbonGroup, bool>(nameof(IsLauncherVisible));

    /// <summary>Command run by the launcher button.</summary>
    public static readonly StyledProperty<ICommand?> LauncherCommandProperty =
        AvaloniaProperty.Register<RibbonGroup, ICommand?>(nameof(LauncherCommand));

    /// <summary>Parameter of <see cref="LauncherCommand"/>.</summary>
    public static readonly StyledProperty<object?> LauncherCommandParameterProperty =
        AvaloniaProperty.Register<RibbonGroup, object?>(nameof(LauncherCommandParameter));

    /// <summary>Whether the group is collapsed into one button now (the ribbon decides from the room it has).</summary>
    public static readonly DirectProperty<RibbonGroup, bool> IsCollapsedProperty =
        AvaloniaProperty.RegisterDirect<RibbonGroup, bool>(nameof(IsCollapsed), o => o.IsCollapsed);

    /// <summary>Whether the drop-down of a collapsed group is open (two-way; ignored while the group is not collapsed).</summary>
    public static readonly StyledProperty<bool> IsDropDownOpenProperty =
        AvaloniaProperty.Register<RibbonGroup, bool>(nameof(IsDropDownOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Raised when the launcher button is clicked.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> LauncherClickEvent =
        RoutedEvent.Register<RibbonGroup, RoutedEventArgs>(nameof(LauncherClick), RoutingStrategies.Bubble);

    /// <inheritdoc cref="HeaderProperty"/>
    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <inheritdoc cref="IconProperty"/>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="ReductionPriorityProperty"/>
    public int ReductionPriority
    {
        get => GetValue(ReductionPriorityProperty);
        set => SetValue(ReductionPriorityProperty, value);
    }

    /// <inheritdoc cref="CanCollapseProperty"/>
    public bool CanCollapse
    {
        get => GetValue(CanCollapseProperty);
        set => SetValue(CanCollapseProperty, value);
    }

    /// <inheritdoc cref="IsLauncherVisibleProperty"/>
    public bool IsLauncherVisible
    {
        get => GetValue(IsLauncherVisibleProperty);
        set => SetValue(IsLauncherVisibleProperty, value);
    }

    /// <inheritdoc cref="LauncherCommandProperty"/>
    public ICommand? LauncherCommand
    {
        get => GetValue(LauncherCommandProperty);
        set => SetValue(LauncherCommandProperty, value);
    }

    /// <inheritdoc cref="LauncherCommandParameterProperty"/>
    public object? LauncherCommandParameter
    {
        get => GetValue(LauncherCommandParameterProperty);
        set => SetValue(LauncherCommandParameterProperty, value);
    }

    /// <inheritdoc cref="IsCollapsedProperty"/>
    public bool IsCollapsed
    {
        get => _isCollapsed;
        private set => SetAndRaise(IsCollapsedProperty, ref _isCollapsed, value);
    }

    /// <inheritdoc cref="IsDropDownOpenProperty"/>
    public bool IsDropDownOpen
    {
        get => GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    /// <summary>
    /// The group's commands: <see cref="RibbonButton"/> and the buttons derived from it, <see cref="RibbonSeparator"/>, or
    /// any control.
    /// </summary>
    [Content]
    public AvaloniaList<Control> Items { get; } = new();

    /// <inheritdoc cref="LauncherClickEvent"/>
    public event EventHandler<RoutedEventArgs>? LauncherClick
    {
        add => AddHandler(LauncherClickEvent, value);
        remove => RemoveHandler(LauncherClickEvent, value);
    }

    #endregion

    /// <summary>Number of shrink steps: 0 as declared, 1 at most medium, 2 at most small, 3 collapsed.</summary>
    internal const int CollapsedLevel = 3;

    private readonly RibbonGroupItemsPanel _itemsPanel = new();
    private bool _isCollapsed;
    private int _level;
    private Border? _edge;
    private RibbonGroupLayout? _layout;
    private Control? _expanded;
    private Control? _headerRow;
    private RibbonVisualSlot? _itemsSlot;
    private RibbonGroupCollapsedButton? _collapsedButton;
    private AvaloniaButton? _launcher;
    private ArcPopup? _popup;
    private RibbonGroupDropDown? _dropDown;
    private bool _focusFirstOnOpen;
    // The pop-up's own IsOpen stays true while it plays its exit, so the group keeps its own flag.
    private bool _dropDownOpen;

    public RibbonGroup()
    {
        LogicalChildren.Add(_itemsPanel);
        Items.CollectionChanged += OnItemsChanged;
    }

    /// <summary>The panel that lays out the commands (tests).</summary>
    internal RibbonGroupItemsPanel ItemsPanel => _itemsPanel;

    /// <summary>The button the group collapses into (tests).</summary>
    internal RibbonButton? CollapsedButton => _collapsedButton;

    /// <summary>The drop-down of the collapsed group (null until it first opens).</summary>
    internal ArcPopup? DropDownPopup => _popup;

    /// <summary>The current shrink step (0 to 3).</summary>
    internal int Level => _level;

    /// <summary>The deepest step this group can take.</summary>
    internal int MaxLevel => CanCollapse ? CollapsedLevel : CollapsedLevel - 1;

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var item in _itemsPanel.Items.ToArray())
            {
                _itemsPanel.Remove(item);
                LogicalChildren.Remove(item);
            }
            AddItems(0, Items);
        }
        else
        {
            if (e.OldItems is not null)
            {
                foreach (Control item in e.OldItems)
                {
                    _itemsPanel.Remove(item);
                    LogicalChildren.Remove(item);
                }
            }
            if (e.NewItems is not null)
            {
                AddItems(e.NewStartingIndex < 0 ? _itemsPanel.Items.Count : e.NewStartingIndex, e.NewItems);
            }
        }
        InvalidateMeasure();
        this.FindLogicalAncestorOfType<Ribbon>()?.OnQuickAccessCatalogChanged();
    }

    private void AddItems(int index, System.Collections.IList items)
    {
        foreach (Control item in items)
        {
            LogicalChildren.Add(item);
            _itemsPanel.Insert(index++, item);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_collapsedButton is not null)
        {
            _collapsedButton.Owner = null;
        }
        if (_launcher is not null)
        {
            _launcher.Click -= OnLauncherClick;
        }
        _itemsSlot?.SetChild(null);
        base.OnApplyTemplate(e);
        _edge = e.NameScope.Find<Border>("PART_Edge");
        _layout = e.NameScope.Find<RibbonGroupLayout>(LayoutPart);
        _expanded = e.NameScope.Find<Control>(ExpandedPart);
        _headerRow = e.NameScope.Find<Control>(HeaderRowPart);
        _itemsSlot = e.NameScope.Find<RibbonVisualSlot>(ItemsSlotPart);
        _collapsedButton = e.NameScope.Find<RibbonGroupCollapsedButton>(CollapsedButtonPart);
        _launcher = e.NameScope.Find<AvaloniaButton>(LauncherPart);
        if (_collapsedButton is not null)
        {
            _collapsedButton.Owner = this;
        }
        if (_launcher is not null)
        {
            _launcher.Click += OnLauncherClick;
        }
        if (!_dropDownOpen)
        {
            _itemsSlot?.SetChild(_itemsPanel);
        }
        _itemsPanel.Bind(RibbonGroupItemsPanel.ContentHeightProperty,
            this.GetResourceObservable(RibbonGroupTokens.Key(RibbonGroupTokenKind.ContentHeight)), BindingPriority.Template);
        _itemsPanel.Bind(RibbonGroupItemsPanel.ColumnSpacingProperty,
            this.GetResourceObservable(RibbonGroupTokens.Key(RibbonGroupTokenKind.ColumnSpacing)), BindingPriority.Template);
        UpdateLauncherText();
        UpdateCollapsedParts();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeaderProperty)
        {
            UpdateLauncherText();
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
            _dropDown?.SetHeader(change.GetNewValue<string?>());
            InvalidateRibbonLayout();
        }
        else if (change.Property == IconProperty || change.Property == CanCollapseProperty
                 || change.Property == ReductionPriorityProperty || change.Property == IsLauncherVisibleProperty)
        {
            InvalidateRibbonLayout();
        }
        else if (change.Property == IsDropDownOpenProperty)
        {
            if (change.GetNewValue<bool>())
            {
                OpenDropDown();
            }
            else
            {
                CloseDropDown();
            }
        }
    }

    private void InvalidateRibbonLayout()
    {
        InvalidateMeasure();
        (this.GetVisualParent() as Layoutable)?.InvalidateMeasure();
    }

    private ArcLanguageManager? _languages;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _languages = ArcLanguageManager.Current;
        if (_languages is not null)
        {
            _languages.Changed += OnLanguageChanged;
        }
        UpdateLauncherText();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_languages is not null)
        {
            _languages.Changed -= OnLanguageChanged;
            _languages = null;
        }
    }

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e) => UpdateLauncherText();

    private void UpdateLauncherText()
    {
        if (_launcher is null)
        {
            return;
        }
        var text = RibbonText.Format(RibbonLangResourceKind.GroupOptions, Header ?? string.Empty);
        AutomationProperties.SetName(_launcher, text);
        Tooltip.SetTip(_launcher, text);
    }

    private void OnLauncherClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(LauncherClickEvent, this));
        var command = LauncherCommand;
        var parameter = LauncherCommandParameter;
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
        }
    }

    /// <summary>The group's width at <paramref name="level"/>, from the natural sizes of its parts.</summary>
    internal double GetWidth(int level)
    {
        ApplyTemplate();
        var chrome = Padding.Left + Padding.Right + (_edge?.BorderThickness.Left ?? 0) + (_edge?.BorderThickness.Right ?? 0);
        if (level >= CollapsedLevel)
        {
            return chrome + (_collapsedButton?.GetNaturalSize(RibbonItemSize.Large).Width ?? 0);
        }
        var header = 0.0;
        if (_headerRow is not null)
        {
            _headerRow.Measure(Size.Infinity);
            header = _headerRow.DesiredSize.Width;
        }
        return chrome + Math.Max(_itemsPanel.ComputeWidth(level), header);
    }

    /// <summary>Moves the group to shrink step <paramref name="level"/> (0 to 3).</summary>
    internal void SetLevel(int level)
    {
        level = Math.Clamp(level, 0, MaxLevel);
        if (_level == level)
        {
            return;
        }
        _level = level;
        var collapsed = level >= CollapsedLevel;
        // A collapsed group shows its commands at their own size in the drop-down.
        _itemsPanel.Level = collapsed ? 0 : level;
        if (IsCollapsed != collapsed)
        {
            IsCollapsed = collapsed;
            PseudoClasses.Set(CollapsedPseudoClass, collapsed);
            if (!collapsed)
            {
                SetCurrentValue(IsDropDownOpenProperty, false);
            }
            UpdateCollapsedParts();
        }
        // Measuring the group with the same constraint would return the cached size: invalidate every element between the
        // commands panel and the group (and the collapsed button's), so the group's new width is measured now.
        InvalidateMeasureUp(_itemsPanel);
        if (_collapsedButton is not null)
        {
            InvalidateMeasureUp(_collapsedButton);
        }
    }

    private void InvalidateMeasureUp(Visual from)
    {
        for (var visual = from; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Layoutable layoutable)
            {
                layoutable.InvalidateMeasure();
            }
            if (ReferenceEquals(visual, this))
            {
                return;
            }
        }
        InvalidateMeasure();
    }

    // The inactive form stays in the tree (so it can still be measured) but is invisible, not hit-testable and out of the
    // tab order; the layout arranges it at zero size.
    private void UpdateCollapsedParts()
    {
        var collapsed = IsCollapsed;
        if (_expanded is not null)
        {
            _expanded.Opacity = collapsed ? 0 : 1;
            _expanded.IsHitTestVisible = !collapsed;
            KeyboardNavigation.SetTabNavigation(_expanded, collapsed ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
            AutomationProperties.SetAccessibilityView(_expanded, collapsed ? AccessibilityView.Raw : AccessibilityView.Content);
        }
        if (_collapsedButton is not null)
        {
            _collapsedButton.Opacity = collapsed ? 1 : 0;
            _collapsedButton.IsHitTestVisible = collapsed;
            _collapsedButton.Focusable = collapsed;
            AutomationProperties.SetAccessibilityView(_collapsedButton, collapsed ? AccessibilityView.Control : AccessibilityView.Raw);
        }
    }

    /// <summary>Opens the drop-down of the collapsed group or closes it (its button's click).</summary>
    internal void ToggleDropDown(bool keyboard)
    {
        if (_dropDownOpen)
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
        }
        else
        {
            _focusFirstOnOpen = keyboard;
            SetCurrentValue(IsDropDownOpenProperty, true);
        }
    }

    private void OpenDropDown()
    {
        if (!IsCollapsed || _collapsedButton is null)
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
            return;
        }
        if (_popup is null)
        {
            _dropDown = new RibbonGroupDropDown();
            _dropDown.AddHandler(KeyDownEvent, OnDropDownKeyDown, RoutingStrategies.Tunnel);
            _popup = new ArcPopup
            {
                Child = _dropDown,
                Placement = PlacementMode.BottomEdgeAlignedLeft,
                EnterOffset = 4,
                MarginToAnchor = 4,
                IsLightDismissEnabled = true
            };
            _popup.Closed += OnPopupClosed;
        }
        _dropDown!.SetHeader(Header);
        _dropDown.Slot.SetChild(_itemsPanel);
        _popup.PlacementTarget = _collapsedButton;
        if (_popup.Parent is null)
        {
            ((ISetLogicalParent)_popup).SetParent(this);
        }
        _dropDownOpen = true;
        _popup.IsOpen = true;
        PseudoClasses.Set(OpenPseudoClass, true);
        _collapsedButton.SetOpen(true);
        if (_focusFirstOnOpen)
        {
            _focusFirstOnOpen = false;
            Dispatcher.UIThread.Post(() => Ribbon.FocusFirstCommand(_itemsPanel), DispatcherPriority.Input);
        }
    }

    /// <summary>Closes the drop-down of the collapsed group.</summary>
    internal void CloseDropDown()
    {
        if (_dropDownOpen)
        {
            _dropDownOpen = false;
            if (_popup is { IsOpen: true })
            {
                _popup.IsOpen = false;
            }
            AfterDropDownClosed();
        }
        SetCurrentValue(IsDropDownOpenProperty, false);
    }

    // Light dismiss closes the pop-up without CloseDropDown.
    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (_dropDownOpen)
        {
            _dropDownOpen = false;
            AfterDropDownClosed();
        }
        if (!_dropDownOpen)
        {
            _itemsSlot?.SetChild(_itemsPanel);
        }
        if (_popup is { IsOpen: false, Parent: not null })
        {
            ((ISetLogicalParent)_popup).SetParent(null);
        }
    }

    private void AfterDropDownClosed()
    {
        // A collapsed group keeps its commands in the pop-up while it fades out; an expanded one needs them back now.
        if (!IsCollapsed || _popup is not { IsOpen: true })
        {
            _itemsSlot?.SetChild(_itemsPanel);
        }
        PseudoClasses.Set(OpenPseudoClass, false);
        _collapsedButton?.SetOpen(false);
        SetCurrentValue(IsDropDownOpenProperty, false);
    }

    private void OnDropDownKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseDropDown();
            _collapsedButton?.Focus(NavigationMethod.Tab);
        }
    }
}
