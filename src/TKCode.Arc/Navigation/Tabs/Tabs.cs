using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>How keyboard focus and selection relate in a <see cref="Tabs"/> list.</summary>
public enum TabsActivationMode
{
    /// <summary>Arrow keys move focus and select the focused tab at once (Arc default).</summary>
    Automatic,

    /// <summary>Arrow keys only move focus; Enter or Space selects the focused tab.</summary>
    Manual
}

/// <summary>
/// Arc tabs: switch between related views of the same object. A single selection pill glides between the
/// triggers on a morph spring, the incoming panel slides 8px in from the side of travel while the outgoing one
/// is pinned in place and fades, and the panel area springs from the old panel height to the new one so the
/// content below glides. Long tab lists scroll horizontally with soft edges and edge scroll buttons, and the
/// selected or focused tab is always scrolled into view.
/// <para>
/// Items are <see cref="TabItem"/> containers written inline (<c>&lt;arc:TabItem Header="Overview"&gt;…&lt;/arc:TabItem&gt;</c>)
/// or generated from <see cref="ItemsControl.ItemsSource"/>: <see cref="ItemsControl.ItemTemplate"/> renders the
/// trigger and <see cref="ContentTemplate"/> the panel.
/// </para>
/// </summary>
[TemplatePart(ListViewportPart, typeof(ScrollViewer))]
[TemplatePart(ListPanelPart, typeof(Panel))]
[TemplatePart(SelectionPillPart, typeof(GlidingIndicator))]
[TemplatePart(ScrollLeftPart, typeof(AvaloniaButton))]
[TemplatePart(ScrollRightPart, typeof(AvaloniaButton))]
[TemplatePart(ContentHostPart, typeof(Control))]
[PseudoClasses(OverflowPseudoClass, ScrolledStartPseudoClass, ScrolledEndPseudoClass)]
public class Tabs : SelectingItemsControl
{
    public const string ListViewportPart = "PART_ListViewport";
    public const string ListPanelPart = "PART_ListPanel";
    public const string SelectionPillPart = "PART_SelectionPill";
    public const string ScrollLeftPart = "PART_ScrollLeft";
    public const string ScrollRightPart = "PART_ScrollRight";
    public const string ContentHostPart = "PART_ContentHost";

    /// <summary>Set while the tab list is wider than its frame.</summary>
    public const string OverflowPseudoClass = ":overflow";

    /// <summary>Set while there are tabs hidden before the visible part of the list.</summary>
    public const string ScrolledStartPseudoClass = ":scrolled-start";

    /// <summary>Set while there are tabs hidden after the visible part of the list.</summary>
    public const string ScrolledEndPseudoClass = ":scrolled-end";

    /// <summary>Width of the edge fade over the scrolled list (Arc: 35px).</summary>
    internal const double EdgeFade = 35;

    /// <summary>Room kept free for a scroll button when a tab is scrolled into view (Arc: 34px).</summary>
    internal const double RevealInset = 34;

    #region Properties

    /// <summary>Template for the panel of data items (the trigger uses <see cref="ItemsControl.ItemTemplate"/>).</summary>
    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        AvaloniaProperty.Register<Tabs, IDataTemplate?>(nameof(ContentTemplate));

    /// <summary>Whether arrow keys select the tab they move to (<see cref="TabsActivationMode.Automatic"/>, Arc default).</summary>
    public static readonly StyledProperty<TabsActivationMode> ActivationModeProperty =
        AvaloniaProperty.Register<Tabs, TabsActivationMode>(nameof(ActivationMode));

    /// <summary>Accessible name of the left scroll button.</summary>
    public static readonly StyledProperty<string> ScrollLeftLabelProperty =
        AvaloniaProperty.Register<Tabs, string>(nameof(ScrollLeftLabel), "Scroll tabs left");

    /// <summary>Accessible name of the right scroll button.</summary>
    public static readonly StyledProperty<string> ScrollRightLabelProperty =
        AvaloniaProperty.Register<Tabs, string>(nameof(ScrollRightLabel), "Scroll tabs right");

    /// <summary>The panel content of the selected tab.</summary>
    public static readonly DirectProperty<Tabs, object?> SelectedContentProperty =
        AvaloniaProperty.RegisterDirect<Tabs, object?>(nameof(SelectedContent), o => o.SelectedContent);

