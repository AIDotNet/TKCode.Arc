using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="SegmentedControl.ItemIntent"/>.</summary>
public sealed class SegmentedItemIntentEventArgs : RoutedEventArgs
{
    public SegmentedItemIntentEventArgs(RoutedEvent routedEvent, object? item, object? value) : base(routedEvent)
    {
        Item = item;
        Value = value;
    }

    /// <summary>The option the pointer or focus reached.</summary>
    public object? Item { get; }

    /// <summary>Its value (the option's <c>Value</c>, or the item itself).</summary>
    public object? Value { get; }
}

/// <summary>
/// Arc segmented control: a row of buttons for switching between a small set of related views. One selection pill
/// glides between them on the morph spring; segments never scale or change weight, only their label color follows.
/// Arrows (wrapping), Home and End move the selection like a tab list and only the selected segment is a tab stop.
/// When the options are wider than the control the row scrolls inside itself with faded edges, and the selected
/// segment is always scrolled fully into view. <see cref="ItemIntent"/> fires when the pointer or focus reaches a
/// segment before it is chosen (preload what it shows).
/// </summary>
[TemplatePart(SelectionPart, typeof(GlidingIndicator))]
[TemplatePart(ScrollViewerPart, typeof(ScrollViewer))]
public class SegmentedControl : SelectingItemsControl
{
    public const string SelectionPart = "PART_Selection";
    public const string ScrollViewerPart = "PART_ScrollViewer";
    public const string TrackPart = "PART_Track";

    public static readonly RoutedEvent<SegmentedItemIntentEventArgs> ItemIntentEvent =
        RoutedEvent.Register<SegmentedControl, SegmentedItemIntentEventArgs>(nameof(ItemIntent), RoutingStrategies.Bubble);

    /// <summary>The pointer or keyboard focus reached a segment (it may not be selected yet).</summary>
    public event EventHandler<SegmentedItemIntentEventArgs>? ItemIntent
    {
        add => AddHandler(ItemIntentEvent, value);
        remove => RemoveHandler(ItemIntentEvent, value);
    }

    internal static readonly StyledProperty<double> EdgeFadeProperty =
        AvaloniaProperty.Register<SegmentedControl, double>(nameof(EdgeFade), 20);

    internal static readonly StyledProperty<double> ScrollRoomProperty =
        AvaloniaProperty.Register<SegmentedControl, double>(nameof(ScrollRoom), 20);

    internal double EdgeFade { get => GetValue(EdgeFadeProperty); set => SetValue(EdgeFadeProperty, value); }
    internal double ScrollRoom { get => GetValue(ScrollRoomProperty); set => SetValue(ScrollRoomProperty, value); }

    private readonly TogglesGroupHost _host;
    private ScrollViewer? _scrollViewer;
    private Control? _track;
    private LinearGradientBrush? _fadeMask;
    private MotionValue<double>? _scroll;
    private bool _scrolledOnce;

    static SegmentedControl()
    {
        FocusableProperty.OverrideDefaultValue<SegmentedControl>(false);
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<SegmentedControl>(KeyboardNavigationMode.Once);
    }

    public SegmentedControl()
    {
        _host = new TogglesGroupHost(this, flipInRtl: false, ApplyOption);
        SelectedValueBinding = TogglesValueBinding.Create();
        SelectionChanged += (_, _) =>
        {
            _host.OnSelectionChanged();
            ScrollSelectedIntoView();
        };
    }

