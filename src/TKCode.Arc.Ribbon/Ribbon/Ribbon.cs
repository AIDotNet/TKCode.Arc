using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// An Office-style ribbon: a row of tabs over the selected tab's groups of commands. Each <see cref="RibbonTab"/> holds
/// <see cref="RibbonGroup"/>s; each group holds commands (<see cref="RibbonButton"/>, <see cref="RibbonToggleButton"/>,
/// <see cref="RibbonDropDownButton"/>, <see cref="RibbonSplitButton"/>, <see cref="RibbonSeparator"/> or any control) laid
/// out large, medium or small. When the ribbon is narrower than the groups, they shrink step by step and finally collapse
/// into drop-downs, so every command stays reachable.
/// <para>
/// The selected tab's header carries an accent underline that glides between tabs, and the new tab's groups fade in from
/// the side of travel. Double-click a tab, press Ctrl+F1 or use the button at the end of the tab row to collapse the ribbon
/// to its tab row (<see cref="IsMinimized"/>); a tab then opens its groups over the content until a command runs.
/// </para>
/// <para>
/// Keyboard: ArrowLeft / ArrowRight, Home and End move between tabs (the tab row is one tab stop), ArrowDown enters the
/// groups, ArrowLeft / ArrowRight move between commands and Escape returns to the tab. Every command run from the ribbon,
/// from a collapsed group or from a linked <see cref="QuickAccessToolBar"/> is reported in <see cref="ItemInvoked"/>. Put
/// app-level items (a File menu, a search box) in <see cref="LeftContent"/> and <see cref="RightContent"/>.
/// </para>
/// </summary>
[TemplatePart(TabRowPart, typeof(Control))]
[TemplatePart(TabStripPart, typeof(Panel))]
[TemplatePart(SelectionIndicatorPart, typeof(GlidingIndicator))]
[TemplatePart(ContentAreaPart, typeof(Control))]
[TemplatePart(ContentSlotPart, typeof(Control))]
[TemplatePart(MinimizeButtonPart, typeof(RibbonButton))]
[PseudoClasses(MinimizedPseudoClass, OpenPseudoClass)]
public partial class Ribbon : TemplatedControl
{
    public const string TabRowPart = "PART_TabRow";
    public const string TabStripPart = "PART_TabStrip";
    public const string SelectionIndicatorPart = "PART_SelectionIndicator";
    public const string ContentAreaPart = "PART_ContentArea";
    public const string ContentSlotPart = "PART_ContentSlot";
    public const string MinimizeButtonPart = "PART_MinimizeButton";

    /// <summary>The ribbon is collapsed to its tab row (<see cref="IsMinimized"/>).</summary>
    public const string MinimizedPseudoClass = ":minimized";

    /// <summary>A collapsed ribbon shows the selected tab's groups in its pop-up.</summary>
    public const string OpenPseudoClass = ":open";

    #region 公共属性定义

