using System.Collections.Specialized;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>Where the tab list of a <see cref="FluidTabControl"/> sits next to its sections.</summary>
public enum FluidTabPlacement
{
    /// <summary>The tab list is on the left and the sections on the right.</summary>
    Left,

    /// <summary>The tab list is on the right and the sections on the left.</summary>
    Right
}

/// <summary>Which line of the scroll area decides the current section of a <see cref="FluidTabControl"/>.</summary>
public enum FluidTabSnapAlignment
{
    /// <summary>The top of the scroll area: a selected section scrolls to the top, and the section at the top is the current one.</summary>
    Top,

    /// <summary>The middle of the scroll area: a selected section scrolls to the middle, and the section across the middle is the current one.</summary>
    Center
}

/// <summary>
/// A tab list beside one scroll area that holds the content of every tab, stacked in order (scroll-linked tabs, or a
/// scroll spy). Selecting a tab scrolls its section into place; scrolling the content selects the tab of the section that
/// reaches the anchor line (<see cref="SnapAlignment"/>). Use it for settings pages, long forms and documents where the
/// person reads straight through but also jumps between sections.
/// <para>
/// Items are <see cref="FluidTabItem"/> containers written inline, or are generated from
/// <see cref="ItemsControl.ItemsSource"/>: <see cref="ItemsControl.ItemTemplate"/> renders the tab,
/// <see cref="IconTemplate"/> its icon and <see cref="ContentTemplate"/> its section.
/// </para>
/// </summary>
[TemplatePart(TabListPart, typeof(Border))]
[TemplatePart(TabListViewportPart, typeof(ScrollViewer))]
[TemplatePart(TabListPanelPart, typeof(Panel))]
[TemplatePart(SelectionHighlightPart, typeof(GlidingIndicator))]
[TemplatePart(SelectionIndicatorPart, typeof(GlidingIndicator))]
[TemplatePart(ContentViewportPart, typeof(ScrollViewer))]
[TemplatePart(SectionsPanelPart, typeof(Panel))]
[PseudoClasses(LeftPseudoClass, RightPseudoClass)]
public class FluidTabControl : SelectingItemsControl
{
    public const string TabListPart = "PART_TabList";
    public const string TabListViewportPart = "PART_TabListViewport";
    public const string TabListPanelPart = "PART_TabListPanel";
    public const string SelectionHighlightPart = "PART_SelectionHighlight";
    public const string SelectionIndicatorPart = "PART_SelectionIndicator";
    public const string ContentViewportPart = "PART_ContentViewport";
    public const string SectionsPanelPart = "PART_SectionsPanel";

    /// <summary>Set while the tab list is on the left (<see cref="FluidTabPlacement.Left"/>).</summary>
    public const string LeftPseudoClass = ":left";

    /// <summary>Set while the tab list is on the right (<see cref="FluidTabPlacement.Right"/>).</summary>
    public const string RightPseudoClass = ":right";

    #region Properties

    /// <summary>Which side the tab list is on.</summary>
    public static readonly StyledProperty<FluidTabPlacement> TabPlacementProperty =
        AvaloniaProperty.Register<FluidTabControl, FluidTabPlacement>(nameof(TabPlacement));

    /// <summary>The line of the scroll area that a selected section scrolls to, and that decides the current section while scrolling.</summary>
    public static readonly StyledProperty<FluidTabSnapAlignment> SnapAlignmentProperty =
        AvaloniaProperty.Register<FluidTabControl, FluidTabSnapAlignment>(nameof(SnapAlignment));

    /// <summary>Template for the section of a data item (the tab uses <see cref="ItemsControl.ItemTemplate"/>).</summary>
    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        AvaloniaProperty.Register<FluidTabControl, IDataTemplate?>(nameof(ContentTemplate));

