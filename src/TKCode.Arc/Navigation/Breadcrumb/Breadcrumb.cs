using System.Collections.Specialized;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>Raised when a breadcrumb (or an entry of the overflow menu) is chosen.</summary>
public sealed class BreadcrumbItemClickEventArgs : RoutedEventArgs
{
    public BreadcrumbItemClickEventArgs(RoutedEvent routedEvent, object? item, int index) : base(routedEvent)
    {
        Item = item;
        Index = index;
    }

    /// <summary>The item (the data item, or the <see cref="BreadcrumbItem"/> written inline).</summary>
    public object? Item { get; }

    /// <summary>Index of the item in the path (0 is the root).</summary>
    public int Index { get; }
}

/// <summary>
/// Arc breadcrumb: where a page sits in a hierarchy, from the root to the current page. Crumbs are separated by
/// chevrons; the last one is the current page (accent, medium weight, not a link) and every crumb reserves the
/// width of its medium-weight label so becoming current never shifts the path. Crumbs present at first render
/// stay still; crumbs added later slide in 8px from the path before them out of a subtle blur while their siblings
/// make room on the smooth spring, and removed crumbs slide 4px back as they fade.
/// <para>
/// Items are <see cref="BreadcrumbItem"/>s written inline or generated from <see cref="ItemsControl.ItemsSource"/>
/// (<see cref="ItemsControl.ItemTemplate"/> renders the label). Choosing a crumb raises <see cref="ItemClick"/>,
/// runs its <see cref="BreadcrumbItem.Command"/> and opens its <see cref="BreadcrumbItem.NavigateUri"/>.
/// <see cref="MaxVisibleItems"/> (off by default, as in Arc) folds the middle of a long path into an overflow menu.
/// </para>
/// </summary>
[TemplatePart(GhostLayerPart, typeof(Canvas))]
public class Breadcrumb : ItemsControl
{
    public const string GhostLayerPart = "PART_GhostLayer";

    /// <summary>Accessible name of the navigation landmark.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<Breadcrumb, string>(nameof(Label), "Breadcrumb");

    /// <summary>
    /// When greater than 1 and the path is longer, the root and the last <c>MaxVisibleItems − 1</c> crumbs stay
    /// visible and the rest collapse into an overflow menu. 0 (Arc default) never collapses.
    /// </summary>
    public static readonly StyledProperty<int> MaxVisibleItemsProperty =
        AvaloniaProperty.Register<Breadcrumb, int>(nameof(MaxVisibleItems));

    /// <summary>Accessible name of the overflow button.</summary>
    public static readonly StyledProperty<string> OverflowLabelProperty =
        AvaloniaProperty.Register<Breadcrumb, string>(nameof(OverflowLabel), "Show hidden path");

    public static readonly RoutedEvent<BreadcrumbItemClickEventArgs> ItemClickEvent =
        RoutedEvent.Register<Breadcrumb, BreadcrumbItemClickEventArgs>(nameof(ItemClick), RoutingStrategies.Bubble);

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="MaxVisibleItemsProperty"/>
    public int MaxVisibleItems { get => GetValue(MaxVisibleItemsProperty); set => SetValue(MaxVisibleItemsProperty, value); }

    /// <inheritdoc cref="OverflowLabelProperty"/>
    public string OverflowLabel { get => GetValue(OverflowLabelProperty); set => SetValue(OverflowLabelProperty, value); }

    /// <summary>Raised when a crumb or an overflow entry is chosen.</summary>
    public event EventHandler<BreadcrumbItemClickEventArgs>? ItemClick
    {
        add => AddHandler(ItemClickEvent, value);
        remove => RemoveHandler(ItemClickEvent, value);
    }

    private Canvas? _ghostLayer;
    private readonly Dictionary<Control, Point> _flipFrom = new();
    private readonly List<string?> _keys = new();
    private ArcPopup? _overflowPopup;
    private StackPanel? _overflowList;
    private BreadcrumbItem? _overflowAnchor;

    static Breadcrumb()
    {
        FocusableProperty.OverrideDefaultValue<Breadcrumb>(false);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<Breadcrumb>(AutomationLandmarkType.Navigation);
    }