    /// <summary>The template of the selected tab's panel.</summary>
    public static readonly DirectProperty<Tabs, IDataTemplate?> SelectedContentTemplateProperty =
        AvaloniaProperty.RegisterDirect<Tabs, IDataTemplate?>(nameof(SelectedContentTemplate), o => o.SelectedContentTemplate);

    /// <inheritdoc cref="ContentTemplateProperty"/>
    public IDataTemplate? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    /// <inheritdoc cref="ActivationModeProperty"/>
    public TabsActivationMode ActivationMode
    {
        get => GetValue(ActivationModeProperty);
        set => SetValue(ActivationModeProperty, value);
    }

    /// <inheritdoc cref="ScrollLeftLabelProperty"/>
    public string ScrollLeftLabel
    {
        get => GetValue(ScrollLeftLabelProperty);
        set => SetValue(ScrollLeftLabelProperty, value);
    }

    /// <inheritdoc cref="ScrollRightLabelProperty"/>
    public string ScrollRightLabel
    {
        get => GetValue(ScrollRightLabelProperty);
        set => SetValue(ScrollRightLabelProperty, value);
    }

    private object? _selectedContent;

    /// <inheritdoc cref="SelectedContentProperty"/>
    public object? SelectedContent
    {
        get => _selectedContent;
        private set => SetAndRaise(SelectedContentProperty, ref _selectedContent, value);
    }

    private IDataTemplate? _selectedContentTemplate;

    /// <inheritdoc cref="SelectedContentTemplateProperty"/>
    public IDataTemplate? SelectedContentTemplate
    {
        get => _selectedContentTemplate;
        private set => SetAndRaise(SelectedContentTemplateProperty, ref _selectedContentTemplate, value);
    }

    #endregion

    private ScrollViewer? _viewport;
    private TabsListPanel? _listPanel;
    private GlidingIndicator? _pill;
    private AvaloniaButton? _scrollLeft;
    private AvaloniaButton? _scrollRight;
    private TabsPanelHost? _contentHost;
    private MotionValue<double>? _scroll;
    private (bool Start, bool End, double Fade) _edgeMaskKey;
    private Rect _pillTarget;
    private bool _overflow;
    private bool _scrolledStart;
    private bool _scrolledEnd;
    private int _focusIndex = -1;
    private bool _revealPending;

    static Tabs()
    {
        SelectionModeProperty.OverrideDefaultValue<Tabs>(SelectionMode.AlwaysSelected);
        FocusableProperty.OverrideDefaultValue<Tabs>(false);
    }

    /// <summary>Selects the tab at <paramref name="index"/> when it exists and is enabled.</summary>
    public bool SelectTab(int index)
    {
        if (index < 0 || index >= ItemCount || !IsTabEnabled(index))
        {
            return false;
        }
        SelectedIndex = index;
        return true;
    }