    /// <summary>Template for the icon of a data item's tab; the item itself is the icon's data. Without it, data items have no icon.</summary>
    public static readonly StyledProperty<IDataTemplate?> IconTemplateProperty =
        AvaloniaProperty.Register<FluidTabControl, IDataTemplate?>(nameof(IconTemplate));

    /// <summary>Length of the smooth scroll to a selected section. Zero jumps.</summary>
    public static readonly StyledProperty<TimeSpan> AnimationDurationProperty =
        AvaloniaProperty.Register<FluidTabControl, TimeSpan>(nameof(AnimationDuration), TimeSpan.FromMilliseconds(480));

    /// <summary>Easing of the smooth scroll to a selected section.</summary>
    public static readonly StyledProperty<Easing> EasingFunctionProperty =
        AvaloniaProperty.Register<FluidTabControl, Easing>(nameof(EasingFunction), ArcEasings.InOut);

    /// <summary>Whether selecting a tab scrolls smoothly. When false, or when the motion level is below full, the scroll jumps.</summary>
    public static readonly StyledProperty<bool> IsAnimationEnabledProperty =
        AvaloniaProperty.Register<FluidTabControl, bool>(nameof(IsAnimationEnabled), true);

    /// <summary>Width of the tab list.</summary>
    public static readonly StyledProperty<double> TabListWidthProperty =
        AvaloniaProperty.Register<FluidTabControl, double>(nameof(TabListWidth), 200);

    /// <summary>Space between two sections in the scroll area.</summary>
    public static readonly StyledProperty<double> SectionSpacingProperty =
        AvaloniaProperty.Register<FluidTabControl, double>(nameof(SectionSpacing), 40);

    /// <summary>Padding around the stacked sections inside the scroll area.</summary>
    public static readonly StyledProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.Register<FluidTabControl, Thickness>(nameof(ContentPadding));

    /// <inheritdoc cref="TabPlacementProperty"/>
    public FluidTabPlacement TabPlacement
    {
        get => GetValue(TabPlacementProperty);
        set => SetValue(TabPlacementProperty, value);
    }

    /// <inheritdoc cref="SnapAlignmentProperty"/>
    public FluidTabSnapAlignment SnapAlignment
    {
        get => GetValue(SnapAlignmentProperty);
        set => SetValue(SnapAlignmentProperty, value);
    }

    /// <inheritdoc cref="ContentTemplateProperty"/>
    public IDataTemplate? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    /// <inheritdoc cref="IconTemplateProperty"/>
    public IDataTemplate? IconTemplate
    {
        get => GetValue(IconTemplateProperty);
        set => SetValue(IconTemplateProperty, value);
    }

    /// <inheritdoc cref="AnimationDurationProperty"/>
    public TimeSpan AnimationDuration
    {
        get => GetValue(AnimationDurationProperty);
        set => SetValue(AnimationDurationProperty, value);
    }

    /// <inheritdoc cref="EasingFunctionProperty"/>
    public Easing EasingFunction
    {
        get => GetValue(EasingFunctionProperty);
        set => SetValue(EasingFunctionProperty, value);
    }

    /// <inheritdoc cref="IsAnimationEnabledProperty"/>
    public bool IsAnimationEnabled
    {
        get => GetValue(IsAnimationEnabledProperty);
        set => SetValue(IsAnimationEnabledProperty, value);
    }

    /// <inheritdoc cref="TabListWidthProperty"/>
    public double TabListWidth
    {
        get => GetValue(TabListWidthProperty);
        set => SetValue(TabListWidthProperty, value);
    }

    /// <inheritdoc cref="SectionSpacingProperty"/>
    public double SectionSpacing
    {
        get => GetValue(SectionSpacingProperty);
        set => SetValue(SectionSpacingProperty, value);
    }