    public Breadcrumb()
    {
        AutomationProperties.SetName(this, Label);
        ItemsView.CollectionChanged += OnItemsChanged;
    }

    /// <summary>True while the overflow menu is open.</summary>
    public bool IsOverflowOpen => _overflowPopup?.IsOpen == true;

    #region Containers

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new BreadcrumbItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<BreadcrumbItem>(item, out recycleKey);

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        RefreshStates();
        // Arc keys crumbs by label and index: a crumb that was already on screen at this index does not re-enter.
        var known = index < _keys.Count && _keys[index] == KeyOf(item);
        if (container is BreadcrumbItem crumb && !known && IsLoaded && crumb.IsVisible && ArcMotion.IsFull(this))
        {
            crumb.PlayEnter();
        }
    }

    private static string? KeyOf(object? item) => item is BreadcrumbItem crumb ? crumb.Content?.ToString() : item?.ToString();

    protected override void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex)
    {
        base.ContainerIndexChangedOverride(container, oldIndex, newIndex);
        RefreshStates();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        // A crumb whose label is still at its index (a new path that shares this prefix) leaves without a ghost.
        var staying = container is BreadcrumbItem { PathIndex: >= 0 } old && old.PathIndex < ItemsView.Count &&
                      !ReferenceEquals(ItemsView[old.PathIndex], old) && KeyOf(ItemsView[old.PathIndex]) == old.PathKey;
        if (container is BreadcrumbItem crumb && !staying && IsLoaded && _ghostLayer is not null && crumb.IsVisible &&
            crumb.Bounds.Width > 0 && ArcMotion.IsFull(this) && TopLevel.GetTopLevel(crumb) is not null)
        {
            var origin = crumb.TranslatePoint(default, _ghostLayer);
            if (origin is { } at)
            {
                var ghost = crumb.CreateGhost();
                Canvas.SetLeft(ghost, at.X);
                Canvas.SetTop(ghost, at.Y);
                _ghostLayer.Children.Add(ghost);
                ghost.PlayExit(() => _ghostLayer?.Children.Remove(ghost));
            }
        }
        base.ClearContainerForItemOverride(container);
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        CloseOverflow();
        RefreshStates();
        if (!IsLoaded || !ArcMotion.IsFull(this))
        {
            return;
        }
        // FLIP: remember where the remaining crumbs are drawn now; the panel springs them from there after layout.
        _flipFrom.Clear();
        foreach (var container in GetRealizedContainers())
        {
            if (container.IsVisible && container.Bounds.Width > 0)
            {
                _flipFrom[container] = container.Bounds.Position;
            }
        }
    }

    /// <summary>Called by <see cref="BreadcrumbPanel"/> after arranging: crumbs that moved because the path changed glide.</summary>
    internal void ApplyFlip()
    {
        _keys.Clear();
        for (var i = 0; i < ItemCount; i++)
        {
            _keys.Add(ContainerFromIndex(i) is BreadcrumbItem known ? known.PathKey : null);
        }
        if (_flipFrom.Count == 0)
        {
            return;
        }
        foreach (var (container, from) in _flipFrom)
        {
            if (container is BreadcrumbItem crumb && crumb.IsVisible && IndexFromContainer(crumb) >= 0)
            {
                var delta = from - crumb.Bounds.Position;
                if (Math.Abs(delta.X) > 0.5 || Math.Abs(delta.Y) > 0.5)
                {
                    crumb.PlayShift(delta);
                }
            }
        }
        _flipFrom.Clear();
    }

    /// <summary>Updates first / current / collapsed states and set positions of every crumb.</summary>
    internal void RefreshStates()
    {
        var count = ItemCount;
        var max = MaxVisibleItems;
        var collapse = max > 1 && count > max;
        var tailStart = collapse ? count - (max - 1) : 1;
        for (var i = 0; i < count; i++)
        {
            if (ContainerFromIndex(i) is not BreadcrumbItem crumb)
            {
                continue;
            }
            crumb.PathIndex = i;
            crumb.PathKey = KeyOf(Items[i]);
            crumb.IsFirst = i == 0;
            crumb.IsCurrent = i == count - 1;
            var hidden = collapse && i > 0 && i < tailStart;
            crumb.IsOverflowAnchor = collapse && i == 1;
            crumb.IsVisible = !hidden || i == 1;
            AutomationProperties.SetPositionInSet(crumb, i + 1);
            AutomationProperties.SetSizeOfSet(crumb, count);
        }
    }

    #endregion

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _ghostLayer = e.NameScope.Find<Canvas>(GhostLayerPart);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == MaxVisibleItemsProperty)
        {
            CloseOverflow();
            RefreshStates();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Left / Right move between crumbs (in addition to Tab), like most desktop path bars.
        if (!e.Handled && e.Source is Visual source && (e.Key == Key.Left || e.Key == Key.Right))
        {
            var crumb = source as BreadcrumbItem ?? source.FindAncestorOfType<BreadcrumbItem>();
            if (crumb is not null && IndexFromContainer(crumb) is var index and >= 0)
            {
                var step = e.Key == Key.Right ? 1 : -1;
                for (var i = index + step; i >= 0 && i < ItemCount; i += step)
                {
                    if (ContainerFromIndex(i) is BreadcrumbItem { IsVisible: true, Focusable: true, IsEffectivelyEnabled: true } next)
                    {
                        next.Focus(NavigationMethod.Directional);
                        e.Handled = true;
                        break;
                    }
                }
            }
        }
        base.OnKeyDown(e);
    }

    /// <summary>Called by a crumb when it is chosen.</summary>
    internal void Invoke(BreadcrumbItem crumb)
    {
        if (crumb.IsOverflowAnchor)
        {
            OpenOverflow(crumb);
            return;
        }
        Invoke(IndexFromContainer(crumb), crumb);
    }

    private void Invoke(int index, BreadcrumbItem? crumb)
    {
        if (index < 0)
        {
            return;
        }
        var item = index < ItemCount ? Items[index] : null;
        RaiseEvent(new BreadcrumbItemClickEventArgs(ItemClickEvent, item, index));
        crumb ??= ContainerFromIndex(index) as BreadcrumbItem;
        crumb?.Execute();
    }

    #region Overflow menu

    /// <summary>Opens the overflow menu (when the path is collapsed).</summary>
    public void OpenOverflow()
    {
        if (ContainerFromIndex(1) is BreadcrumbItem { IsOverflowAnchor: true } anchor)
        {
            OpenOverflow(anchor);
        }
    }

    /// <summary>Closes the overflow menu.</summary>
    public void CloseOverflow()
    {
        if (_overflowPopup is { IsOpen: true } popup)
        {
            popup.IsOpen = false;
        }
    }

    private void OpenOverflow(BreadcrumbItem anchor)
    {
        if (TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        EnsureOverflow();
        var list = _overflowList!;
        list.Children.Clear();
        var max = MaxVisibleItems;
        var tailStart = ItemCount - (max - 1);
        for (var i = 1; i < tailStart; i++)
        {
            var crumb = ContainerFromIndex(i) as BreadcrumbItem;
            var entry = new BreadcrumbMenuItem
            {
                Index = i,
                Content = crumb?.MenuLabel ?? Items[i]?.ToString(),
                ContentTemplate = crumb?.Content is Control ? null : crumb?.ContentTemplate
            };
            entry.Click += OnOverflowEntryClick;
            list.Children.Add(entry);
        }
        _overflowAnchor = anchor;
        var popup = _overflowPopup!;
        popup.PlacementTarget = anchor.Frame ?? anchor;
        if (popup.Parent is null)
        {
            ((ISetLogicalParent)popup).SetParent(this);
        }
        popup.IsOpen = true;
        if (anchor.IsKeyboardFocusWithin && list.Children.Count > 0)
        {
            list.Children[0].Focus(NavigationMethod.Directional);
        }
    }

    private void EnsureOverflow()
    {
        if (_overflowPopup is not null)
        {
            return;
        }
        _overflowList = new StackPanel { Orientation = Orientation.Vertical };
        var surface = new BreadcrumbMenu { Content = _overflowList };
        surface.AddHandler(KeyDownEvent, OnOverflowKeyDown, RoutingStrategies.Tunnel);
        _overflowPopup = new ArcPopup
        {
            Child = surface,
            Placement = PlacementMode.Bottom,
            EnterOffset = 5,
            MarginToAnchor = 6,
            IsLightDismissEnabled = true
        };
        _overflowPopup.Closed += (_, _) =>
        {
            if (_overflowPopup is { IsOpen: false } closed && closed.Parent is not null)
            {
                ((ISetLogicalParent)closed).SetParent(null);
            }
        };
    }

    private void OnOverflowEntryClick(object? sender, RoutedEventArgs e)
    {
        if (sender is BreadcrumbMenuItem entry)
        {
            var anchor = _overflowAnchor;
            CloseOverflow();
            anchor?.Focus(NavigationMethod.Pointer);
            Invoke(entry.Index, null);
        }
    }

    private void OnOverflowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_overflowList is null)
        {
            return;
        }
        var entries = _overflowList.Children;
        var focused = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].IsFocused)
            {
                focused = i;
            }
        }
        int? target = e.Key switch
        {
            Key.Down => focused < 0 ? 0 : (focused + 1) % entries.Count,
            Key.Up => focused <= 0 ? entries.Count - 1 : focused - 1,
            Key.Home => 0,
            Key.End => entries.Count - 1,
            _ => null
        };
        if (target is { } index && entries.Count > 0)
        {
            entries[index].Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            var anchor = _overflowAnchor;
            CloseOverflow();
            anchor?.Focus(NavigationMethod.Tab);
            e.Handled = true;
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new BreadcrumbAutomationPeer(this);

    private sealed class BreadcrumbAutomationPeer : ControlAutomationPeer
    {
        public BreadcrumbAutomationPeer(Breadcrumb owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
    }
}

/// <summary>Wrapping row of crumbs (Arc: flex-wrap, 6px between crumbs, 2px between lines) that runs the path FLIP.</summary>
internal sealed class BreadcrumbPanel : Panel
{
    public static readonly StyledProperty<double> ItemSpacingProperty =
        AvaloniaProperty.Register<BreadcrumbPanel, double>(nameof(ItemSpacing), 6);

    public static readonly StyledProperty<double> LineSpacingProperty =
        AvaloniaProperty.Register<BreadcrumbPanel, double>(nameof(LineSpacing), 2);

    public double ItemSpacing { get => GetValue(ItemSpacingProperty); set => SetValue(ItemSpacingProperty, value); }

    public double LineSpacing { get => GetValue(LineSpacingProperty); set => SetValue(LineSpacingProperty, value); }

    static BreadcrumbPanel()
    {
        AffectsMeasure<BreadcrumbPanel>(ItemSpacingProperty, LineSpacingProperty);
    }

    protected override Size MeasureOverride(Size availableSize) => Flow(availableSize, arrange: false);

    protected override Size ArrangeOverride(Size finalSize)
    {
        Flow(finalSize, arrange: true);
        ((Parent as ItemsPresenter)?.TemplatedParent as Breadcrumb)?.ApplyFlip();
        return finalSize;
    }

    private Size Flow(Size size, bool arrange)
    {
        double x = 0, y = 0, line = 0, width = 0;
        var lineStart = 0;
        var children = Children;
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!arrange)
            {
                child.Measure(Size.Infinity);
            }
            if (!child.IsVisible)
            {
                continue;
            }
            var desired = child.DesiredSize;
            if (x > 0 && x + desired.Width > size.Width + 0.5)
            {
                if (arrange)
                {
                    ArrangeLine(lineStart, i, y, line);
                }
                y += line + LineSpacing;
                x = 0;
                line = 0;
                lineStart = i;
            }
            x += desired.Width + ItemSpacing;
            width = Math.Max(width, x - ItemSpacing);
            line = Math.Max(line, desired.Height);
        }
        if (arrange)
        {
            ArrangeLine(lineStart, children.Count, y, line);
        }
        return new Size(width, y + line);
    }

    private void ArrangeLine(int start, int end, double y, double height)
    {
        double x = 0;
        for (var i = start; i < end; i++)
        {
            var child = Children[i];
            if (!child.IsVisible)
            {
                continue;
            }
            var desired = child.DesiredSize;
            // align-items: center
            child.Arrange(new Rect(x, y + (height - desired.Height) / 2, desired.Width, desired.Height));
            x += desired.Width + ItemSpacing;
        }
    }
}