    #region Containers

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new TabItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<TabItem>(item, out recycleKey);

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is TabItem tab && !ReferenceEquals(container, item))
        {
            if (ContentTemplate is { } template && !tab.IsSet(ContentControl.ContentTemplateProperty))
            {
                tab.ContentTemplate = template;
            }
        }
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        if (index == SelectedIndex)
        {
            UpdateContent(0, animate: false);
        }
        UpdateTabStops();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        UpdateTabStops();
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_viewport is not null)
        {
            _viewport.ScrollChanged -= OnViewportScrollChanged;
            _viewport.SizeChanged -= OnViewportSizeChanged;
            _viewport.RemoveHandler(PointerWheelChangedEvent, OnViewportWheel);
        }
        if (_scrollLeft is not null)
        {
            _scrollLeft.Click -= OnScrollLeftClick;
        }
        if (_scrollRight is not null)
        {
            _scrollRight.Click -= OnScrollRightClick;
        }
        if (_listPanel is not null)
        {
            _listPanel.Arranged = null;
        }

        base.OnApplyTemplate(e);

        _viewport = e.NameScope.Find<ScrollViewer>(ListViewportPart);
        _listPanel = e.NameScope.Find<TabsListPanel>(ListPanelPart);
        _pill = e.NameScope.Find<GlidingIndicator>(SelectionPillPart);
        _scrollLeft = e.NameScope.Find<AvaloniaButton>(ScrollLeftPart);
        _scrollRight = e.NameScope.Find<AvaloniaButton>(ScrollRightPart);
        _contentHost = e.NameScope.Find<TabsPanelHost>(ContentHostPart);
        _pillTarget = default;

        if (_viewport is not null)
        {
            _viewport.ScrollChanged += OnViewportScrollChanged;
            _viewport.SizeChanged += OnViewportSizeChanged;
            _viewport.AddHandler(PointerWheelChangedEvent, OnViewportWheel, RoutingStrategies.Tunnel);
        }
        if (_scrollLeft is not null)
        {
            _scrollLeft.Click += OnScrollLeftClick;
        }
        if (_scrollRight is not null)
        {
            _scrollRight.Click += OnScrollRightClick;
        }
        if (_listPanel is not null)
        {
            _listPanel.Arranged = OnListArranged;
        }
        _revealPending = true;
        UpdateContent(0, animate: false);
    }

    #endregion

    #region Selection

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty)
        {
            var from = change.GetOldValue<int>();
            var to = change.GetNewValue<int>();
            var direction = from >= 0 && to >= 0 && from != to ? Math.Sign(to - from) : 0;
            _focusIndex = to;
            UpdateContent(direction, animate: true);
            UpdateIndicator(animate: true);
            UpdateTabStops();
            RevealIndex(to);
        }
        else if (change.Property == ContentTemplateProperty)
        {
            UpdateContent(0, animate: false);
        }
    }

    /// <summary>Called by a <see cref="TabItem"/> when its content or template changes.</summary>
    internal void OnTabContentChanged(TabItem tab)
    {
        if (IndexFromContainer(tab) == SelectedIndex)
        {
            UpdateContent(0, animate: false);
        }
    }

    /// <summary>Called by a <see cref="TabItem"/> when it is pressed (mouse down, like Radix).</summary>
    internal void OnTabPressed(TabItem tab)
    {
        var index = IndexFromContainer(tab);
        if (index >= 0 && tab.IsEffectivelyEnabled)
        {
            _focusIndex = index;
            SelectedIndex = index;
        }
    }

    /// <summary>Called by a <see cref="TabItem"/> when it receives keyboard focus.</summary>
    internal void OnTabFocused(TabItem tab, NavigationMethod method)
    {
        var index = IndexFromContainer(tab);
        if (index < 0)
        {
            return;
        }
        _focusIndex = index;
        UpdateTabStops();
        RevealIndex(index);
        // Radix activates a tab when it receives focus in automatic mode (arrow keys and programmatic focus).
        if (ActivationMode == TabsActivationMode.Automatic && method == NavigationMethod.Directional && index != SelectedIndex)
        {
            SelectedIndex = index;
        }
    }

    private void UpdateContent(int direction, bool animate)
    {
        var index = SelectedIndex;
        object? content = null;
        IDataTemplate? template = null;
        if (index >= 0 && ContainerFromIndex(index) is TabItem tab)
        {
            content = tab.Content;
            template = tab.ContentTemplate ?? ContentTemplate;
        }
        else if (index >= 0 && index < ItemCount)
        {
            var item = Items[index];
            if (item is not TabItem)
            {
                content = item;
                template = ContentTemplate;
            }
        }
        SelectedContentTemplate = template;
        SelectedContent = content;
        _contentHost?.Show(content, template, direction, animate && IsLoaded);
    }

    private bool IsTabEnabled(int index) => ContainerFromIndex(index) is not { } container || container.IsEffectivelyEnabled;

    private void UpdateTabStops()
    {
        var count = ItemCount;
        var stop = _focusIndex >= 0 && _focusIndex < count ? _focusIndex : SelectedIndex;
        for (var i = 0; i < count; i++)
        {
            if (ContainerFromIndex(i) is TabItem tab)
            {
                tab.IsTabStop = i == stop || (stop < 0 && i == 0);
            }
        }
    }

    #endregion

    #region Keyboard

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || e.Source is not Visual source)
        {
            base.OnKeyDown(e);
            return;
        }
        var tab = FindTab(source);
        if (tab is null)
        {
            base.OnKeyDown(e);
            return;
        }
        var current = IndexFromContainer(tab);
        int? target = e.Key switch
        {
            Key.Left => Step(current, -1),
            Key.Right => Step(current, 1),
            Key.Home => FirstEnabled(0, 1),
            Key.End => FirstEnabled(ItemCount - 1, -1),
            _ => null
        };
        if (target is { } index && index >= 0)
        {
            e.Handled = true;
            FocusTab(index);
            return;
        }
        if ((e.Key == Key.Enter || e.Key == Key.Space) && current >= 0)
        {
            e.Handled = true;
            SelectedIndex = current;
            return;
        }
        base.OnKeyDown(e);
    }

    private TabItem? FindTab(Visual source)
    {
        for (Visual? v = source; v is not null && v != this; v = v.GetVisualParent())
        {
            if (v is TabItem tab && IndexFromContainer(tab) >= 0)
            {
                return tab;
            }
        }
        return null;
    }

    private void FocusTab(int index)
    {
        if (ContainerFromIndex(index) is TabItem tab)
        {
            _focusIndex = index;
            UpdateTabStops();
            tab.Focus(NavigationMethod.Directional);
            if (ActivationMode == TabsActivationMode.Automatic)
            {
                SelectedIndex = index;
            }
        }
    }

    private int Step(int current, int delta)
    {
        var count = ItemCount;
        if (count == 0)
        {
            return -1;
        }
        var index = current;
        for (var i = 0; i < count; i++)
        {
            // Radix tabs loop at the ends.
            index = ((index + delta) % count + count) % count;
            if (IsTabEnabled(index))
            {
                return index;
            }
        }
        return current;
    }

    private int FirstEnabled(int start, int step)
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

    #region Indicator

    private void OnListArranged()
    {
        // Passive layout changes (fonts, resizes, items) snap the pill; an equal target leaves a glide in flight alone.
        UpdateIndicator(animate: false, onlyWhenMoved: true);
        UpdateOverflow();
        if (_revealPending && SelectedIndex >= 0 && ContainerFromIndex(SelectedIndex) is { Bounds.Width: > 0 })
        {
            // The selected tab starts in view (Arc reveals it on mount), without motion.
            _revealPending = false;
            RevealIndex(SelectedIndex, instant: true);
        }
    }

    private void UpdateIndicator(bool animate, bool onlyWhenMoved = false)
    {
        if (_pill is null || _listPanel is null)
        {
            return;
        }
        var index = SelectedIndex;
        if (index < 0 || ContainerFromIndex(index) is not { IsVisible: true } container || container.Bounds.Width <= 0)
        {
            _pillTarget = default;
            _pill.Hide(animate: false);
            return;
        }
        var origin = container.TranslatePoint(default, _listPanel);
        if (origin is null)
        {
            return;
        }
        var target = new Rect(origin.Value, container.Bounds.Size).Deflate(0.5);
        if (onlyWhenMoved && _pill.HasTarget && Near(target, _pillTarget))
        {
            return;
        }
        _pillTarget = target;
        _pill.MoveTo(target, animate && IsLoaded, ArcSpring.Morph);
    }

    private static bool Near(Rect a, Rect b)
        => Math.Abs(a.X - b.X) < 0.5 && Math.Abs(a.Y - b.Y) < 0.5 && Math.Abs(a.Width - b.Width) < 0.5 && Math.Abs(a.Height - b.Height) < 0.5;

    #endregion

    #region Overflow scrolling

    private void OnViewportScrollChanged(object? sender, ScrollChangedEventArgs e) => UpdateOverflow();

    private void OnViewportSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateOverflow();

    private void OnViewportWheel(object? sender, PointerWheelEventArgs e) => _scroll?.Stop();

    private void UpdateOverflow()
    {
        if (_viewport is null)
        {
            return;
        }
        var extent = _viewport.Extent.Width;
        var width = _viewport.Viewport.Width;
        var max = Math.Max(0, extent - width);
        var offset = _viewport.Offset.X;
        var overflow = extent > width + 1;
        var start = overflow && offset > 1;
        var end = overflow && offset < max - 1;
        if (overflow != _overflow || start != _scrolledStart || end != _scrolledEnd)
        {
            _overflow = overflow;
            _scrolledStart = start;
            _scrolledEnd = end;
            PseudoClasses.Set(OverflowPseudoClass, overflow);
            PseudoClasses.Set(ScrolledStartPseudoClass, start);
            PseudoClasses.Set(ScrolledEndPseudoClass, end);
            if (_scrollLeft is not null)
            {
                _scrollLeft.IsVisible = overflow;
                _scrollLeft.IsEnabled = start;
            }
            if (_scrollRight is not null)
            {
                _scrollRight.IsVisible = overflow;
                _scrollRight.IsEnabled = end;
            }
        }
        UpdateEdgeMask(width);
    }

    private void UpdateEdgeMask(double width)
    {
        if (_viewport is null)
        {
            return;
        }
        if ((!_scrolledStart && !_scrolledEnd) || width <= 0)
        {
            _viewport.OpacityMask = null;
            _edgeMaskKey = default;
            return;
        }
        var fade = Math.Round(Math.Min(0.5, EdgeFade / width), 4);
        var key = (_scrolledStart, _scrolledEnd, fade);
        if (_viewport.OpacityMask is not null && key == _edgeMaskKey)
        {
            return;
        }
        _edgeMaskKey = key;
        // A new immutable brush only when the edges or the width change; scrolling in between allocates nothing.
        var stops = new[]
        {
            new ImmutableGradientStop(0, _scrolledStart ? Colors.Transparent : Colors.Black),
            new ImmutableGradientStop(_scrolledStart ? fade : 0, Colors.Black),
            new ImmutableGradientStop(_scrolledEnd ? 1 - fade : 1, Colors.Black),
            new ImmutableGradientStop(1, _scrolledEnd ? Colors.Transparent : Colors.Black)
        };
        _viewport.OpacityMask = new ImmutableLinearGradientBrush(stops, 1, null, null, GradientSpreadMethod.Pad,
            new RelativePoint(0, 0.5, RelativeUnit.Relative), new RelativePoint(1, 0.5, RelativeUnit.Relative));
    }

    private void OnScrollLeftClick(object? sender, RoutedEventArgs e) => ScrollList(-1);

    private void OnScrollRightClick(object? sender, RoutedEventArgs e) => ScrollList(1);

    private void ScrollList(int direction)
    {
        if (_viewport is null)
        {
            return;
        }
        var from = _scroll is { IsAnimating: true } running ? running.Target : _viewport.Offset.X;
        ScrollTo(from + direction * _viewport.Viewport.Width * 0.75);
    }

    private void RevealIndex(int index, bool instant = false)
    {
        if (_viewport is null || index < 0 || ContainerFromIndex(index) is not { } container || container.Bounds.Width <= 0)
        {
            return;
        }
        var origin = container.TranslatePoint(default, _viewport);
        if (origin is null)
        {
            return;
        }
        var max = Math.Max(0, _viewport.Extent.Width - _viewport.Viewport.Width);
        var offset = _viewport.Offset.X;
        var left = offset > 1 ? RevealInset : 0;
        var right = _viewport.Viewport.Width - (offset < max - 1 ? RevealInset : 0);
        var itemLeft = origin.Value.X;
        var itemRight = itemLeft + container.Bounds.Width;
        var delta = itemLeft < left ? itemLeft - left : itemRight > right ? itemRight - right : 0;
        if (Math.Abs(delta) > 0.5)
        {
            if (instant)
            {
                _scroll?.Stop();
                _viewport.Offset = new Vector(Math.Clamp(offset + delta, 0, max), _viewport.Offset.Y);
            }
            else
            {
                ScrollTo(offset + delta);
            }
        }
    }

    private void ScrollTo(double x)
    {
        if (_viewport is null)
        {
            return;
        }
        var max = Math.Max(0, _viewport.Extent.Width - _viewport.Viewport.Width);
        var target = Math.Clamp(x, 0, max);
        if (_scroll is null)
        {
            _scroll = new MotionValue<double>(this, _viewport.Offset.X, MotionVectors.Double);
            _scroll.Changed += value =>
            {
                if (_viewport is not null)
                {
                    _viewport.Offset = new Vector(value, _viewport.Offset.Y);
                }
            };
        }
        if (!_scroll.IsAnimating)
        {
            _scroll.Jump(_viewport.Offset.X);
        }
        // P30: smooth scroll on the smooth spring; Reduced and Off jump (MotionValue honors the level).
        _ = _scroll.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new TabsAutomationPeer(this);

    private sealed class TabsAutomationPeer : SelectingItemsControlAutomationPeer
    {
        public TabsAutomationPeer(Tabs owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;
    }
}