    /// <inheritdoc cref="ContentPaddingProperty"/>
    public Thickness ContentPadding
    {
        get => GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    #endregion

    private readonly List<ContentPresenter> _sections = new();
    private ScrollViewer? _listViewport;
    private FluidTabListPanel? _listPanel;
    private GlidingIndicator? _highlight;
    private GlidingIndicator? _indicator;
    private ScrollViewer? _contentViewport;
    private Panel? _sectionsPanel;
    private MotionValue<double>? _contentScroll;
    private MotionValue<double>? _listScroll;
    private int _scrollIndex = -1;
    private int _pendingIndex = -1;
    private bool _pendingAnimate;
    private bool _layoutHooked;
    private bool _syncingFromScroll;
    private double _writtenOffset = double.NaN;
    private int _focusIndex = -1;
    private Rect _highlightTarget;

    static FluidTabControl()
    {
        SelectionModeProperty.OverrideDefaultValue<FluidTabControl>(SelectionMode.AlwaysSelected);
        FocusableProperty.OverrideDefaultValue<FluidTabControl>(false);
        // The control scrolls the content to the selected section and reveals the tab in its own list; the stock
        // scroll-into-view request would bubble and scroll any page the control sits in.
        AutoScrollToSelectedItemProperty.OverrideDefaultValue<FluidTabControl>(false);
    }

    public FluidTabControl()
    {
        PseudoClasses.Set(LeftPseudoClass, true);
        ItemsView.CollectionChanged += OnItemsViewChanged;
    }

    /// <summary>
    /// Selects the tab at <paramref name="index"/> and scrolls its section into place, when the tab exists and is enabled.
    /// Selecting the tab that is already selected scrolls its section back into place.
    /// </summary>
    public bool SelectTab(int index)
    {
        if (index < 0 || index >= ItemCount || !IsTabEnabled(index))
        {
            return false;
        }
        if (index == SelectedIndex)
        {
            ScrollToSection(index, animate: true);
        }
        else
        {
            SelectedIndex = index;
        }
        return true;
    }

    /// <summary>The presenter showing the section of the item at <paramref name="index"/> (tests).</summary>
    internal ContentPresenter? SectionPresenter(int index) => index >= 0 && index < _sections.Count ? _sections[index] : null;

    /// <summary>The number of section presenters (tests).</summary>
    internal int SectionCount => _sections.Count;

    /// <summary>Whether a smooth scroll to a section is running (tests).</summary>
    internal bool IsScrollAnimating => _contentScroll is { IsAnimating: true };

    #region Containers

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new FluidTabItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<FluidTabItem>(item, out recycleKey);

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is FluidTabItem tab && !ReferenceEquals(container, item))
        {
            if (IconTemplate is { } template && item is not null)
            {
                if (!tab.IsSet(FluidTabItem.IconProperty))
                {
                    tab.Icon = item;
                }
                if (!tab.IsSet(FluidTabItem.IconTemplateProperty))
                {
                    tab.IconTemplate = template;
                }
            }
        }
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        UpdateTabStops();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is FluidTabItem tab)
        {
            tab.ClearValue(FluidTabItem.IconProperty);
            tab.ClearValue(FluidTabItem.IconTemplateProperty);
        }
        UpdateTabStops();
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_listPanel is not null)
        {
            _listPanel.Arranged = null;
        }
        if (_listViewport is not null)
        {
            _listViewport.RemoveHandler(PointerWheelChangedEvent, OnListWheel);
        }
        if (_contentViewport is not null)
        {
            _contentViewport.ScrollChanged -= OnContentScrollChanged;
            _contentViewport.RemoveHandler(PointerWheelChangedEvent, OnContentUserInput);
            _contentViewport.RemoveHandler(PointerPressedEvent, OnContentUserInput);
            _contentViewport.RemoveHandler(KeyDownEvent, OnContentUserInput);
        }
        DetachSections();
        _contentScroll?.Stop();
        _listScroll?.Stop();

        base.OnApplyTemplate(e);

        _listViewport = e.NameScope.Find<ScrollViewer>(TabListViewportPart);
        _listPanel = e.NameScope.Find<FluidTabListPanel>(TabListPanelPart);
        _highlight = e.NameScope.Find<GlidingIndicator>(SelectionHighlightPart);
        _indicator = e.NameScope.Find<GlidingIndicator>(SelectionIndicatorPart);
        _contentViewport = e.NameScope.Find<ScrollViewer>(ContentViewportPart);
        _sectionsPanel = e.NameScope.Find<Panel>(SectionsPanelPart);
        _highlightTarget = default;
        _writtenOffset = double.NaN;

        if (_listPanel is not null)
        {
            _listPanel.Arranged = OnListArranged;
        }
        if (_listViewport is not null)
        {
            _listViewport.AddHandler(PointerWheelChangedEvent, OnListWheel, RoutingStrategies.Tunnel);
        }
        if (_contentViewport is not null)
        {
            _contentViewport.ScrollChanged += OnContentScrollChanged;
            // Any scrolling by the person (wheel, touchpad, scroll bar, keys) takes over from a running smooth scroll.
            _contentViewport.AddHandler(PointerWheelChangedEvent, OnContentUserInput, RoutingStrategies.Tunnel, handledEventsToo: true);
            _contentViewport.AddHandler(PointerPressedEvent, OnContentUserInput, RoutingStrategies.Tunnel, handledEventsToo: true);
            _contentViewport.AddHandler(KeyDownEvent, OnContentUserInput, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
        SyncSections();
        if (SelectedIndex > 0)
        {
            // The selected section starts in place, without motion.
            ScrollToSection(SelectedIndex, animate: false);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _contentScroll?.Stop();
        _listScroll?.Stop();
        UnhookLayout();
    }

    #endregion

    #region Sections

    private void OnItemsViewChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncSections();
        UpdateTabStops();
    }

    /// <summary>Called by a <see cref="FluidTabItem"/> when its content, template or visibility changes.</summary>
    internal void OnTabContentChanged(FluidTabItem tab) => SyncSections();

    /// <summary>
    /// Keeps one presenter per item, in item order. A presenter whose content changes is emptied first (template, then
    /// content) and filled in a second pass (content, then template), so a control moving between sections never has two
    /// parents and a data template is never built with null.
    /// </summary>
    private void SyncSections()
    {
        if (_sectionsPanel is null)
        {
            return;
        }
        var count = ItemCount;
        for (var i = 0; i < _sections.Count; i++)
        {
            var content = i < count ? Describe(i).Content : null;
            if (!ReferenceEquals(_sections[i].Content, content))
            {
                Clear(_sections[i]);
            }
        }
        while (_sections.Count > count)
        {
            var last = _sections[^1];
            _sections.RemoveAt(_sections.Count - 1);
            _sectionsPanel.Children.Remove(last);
        }
        while (_sections.Count < count)
        {
            var presenter = new ContentPresenter();
            _sections.Add(presenter);
            _sectionsPanel.Children.Add(presenter);
        }
        for (var i = 0; i < count; i++)
        {
            var (content, template, visible) = Describe(i);
            var presenter = _sections[i];
            presenter.IsVisible = visible;
            if (content is null)
            {
                continue;
            }
            if (presenter.Content is null)
            {
                presenter.Content = content;
            }
            if (!ReferenceEquals(presenter.ContentTemplate, template))
            {
                presenter.ContentTemplate = template;
            }
        }
    }

    private void DetachSections()
    {
        foreach (var presenter in _sections)
        {
            Clear(presenter);
            _sectionsPanel?.Children.Remove(presenter);
        }
        _sections.Clear();
    }

    private static void Clear(ContentPresenter presenter)
    {
        presenter.ContentTemplate = null;
        presenter.Content = null;
    }

    private (object? Content, IDataTemplate? Template, bool Visible) Describe(int index)
    {
        var item = ItemsView[index];
        if (item is FluidTabItem tab)
        {
            return (tab.Content, tab.ContentTemplate ?? ContentTemplate, tab.IsVisible);
        }
        return (item, ContentTemplate, true);
    }

    private bool IsSectionShown(int index)
        => index >= 0 && index < _sections.Count && _sections[index].IsVisible;

    /// <summary>Top of a section in the scroll area's content (its offset when it sits at the top).</summary>
    private double SectionTop(int index) => _sections[index].Bounds.Top + (_sectionsPanel?.Margin.Top ?? 0);

    #endregion

    #region Selection

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty)
        {
            var index = change.GetNewValue<int>();
            _focusIndex = index;
            UpdateTabStops();
            UpdateIndicator(animate: true);
            RevealTab(index);
            if (!_syncingFromScroll && index >= 0)
            {
                ScrollToSection(index, animate: true);
            }
        }
        else if (change.Property == ContentTemplateProperty)
        {
            SyncSections();
        }
        else if (change.Property == TabPlacementProperty)
        {
            var right = change.GetNewValue<FluidTabPlacement>() == FluidTabPlacement.Right;
            PseudoClasses.Set(LeftPseudoClass, !right);
            PseudoClasses.Set(RightPseudoClass, right);
            // The indicator changes sides; the highlight keeps its place in the list.
            UpdateIndicator(animate: true);
        }
        else if (change.Property == SnapAlignmentProperty)
        {
            if (SelectedIndex >= 0)
            {
                ScrollToSection(SelectedIndex, animate: true);
            }
        }
    }

    /// <summary>Called by a <see cref="FluidTabItem"/> when it is pressed.</summary>
    internal void OnTabPressed(FluidTabItem tab)
    {
        var index = IndexFromContainer(tab);
        if (index >= 0 && tab.IsEffectivelyEnabled)
        {
            _focusIndex = index;
            SelectTab(index);
        }
    }

    /// <summary>Called by a <see cref="FluidTabItem"/> when it receives focus.</summary>
    internal void OnTabFocused(FluidTabItem tab)
    {
        var index = IndexFromContainer(tab);
        if (index < 0)
        {
            return;
        }
        _focusIndex = index;
        UpdateTabStops();
        RevealTab(index);
    }

    private bool IsTabEnabled(int index)
        => ContainerFromIndex(index) is not { } container || (container.IsEffectivelyEnabled && container.IsVisible);

    private void UpdateTabStops()
    {
        var count = ItemCount;
        var stop = _focusIndex >= 0 && _focusIndex < count ? _focusIndex : SelectedIndex;
        for (var i = 0; i < count; i++)
        {
            if (ContainerFromIndex(i) is FluidTabItem tab)
            {
                tab.IsTabStop = i == stop || (stop < 0 && i == 0);
            }
        }
    }

    #endregion

    #region Keyboard

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || e.Source is not Visual source || FindTab(source) is not { } tab)
        {
            base.OnKeyDown(e);
            return;
        }
        var current = IndexFromContainer(tab);
        var target = e.Key switch
        {
            Key.Up => NextEnabled(current - 1, -1),
            Key.Down => NextEnabled(current + 1, 1),
            Key.Home => NextEnabled(0, 1),
            Key.End => NextEnabled(ItemCount - 1, -1),
            _ => -2
        };
        if (target >= 0)
        {
            e.Handled = true;
            if (ContainerFromIndex(target) is FluidTabItem next)
            {
                _focusIndex = target;
                UpdateTabStops();
                next.Focus(NavigationMethod.Directional);
            }
            SelectTab(target);
            return;
        }
        if (target == -1)
        {
            // An arrow at the end of the list stays there.
            e.Handled = true;
            return;
        }
        if ((e.Key == Key.Enter || e.Key == Key.Space) && current >= 0)
        {
            e.Handled = true;
            SelectTab(current);
            return;
        }
        base.OnKeyDown(e);
    }

    private FluidTabItem? FindTab(Visual source)
    {
        for (Visual? v = source; v is not null && v != this; v = v.GetVisualParent())
        {
            if (v is FluidTabItem tab && IndexFromContainer(tab) >= 0)
            {
                return tab;
            }
            if (ReferenceEquals(v, _contentViewport))
            {
                // Keys in a section belong to the controls in it.
                return null;
            }
        }
        return null;
    }

    private int NextEnabled(int start, int step)
    {
        for (var i = start; i >= 0 && i < ItemCount; i += step)
        {
            if (IsTabEnabled(i))
            {
                return i;
            }
        }
        return -1;
    }

    #endregion

    #region Content scrolling

    /// <summary>
    /// Scrolls the content so the section at <paramref name="index"/> sits on the anchor line. Runs after layout when the
    /// sections are not laid out yet (first show, items just added or removed).
    /// </summary>
    private void ScrollToSection(int index, bool animate)
    {
        if (_contentViewport is null || _sectionsPanel is null || !IsSectionShown(index)
            || !_sectionsPanel.IsArrangeValid || !_contentViewport.IsArrangeValid)
        {
            _pendingIndex = index;
            _pendingAnimate = animate;
            HookLayout();
            return;
        }
        _pendingIndex = -1;
        _scrollIndex = index;
        var target = TargetOffset(index);
        var offset = _contentViewport.Offset.Y;
        if (_contentScroll is { IsAnimating: true } running)
        {
            if (Math.Abs(running.Target - target) < 0.5)
            {
                return;
            }
        }
        else if (Math.Abs(offset - target) < 0.5)
        {
            // Already in place: no motion, and nothing for the scroll handler to sync.
            return;
        }
        if (_contentScroll is null)
        {
            _contentScroll = new MotionValue<double>(this, offset, MotionVectors.Double);
            _contentScroll.Changed += WriteContentOffset;
        }
        if (!_contentScroll.IsAnimating)
        {
            _contentScroll.Jump(offset);
        }
        // Reduced motion drops the travel (position tweens are full-motion only); Off jumps inside MotionValue too.
        var smooth = animate && IsLoaded && IsAnimationEnabled && AnimationDuration > TimeSpan.Zero && ArcMotion.IsFull(this);
        if (!smooth)
        {
            _contentScroll.Jump(target);
            return;
        }
        // Every step writes the offset and so causes a layout pass; the hook follows a moving section and unhooks itself
        // after the last step.
        HookLayout();
        // Code-driven: the target is measured from the laid-out sections and is retargeted when they move, and a click
        // during the scroll continues from the current position; a ControlTheme transition on Offset can do neither.
        _ = _contentScroll.AnimateTo(target, MotionSpec.Tween(AnimationDuration, EasingFunction));
    }

    private void WriteContentOffset(double value)
    {
        if (_contentViewport is null)
        {
            return;
        }
        _writtenOffset = value;
        _contentViewport.Offset = new Vector(_contentViewport.Offset.X, value);
    }

    private double TargetOffset(int index)
    {
        var viewport = _contentViewport!;
        var max = Math.Max(0, viewport.Extent.Height - viewport.Viewport.Height);
        var top = SectionTop(index);
        // Top: the section sits below the content padding, where the first section starts.
        var target = top - (_sectionsPanel?.Margin.Top ?? 0);
        if (SnapAlignment == FluidTabSnapAlignment.Center)
        {
            var height = _sections[index].Bounds.Height;
            // A section taller than the viewport shows its top instead of its middle.
            target = height >= viewport.Viewport.Height ? top : top + height / 2 - viewport.Viewport.Height / 2;
        }
        return Math.Clamp(target, 0, max);
    }

    private void HookLayout()
    {
        if (!_layoutHooked)
        {
            _layoutHooked = true;
            LayoutUpdated += OnLayoutUpdated;
        }
    }

    private void UnhookLayout()
    {
        if (_layoutHooked)
        {
            _layoutHooked = false;
            LayoutUpdated -= OnLayoutUpdated;
        }
    }

    private void UnhookLayoutWhenIdle()
    {
        if (_pendingIndex < 0 && _contentScroll is not { IsAnimating: true })
        {
            UnhookLayout();
        }
    }

    /// <summary>
    /// Subscribed only while a scroll waits for layout or runs: after layout the extent and the section bounds are final,
    /// so a waiting scroll starts and a running one follows its section if it moved (content loaded or resized).
    /// </summary>
    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_pendingIndex >= 0)
        {
            var index = _pendingIndex;
            if (index < ItemCount && IsSectionShown(index) && _contentViewport is { IsArrangeValid: true } && _sectionsPanel is { IsArrangeValid: true })
            {
                ScrollToSection(index, _pendingAnimate);
            }
            else if (index >= ItemCount || !IsSectionShown(index))
            {
                _pendingIndex = -1;
            }
        }
        else if (_contentScroll is { IsAnimating: true } running && _scrollIndex >= 0 && IsSectionShown(_scrollIndex))
        {
            var target = TargetOffset(_scrollIndex);
            if (Math.Abs(running.Target - target) >= 0.5)
            {
                _ = running.AnimateTo(target, MotionSpec.Tween(AnimationDuration, EasingFunction));
            }
        }
        UnhookLayoutWhenIdle();
    }

    private void OnContentUserInput(object? sender, RoutedEventArgs e)
    {
        if (_contentScroll is { IsAnimating: true } running)
        {
            running.Stop();
        }
        _writtenOffset = double.NaN;
    }

    private void OnContentScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_contentViewport is null || _contentScroll is { IsAnimating: true } || _pendingIndex >= 0)
        {
            return;
        }
        var offset = _contentViewport.Offset.Y;
        if (!double.IsNaN(_writtenOffset))
        {
            var ours = Math.Abs(offset - _writtenOffset) < 0.5;
            _writtenOffset = double.NaN;
            if (ours)
            {
                // The last step of our own scroll: the selection is already the section that was asked for.
                return;
            }
        }
        if (Math.Abs(e.OffsetDelta.Y) < 0.01)
        {
            return;
        }
        SyncSelectionToScroll(offset);
    }

    private void SyncSelectionToScroll(double offset)
    {
        var index = SectionAt(offset);
        if (index < 0 || index == SelectedIndex || !IsTabEnabled(index))
        {
            return;
        }
        _syncingFromScroll = true;
        try
        {
            SelectedIndex = index;
        }
        finally
        {
            _syncingFromScroll = false;
        }
    }

    /// <summary>The section on the anchor line at <paramref name="offset"/>; the first at the top and the last at the bottom.</summary>
    private int SectionAt(double offset)
    {
        var viewport = _contentViewport!;
        var max = Math.Max(0, viewport.Extent.Height - viewport.Viewport.Height);
        int first = -1, last = -1;
        for (var i = 0; i < _sections.Count; i++)
        {
            if (_sections[i].IsVisible)
            {
                if (first < 0)
                {
                    first = i;
                }
                last = i;
            }
        }
        if (first < 0)
        {
            return -1;
        }
        if (offset <= 0.5)
        {
            return first;
        }
        if (max > 0.5 && offset >= max - 0.5)
        {
            return last;
        }
        var anchor = offset + (SnapAlignment == FluidTabSnapAlignment.Center
            ? viewport.Viewport.Height / 2
            : (_sectionsPanel?.Margin.Top ?? 0) + 1);
        var found = first;
        for (var i = first; i <= last; i++)
        {
            if (!_sections[i].IsVisible)
            {
                continue;
            }
            if (SectionTop(i) <= anchor)
            {
                found = i;
            }
            else
            {
                break;
            }
        }
        return found;
    }

    #endregion

    #region Tab list

    private void OnListArranged()
    {
        // Passive layout changes (fonts, resizes, items) snap the indicators; an equal target leaves a glide alone.
        UpdateIndicator(animate: false, onlyWhenMoved: true);
    }

    private void UpdateIndicator(bool animate, bool onlyWhenMoved = false)
    {
        if (_highlight is null || _listPanel is null)
        {
            return;
        }
        var index = SelectedIndex;
        if (index < 0 || ContainerFromIndex(index) is not { IsVisible: true } container || container.Bounds.Height <= 0)
        {
            _highlightTarget = default;
            _highlight.Hide(animate: false);
            _indicator?.Hide(animate: false);
            return;
        }
        var origin = container.TranslatePoint(default, _listPanel);
        if (origin is null)
        {
            return;
        }
        var target = new Rect(origin.Value, container.Bounds.Size);
        if (onlyWhenMoved && _highlight.HasTarget && Near(target, _highlightTarget))
        {
            return;
        }
        _highlightTarget = target;
        var glide = animate && IsLoaded;
        _highlight.MoveTo(target, glide, ArcSpring.Morph);
        if (_indicator is not null)
        {
            var thickness = Token(FluidTabControlTokenKind.IndicatorThickness, 3);
            var inset = Math.Min(Token(FluidTabControlTokenKind.IndicatorInset, 9), target.Height / 2 - 2);
            var x = TabPlacement == FluidTabPlacement.Right ? target.Right - thickness : target.X;
            _indicator.MoveTo(new Rect(x, target.Y + inset, thickness, Math.Max(0, target.Height - inset * 2)), glide, ArcSpring.Morph);
        }
    }

    private double Token(FluidTabControlTokenKind kind, double fallback)
        => this.TryFindResource(FluidTabControlTokens.Key(kind), out var value) && value is double d ? d : fallback;

    private static bool Near(Rect a, Rect b)
        => Math.Abs(a.X - b.X) < 0.5 && Math.Abs(a.Y - b.Y) < 0.5 && Math.Abs(a.Width - b.Width) < 0.5 && Math.Abs(a.Height - b.Height) < 0.5;

    private void OnListWheel(object? sender, PointerWheelEventArgs e) => _listScroll?.Stop();

    /// <summary>Scrolls the tab list (not the page around it) so the tab at <paramref name="index"/> is fully visible.</summary>
    private void RevealTab(int index)
    {
        if (_listViewport is null || index < 0 || ContainerFromIndex(index) is not { } container || container.Bounds.Height <= 0)
        {
            return;
        }
        var origin = container.TranslatePoint(default, _listViewport);
        if (origin is null)
        {
            return;
        }
        var height = _listViewport.Viewport.Height;
        var max = Math.Max(0, _listViewport.Extent.Height - height);
        var offset = _listViewport.Offset.Y;
        var top = origin.Value.Y;
        var bottom = top + container.Bounds.Height;
        var delta = top < 0 ? top : bottom > height ? bottom - height : 0;
        if (Math.Abs(delta) <= 0.5)
        {
            return;
        }
        var target = Math.Clamp(offset + delta, 0, max);
        if (_listScroll is null)
        {
            _listScroll = new MotionValue<double>(this, offset, MotionVectors.Double);
            _listScroll.Changed += value =>
            {
                if (_listViewport is not null)
                {
                    _listViewport.Offset = new Vector(_listViewport.Offset.X, value);
                }
            };
        }
        if (!_listScroll.IsAnimating)
        {
            _listScroll.Jump(offset);
        }
        // Smooth spring (Reduced and Off jump inside MotionValue).
        _ = _listScroll.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new FluidTabControlAutomationPeer(this);

    private sealed class FluidTabControlAutomationPeer : SelectingItemsControlAutomationPeer
    {
        public FluidTabControlAutomationPeer(FluidTabControl owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;
    }
}