    /// <summary>Index of the selected tab in <see cref="Tabs"/> (two-way; -1 when there is no visible tab).</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<Ribbon, int>(nameof(SelectedIndex), 0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The selected tab (two-way).</summary>
    public static readonly StyledProperty<RibbonTab?> SelectedTabProperty =
        AvaloniaProperty.Register<Ribbon, RibbonTab?>(nameof(SelectedTab), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the ribbon is collapsed to its tab row (two-way). A tab then opens its groups in a pop-up.</summary>
    public static readonly StyledProperty<bool> IsMinimizedProperty =
        AvaloniaProperty.Register<Ribbon, bool>(nameof(IsMinimized), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Content at the start of the tab row, before the tabs (a File menu, an app button).</summary>
    public static readonly StyledProperty<object?> LeftContentProperty =
        AvaloniaProperty.Register<Ribbon, object?>(nameof(LeftContent));

    /// <summary>Content at the end of the tab row, before the collapse button (a search box, a share button).</summary>
    public static readonly StyledProperty<object?> RightContentProperty =
        AvaloniaProperty.Register<Ribbon, object?>(nameof(RightContent));

    /// <summary>Raised when the selected tab changes (by the user or in code).</summary>
    public static readonly RoutedEvent<RibbonSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<Ribbon, RibbonSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when the user runs a command: a ribbon button (after its own <c>Click</c> and <c>Command</c>), a row of a
    /// drop-down or split button's menu, or a command on a linked <see cref="QuickAccessToolBar"/>.
    /// </summary>
    public static readonly RoutedEvent<RibbonItemInvokedEventArgs> ItemInvokedEvent =
        RoutedEvent.Register<Ribbon, RibbonItemInvokedEventArgs>(nameof(ItemInvoked), RoutingStrategies.Bubble);

    /// <inheritdoc cref="SelectedIndexProperty"/>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <inheritdoc cref="SelectedTabProperty"/>
    public RibbonTab? SelectedTab
    {
        get => GetValue(SelectedTabProperty);
        set => SetValue(SelectedTabProperty, value);
    }

    /// <inheritdoc cref="IsMinimizedProperty"/>
    public bool IsMinimized
    {
        get => GetValue(IsMinimizedProperty);
        set => SetValue(IsMinimizedProperty, value);
    }

    /// <inheritdoc cref="LeftContentProperty"/>
    public object? LeftContent
    {
        get => GetValue(LeftContentProperty);
        set => SetValue(LeftContentProperty, value);
    }

    /// <inheritdoc cref="RightContentProperty"/>
    public object? RightContent
    {
        get => GetValue(RightContentProperty);
        set => SetValue(RightContentProperty, value);
    }

    /// <summary>The tabs, left to right.</summary>
    [Content]
    public AvaloniaList<RibbonTab> Tabs { get; } = new();

    /// <inheritdoc cref="SelectionChangedEvent"/>
    public event EventHandler<RibbonSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <inheritdoc cref="ItemInvokedEvent"/>
    public event EventHandler<RibbonItemInvokedEventArgs>? ItemInvoked
    {
        add => AddHandler(ItemInvokedEvent, value);
        remove => RemoveHandler(ItemInvokedEvent, value);
    }

    #endregion

    private readonly List<RibbonTabHeader> _headers = new();
    private Control? _tabRow;
    private Panel? _tabStrip;
    private GlidingIndicator? _indicator;
    private Control? _contentArea;
    private RibbonVisualSlot? _contentSlot;
    private RibbonButton? _minimizeButton;
    private RibbonTab? _shownTab;
    private bool _syncingSelection;
    private bool _indicatorPlaced;
    private MotionValue<double>? _swap;
    private RibbonTab? _swapTab;
    private double _swapDirection;
    private readonly TranslateTransform _swapTransform = new();

    public Ribbon()
    {
        Tabs.CollectionChanged += OnTabsChanged;
        AddHandler(ContextRequestedEvent, OnContextRequested);
        AddHandler(KeyDownEvent, OnRibbonKeyDown, RoutingStrategies.Bubble);
        AutomationProperties.SetName(this, "Ribbon");
    }

    /// <summary>The tab headers, one per tab (tests).</summary>
    internal IReadOnlyList<RibbonTabHeader> Headers => _headers;

    /// <summary>The selection underline (tests).</summary>
    internal GlidingIndicator? SelectionIndicator => _indicator;

    #region Tabs

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (RibbonTab tab in e.OldItems)
            {
                tab.PropertyChanged -= OnTabPropertyChanged;
                tab.IsSelected = false;
                LogicalChildren.Remove(tab);
            }
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var tab in LogicalChildren.OfType<RibbonTab>().ToArray())
            {
                tab.PropertyChanged -= OnTabPropertyChanged;
                tab.IsSelected = false;
                LogicalChildren.Remove(tab);
            }
            foreach (var tab in Tabs)
            {
                AddTab(tab);
            }
        }
        else if (e.NewItems is not null)
        {
            foreach (RibbonTab tab in e.NewItems)
            {
                AddTab(tab);
            }
        }
        RebuildHeaders();
        if (_shownTab is not null && !Tabs.Contains(_shownTab))
        {
            ShowTab(null, 0);
        }
        CoerceSelection(SelectedTab is { } selected && Tabs.Contains(selected) ? Tabs.IndexOf(selected) : SelectedIndex);
        OnQuickAccessCatalogChanged();
    }

    private void AddTab(RibbonTab tab)
    {
        LogicalChildren.Add(tab);
        tab.PropertyChanged += OnTabPropertyChanged;
    }

    private void OnTabPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not RibbonTab tab)
        {
            return;
        }
        if (e.Property == RibbonTab.HeaderProperty || e.Property == IsVisibleProperty || e.Property == IsEnabledProperty
            || e.Property == RibbonTab.IsContextualProperty)
        {
            HeaderOf(tab)?.Sync();
            if (e.Property == IsVisibleProperty || e.Property == IsEnabledProperty)
            {
                CoerceSelection(SelectedIndex);
            }
            InvalidateArrange();
        }
    }

    private RibbonTabHeader? HeaderOf(RibbonTab? tab)
    {
        foreach (var header in _headers)
        {
            if (ReferenceEquals(header.Tab, tab))
            {
                return header;
            }
        }
        return null;
    }

    private void RebuildHeaders()
    {
        if (_tabStrip is null)
        {
            return;
        }
        _tabStrip.Children.Clear();
        _headers.Clear();
        foreach (var tab in Tabs)
        {
            var header = new RibbonTabHeader(this, tab);
            header.SetSelected(ReferenceEquals(tab, SelectedTab));
            _headers.Add(header);
            _tabStrip.Children.Add(header);
        }
        _indicatorPlaced = false;
        InvalidateArrange();
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_minimizeButton is not null)
        {
            _minimizeButton.Click -= OnMinimizeButtonClick;
        }
        _contentSlot?.SetChild(null);
        base.OnApplyTemplate(e);
        _tabRow = e.NameScope.Find<Control>(TabRowPart);
        _tabStrip = e.NameScope.Find<Panel>(TabStripPart);
        _indicator = e.NameScope.Find<GlidingIndicator>(SelectionIndicatorPart);
        _contentArea = e.NameScope.Find<Control>(ContentAreaPart);
        _contentSlot = e.NameScope.Find<RibbonVisualSlot>(ContentSlotPart);
        _minimizeButton = e.NameScope.Find<RibbonButton>(MinimizeButtonPart);
        if (_minimizeButton is not null)
        {
            _minimizeButton.Click += OnMinimizeButtonClick;
        }
        RebuildHeaders();
        _shownTab = null;
        ShowTab(SelectedTab, 0);
        UpdateMinimizedState();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        UpdateIndicator();
        return size;
    }

    // The underline sits under the selected header's text, on the bottom edge of the tab row.
    private void UpdateIndicator()
    {
        if (_indicator?.GetVisualParent() is not Visual host)
        {
            return;
        }
        var header = HeaderOf(SelectedTab);
        var showing = header is { IsVisible: true } && (!IsMinimized || IsPopupOpen);
        if (!showing || header!.Bounds.Width <= 0 || header.TranslatePoint(default, host) is not { } origin)
        {
            _indicator.Hide(animate: _indicatorPlaced);
            _indicatorPlaced = false;
            return;
        }
        var thickness = Token(RibbonTokenKind.IndicatorThickness, 2.0);
        var width = Math.Max(0, header.Bounds.Width - header.Padding.Left - header.Padding.Right);
        var rect = new Rect(origin.X + header.Padding.Left, host.Bounds.Height - thickness, width, thickness);
        _indicator.MoveTo(rect, animate: _indicatorPlaced);
        _indicatorPlaced = true;
    }

    private T Token<T>(RibbonTokenKind kind, T fallback)
        => this.TryFindResource(RibbonTokens.Key(kind), ActualThemeVariant, out var value) && value is T typed ? typed : fallback;

    #endregion

    #region Selection

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty && !_syncingSelection)
        {
            CoerceSelection(change.GetNewValue<int>());
        }
        else if (change.Property == SelectedTabProperty && !_syncingSelection)
        {
            var tab = change.GetNewValue<RibbonTab?>();
            CoerceSelection(tab is null ? -1 : Tabs.IndexOf(tab));
        }
        else if (change.Property == IsMinimizedProperty)
        {
            UpdateMinimizedState();
        }
    }

    private static bool CanSelect(RibbonTab tab) => tab.IsVisible && tab.IsEnabled;

    /// <summary>Selects the tab at <paramref name="index"/>, or the first selectable tab when that one cannot be selected.</summary>
    private void CoerceSelection(int index)
    {
        var target = -1;
        if (index >= 0 && index < Tabs.Count && CanSelect(Tabs[index]))
        {
            target = index;
        }
        else
        {
            for (var i = 0; i < Tabs.Count; i++)
            {
                if (CanSelect(Tabs[i]))
                {
                    target = i;
                    break;
                }
            }
        }
        Select(target);
    }

    private void Select(int index)
    {
        var oldTab = _shownTabSelected;
        var newTab = index >= 0 ? Tabs[index] : null;
        _syncingSelection = true;
        try
        {
            SetCurrentValue(SelectedIndexProperty, index);
            SetCurrentValue(SelectedTabProperty, newTab);
        }
        finally
        {
            _syncingSelection = false;
        }
        if (ReferenceEquals(oldTab, newTab))
        {
            return;
        }
        _shownTabSelected = newTab;
        if (oldTab is not null)
        {
            oldTab.IsSelected = false;
            foreach (var group in oldTab.Groups)
            {
                group.CloseDropDown();
            }
        }
        if (newTab is not null)
        {
            newTab.IsSelected = true;
        }
        foreach (var header in _headers)
        {
            header.SetSelected(ReferenceEquals(header.Tab, newTab));
        }
        var oldIndex = oldTab is null ? -1 : Tabs.IndexOf(oldTab);
        ShowTab(newTab, oldIndex < 0 || index < 0 ? 0 : Math.Sign(index - oldIndex));
        InvalidateArrange();
        RaiseEvent(new RibbonSelectionChangedEventArgs(SelectionChangedEvent, oldTab, newTab));
    }

    private RibbonTab? _shownTabSelected;

    /// <summary>Puts <paramref name="tab"/> into the command area (or the pop-up of a collapsed ribbon).</summary>
    private void ShowTab(RibbonTab? tab, double direction)
    {
        if (ReferenceEquals(_shownTab, tab))
        {
            return;
        }
        if (_shownTab is not null)
        {
            ResetSwap(_shownTab);
        }
        _shownTab = tab;
        var slot = IsPopupOpen ? _popupSurface?.Slot : IsMinimized ? null : _contentSlot;
        if (slot is null)
        {
            _contentSlot?.SetChild(null);
            return;
        }
        slot.SetChild(tab);
        if (tab is not null && direction != 0)
        {
            PlaySwap(tab, direction);
        }
    }

    // The incoming tab fades in and slides 8px from the side of travel. This is code-driven: the tab is moved into the
    // command area at the moment of selection, and a ControlTheme transition has no "from" value for a newly shown element.
    private void PlaySwap(RibbonTab tab, double direction)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off || !IsLoaded)
        {
            return;
        }
        _swap ??= CreateSwap();
        _swapTab = tab;
        _swapDirection = level == ArcMotionLevel.Full ? direction : 0;
        tab.RenderTransform = _swapTransform;
        _swap.Jump(0);
        ApplySwap(0);
        _ = _swap.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
    }

    private MotionValue<double> CreateSwap()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += ApplySwap;
        return value;
    }

    private void ApplySwap(double progress)
    {
        if (_swapTab is null)
        {
            return;
        }
        _swapTab.Opacity = progress;
        _swapTransform.X = (1 - progress) * 8 * _swapDirection;
        if (progress >= 1)
        {
            ResetSwap(_swapTab);
        }
    }

    private void ResetSwap(RibbonTab tab)
    {
        if (!ReferenceEquals(_swapTab, tab))
        {
            return;
        }
        _swap?.Stop();
        tab.Opacity = 1;
        tab.RenderTransform = null;
        _swapTab = null;
    }

    #endregion
}