    protected override Type StyleKeyOverride => typeof(SegmentedControl);

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<SegmentedItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new SegmentedItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        ApplyOption(container, item);
    }

    private static void ApplyOption(Control container, object? item)
    {
        if (container is SegmentedItem segment && item is SegmentedOption option)
        {
            segment.Content = option.Label ?? option.Value?.ToString();
            segment.Accessory = option.Accessory;
            segment.IsEnabled = !option.IsDisabled;
        }
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        _host.UpdateTabStop();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is SegmentedItem segment)
        {
            segment.ClearValue(ContentControl.ContentProperty);
            segment.ClearValue(SegmentedItem.AccessoryProperty);
            segment.ClearValue(IsEnabledProperty);
        }
    }

    internal void RaiseIntent(SegmentedItem segment)
    {
        var index = IndexFromContainer(segment);
        var item = index >= 0 && index < ItemCount ? Items[index] : null;
        RaiseEvent(new SegmentedItemIntentEventArgs(ItemIntentEvent, item, TogglesValueBinding.ValueOf(item)));
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged -= OnScrollChanged;
        }
        _scrollViewer = e.NameScope.Find<ScrollViewer>(ScrollViewerPart);
        _track = e.NameScope.Find<Control>(TrackPart);
        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged += OnScrollChanged;
        }
        // The pill's 1px stroke is centered half a pixel inside the segment, like a CSS border.
        _host.AttachIndicator(e.NameScope.Find<GlidingIndicator>(SelectionPart), inset: 0.5, fadeInFirst: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
        {
            _host.UpdateTabStop();
        }
    }

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new TogglesGroupAutomationPeer(this);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_host.HandleKey(e))
        {
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _host.UpdateTabStop();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        UpdateEdgeFade();
        ScrollSelectedIntoView();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => UpdateEdgeFade();

    /// <summary>Edges fade only on the side with more to see (Arc's mask-image on the scrolling track).</summary>
    private void UpdateEdgeFade()
    {
        if (_scrollViewer is not { } viewer)
        {
            return;
        }
        var width = viewer.Viewport.Width;
        var rest = viewer.Extent.Width - width - viewer.Offset.X;
        var start = viewer.Offset.X > 1 ? EdgeFade : 0;
        var end = rest > 1 ? EdgeFade : 0;
        if (width <= 0 || (start <= 0 && end <= 0))
        {
            viewer.OpacityMask = null;
            return;
        }
        _fadeMask ??= new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Colors.Transparent, 0),
                new GradientStop(Colors.Black, 0),
                new GradientStop(Colors.Black, 1),
                new GradientStop(Colors.Transparent, 1)
            }
        };
        _fadeMask.GradientStops[1].Offset = Math.Clamp(start / width, 0, 0.5);
        _fadeMask.GradientStops[2].Offset = Math.Clamp(1 - end / width, 0.5, 1);
        viewer.OpacityMask = _fadeMask;
    }

    /// <summary>Keeps the selected segment fully visible with a little room so it clears the fade.</summary>
    private void ScrollSelectedIntoView()
    {
        if (_scrollViewer is not { } viewer || _track is null || SelectedIndex < 0 ||
            ContainerFromIndex(SelectedIndex) is not { } segment || segment.TranslatePoint(default, _track) is not { } at)
        {
            return;
        }
        var viewport = viewer.Viewport.Width;
        if (viewer.Extent.Width <= viewport || viewport <= 0)
        {
            _scrolledOnce = true;
            return;
        }
        var room = ScrollRoom;
        var start = at.X - room;
        var end = at.X + segment.Bounds.Width + room - viewport;
        var offset = viewer.Offset.X;
        var target = offset > start ? start : offset < end ? end : offset;
        target = Math.Clamp(target, 0, Math.Max(0, viewer.Extent.Width - viewport));
        if (Math.Abs(target - offset) < 0.5)
        {
            _scrolledOnce = true;
            return;
        }
        _scroll ??= CreateScroll(viewer);
        if (!_scrolledOnce || !IsLoaded || !ArcMotion.IsFull(this))
        {
            _scroll.Jump(target);
        }
        else
        {
            _scroll.Jump(offset);
            _ = _scroll.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.InOut));
        }
        _scrolledOnce = true;
    }

    private MotionValue<double> CreateScroll(ScrollViewer viewer)
    {
        var value = new MotionValue<double>(this, viewer.Offset.X, MotionVectors.Double);
        value.Changed += x => viewer.Offset = new Vector(x, viewer.Offset.Y);
        return value;
    }
}
