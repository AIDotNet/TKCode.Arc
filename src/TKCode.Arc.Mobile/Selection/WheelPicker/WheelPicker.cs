using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Gestures;
using TKCode.Arc.Mobile.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile;

/// <summary>Event data of <see cref="WheelPicker.SelectionChanged"/>.</summary>
public sealed class WheelPickerSelectionChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data.</summary>
    public WheelPickerSelectionChangedEventArgs(RoutedEvent routedEvent, object? oldItem, object? newItem, int oldIndex, int newIndex)
        : base(routedEvent)
    {
        OldItem = oldItem;
        NewItem = newItem;
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    /// <summary>Previously selected item, or null.</summary>
    public object? OldItem { get; }

    /// <summary>Newly selected item, or null.</summary>
    public object? NewItem { get; }

    /// <summary>Previous index, or -1.</summary>
    public int OldIndex { get; }

    /// <summary>New index, or -1.</summary>
    public int NewIndex { get; }
}

/// <summary>
/// A mobile inertia wheel picker (similar to iOS <c>UIPickerView</c>): drag vertically with 1:1 tracking, rubber-band
/// resistance past either end, velocity-projected snapping onto discrete items, and a selection haptic tick as each
/// item crosses center (<see cref="Haptics"/>). Tap any visible item to scroll to it; mouse wheel and arrow keys also
/// work. Under reduced motion the wheel jumps directly to the chosen item.
/// </summary>
[TemplatePart("PART_Root", typeof(Control))]
[TemplatePart("PART_Band", typeof(Border))]
[TemplatePart("PART_Track", typeof(Canvas))]
public class WheelPicker : TemplatedControl
{
    private const double Stretch = 64;
    private static readonly TimeSpan Projection = TimeSpan.FromSeconds(0.22);

    /// <summary>Choices sequence; while null, <see cref="Items"/> holds the choices.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<WheelPicker, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Selected item (two-way).</summary>
    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<WheelPicker, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Selected index (two-way, -1 when empty).</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<WheelPicker, int>(nameof(SelectedIndex), -1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Optional binding that reads display text for each item.</summary>
    public static readonly StyledProperty<BindingBase?> DisplayMemberBindingProperty =
        AvaloniaProperty.Register<WheelPicker, BindingBase?>(nameof(DisplayMemberBinding));

    /// <summary>How many rows are visible at once (forced odd, minimum 3): 5.</summary>
    public static readonly StyledProperty<int> VisibleItemCountProperty =
        AvaloniaProperty.Register<WheelPicker, int>(nameof(VisibleItemCount), 5);

    /// <summary>Raised whenever the selected item changes.</summary>
    public static readonly RoutedEvent<WheelPickerSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<WheelPicker, WheelPickerSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    private readonly List<TextBlock> _labels = [];
    private Control? _root;
    private Border? _band;
    private Canvas? _track;
    private MotionValue<double>? _offset;
    private DragTracker? _drag;
    private List<object?>? _choices;
    private INotifyCollectionChanged? _watched;
    private bool _attached;
    private bool _syncing;
    private bool _dragging;
    private double _dragOrigin;
    private int _lastHapticIndex = -1;
    private object? _raisedItem;
    private int _raisedIndex = -1;

    /// <summary>Creates an empty wheel picker.</summary>
    public WheelPicker()
    {
        Items.CollectionChanged += (_, _) =>
        {
            if (ItemsSource is null)
            {
                OnChoicesChanged();
            }
        };
    }

    /// <summary>Inline choices written in XAML, used while <see cref="ItemsSource"/> is null.</summary>
    public AvaloniaList<object?> Items { get; } = [];

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <inheritdoc cref="SelectedItemProperty"/>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <inheritdoc cref="SelectedIndexProperty"/>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <inheritdoc cref="DisplayMemberBindingProperty"/>
    [AssignBinding]
    public BindingBase? DisplayMemberBinding
    {
        get => GetValue(DisplayMemberBindingProperty);
        set => SetValue(DisplayMemberBindingProperty, value);
    }

    /// <inheritdoc cref="VisibleItemCountProperty"/>
    public int VisibleItemCount
    {
        get => GetValue(VisibleItemCountProperty);
        set => SetValue(VisibleItemCountProperty, value);
    }

    /// <inheritdoc cref="SelectionChangedEvent"/>
    public event EventHandler<WheelPickerSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <summary>Current vertical scroll offset in px (index * ItemHeight). For tests and diagnostics.</summary>
    public double ScrollOffset => _offset?.Value ?? 0;

    protected override Type StyleKeyOverride => typeof(WheelPicker);

    private bool Reduced => ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full;

    private double ItemHeight =>
        this.TryFindResource(WheelPickerTokens.Key(WheelPickerTokenKind.ItemHeight), out var v) && v is double h && h > 0 ? h : 40;

    private double BandInset =>
        this.TryFindResource(WheelPickerTokens.Key(WheelPickerTokenKind.BandInset), out var v) && v is double d && d >= 0 ? d : 8;

    private int EffectiveVisibleCount
    {
        get
        {
            var n = Math.Max(3, VisibleItemCount);
            return n % 2 == 0 ? n + 1 : n;
        }
    }

    private List<object?> Choices
    {
        get
        {
            if (_choices is null)
            {
                _choices = [];
                foreach (var item in ItemsSource ?? Items)
                {
                    _choices.Add(item);
                }
            }
            return _choices;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Detach();
        _root = e.NameScope.Find<Control>("PART_Root");
        _band = e.NameScope.Find<Border>("PART_Band");
        _track = e.NameScope.Find<Canvas>("PART_Track");
        if (_root is null || _track is null)
        {
            return;
        }
        if (_band is not null)
        {
            _band.Margin = new Thickness(BandInset, 0);
        }
        UpdateHeight();
        var initial = Math.Max(0, SelectedIndex) * ItemHeight;
        _offset = new MotionValue<double>(_track, initial, MotionVectors.Double);
        _offset.Changed += _ => LayoutItems();
        _drag = new DragTracker(_root, new DragOptions { Axis = DragAxis.Vertical, Threshold = 4, HandledEventsToo = true })
        {
            CanBegin = _ => IsEffectivelyEnabled && Choices.Count > 1,
        };
        _drag.Started += (_, _) => BeginDrag();
        _drag.Moved += (_, args) => MoveDrag(args.Offset.Y);
        _drag.Completed += (_, args) => EndDrag(args.Velocity.Y, false);
        _drag.Cancelled += (_, _) => EndDrag(0, true);
        _root.PointerWheelChanged += OnWheel;
        _root.PointerPressed += OnRootPressed;
        RebuildItems();
        LayoutItems();
    }

    private void Detach()
    {
        _drag?.Dispose();
        _drag = null;
        _offset?.Dispose();
        _offset = null;
        if (_root is not null)
        {
            _root.PointerWheelChanged -= OnWheel;
            _root.PointerPressed -= OnRootPressed;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Watch();
        OnChoicesChanged();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Watch();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty)
        {
            Sync(fromIndex: true);
        }
        else if (change.Property == SelectedItemProperty)
        {
            Sync(fromIndex: false);
        }
        else if (change.Property == ItemsSourceProperty)
        {
            Watch();
            OnChoicesChanged();
        }
        else if (change.Property == DisplayMemberBindingProperty)
        {
            RebuildItems();
            LayoutItems();
        }
        else if (change.Property == VisibleItemCountProperty)
        {
            UpdateHeight();
            LayoutItems();
        }
        else if (change.Property == BoundsProperty)
        {
            LayoutItems();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEffectivelyEnabled || Choices.Count == 0)
        {
            return;
        }
        var last = Choices.Count - 1;
        switch (e.Key)
        {
            case Key.Up:
                SelectWithHaptic(Math.Max(0, SelectedIndex - 1));
                e.Handled = true;
                break;
            case Key.Down:
                SelectWithHaptic(Math.Min(last, Math.Max(0, SelectedIndex) + 1));
                e.Handled = true;
                break;
            case Key.Home:
                SelectWithHaptic(0);
                e.Handled = true;
                break;
            case Key.End:
                SelectWithHaptic(last);
                e.Handled = true;
                break;
        }
    }

    private void Watch()
    {
        if (_watched is not null)
        {
            _watched.CollectionChanged -= OnSourceChanged;
        }
        _watched = _attached ? ItemsSource as INotifyCollectionChanged : null;
        if (_watched is not null)
        {
            _watched.CollectionChanged += OnSourceChanged;
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnChoicesChanged();

    private void OnChoicesChanged()
    {
        _choices = null;
        var count = Choices.Count;
        if (count > 0 && SelectedIndex < 0 && SelectedItem is null)
        {
            SetCurrentValue(SelectedIndexProperty, 0);
        }
        else
        {
            Sync(fromIndex: SelectedItem is null);
        }
        RebuildItems();
        SnapVisualToSelection(animate: false);
    }

    private void Sync(bool fromIndex)
    {
        if (_syncing)
        {
            return;
        }
        _syncing = true;
        try
        {
            var choices = Choices;
            if (fromIndex)
            {
                var index = SelectedIndex;
                if (choices.Count == 0)
                {
                    SetCurrentValue(SelectedItemProperty, null);
                }
                else if (index >= 0 && index < choices.Count)
                {
                    SetCurrentValue(SelectedItemProperty, choices[index]);
                }
                else if (index >= choices.Count)
                {
                    SetCurrentValue(SelectedIndexProperty, choices.Count - 1);
                    SetCurrentValue(SelectedItemProperty, choices[^1]);
                }
                else
                {
                    SetCurrentValue(SelectedItemProperty, null);
                }
            }
            else
            {
                var idx = IndexOf(SelectedItem);
                SetCurrentValue(SelectedIndexProperty, idx);
            }
        }
        finally
        {
            _syncing = false;
        }
        OnSelectionSettled();
    }

    private int IndexOf(object? item)
    {
        if (item is null)
        {
            return -1;
        }
        var choices = Choices;
        for (var i = 0; i < choices.Count; i++)
        {
            if (Equals(choices[i], item))
            {
                return i;
            }
        }
        return -1;
    }

    private void OnSelectionSettled()
    {
        var item = SelectedItem;
        var index = SelectedIndex;
        AutomationProperties.SetHelpText(this, TextOf(item));
        if (!_dragging)
        {
            SnapVisualToSelection(animate: _attached);
        }
        if (Equals(item, _raisedItem) && index == _raisedIndex)
        {
            return;
        }
        var args = new WheelPickerSelectionChangedEventArgs(SelectionChangedEvent, _raisedItem, item, _raisedIndex, index);
        _raisedItem = item;
        _raisedIndex = index;
        RaiseEvent(args);
    }

    private void SelectWithHaptic(int index)
    {
        var clamped = Math.Clamp(index, 0, Math.Max(0, Choices.Count - 1));
        if (clamped != SelectedIndex)
        {
            Haptics.Trigger(HapticKind.Selection);
            SetCurrentValue(SelectedIndexProperty, clamped);
        }
    }

    private void UpdateHeight()
    {
        if (_root is not null)
        {
            _root.Height = EffectiveVisibleCount * ItemHeight;
        }
    }

    private void RebuildItems()
    {
        if (_track is null)
        {
            return;
        }
        _track.Children.Clear();
        _labels.Clear();
        var choices = Choices;
        var rowHeight = ItemHeight;
        for (var i = 0; i < choices.Count; i++)
        {
            var label = new TextBlock
            {
                Text = TextOf(choices[i]) ?? string.Empty,
                Height = rowHeight,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1,
                IsHitTestVisible = false,
            };
            _labels.Add(label);
            _track.Children.Add(label);
        }
    }

    private void LayoutItems()
    {
        if (_track is null || _labels.Count == 0)
        {
            return;
        }
        var rowHeight = ItemHeight;
        var width = Bounds.Width > 0 ? Bounds.Width : _track.Bounds.Width;
        var visible = EffectiveVisibleCount;
        var centerRow = (visible - 1) / 2.0;
        var centerY = centerRow * rowHeight;
        var offset = _offset?.Value ?? 0;

        for (var i = 0; i < _labels.Count; i++)
        {
            var label = _labels[i];
            var y = centerY + i * rowHeight - offset;
            var distance = Math.Abs(y - centerY) / rowHeight;
            label.Width = Math.Max(0, width - BandInset * 2);
            Canvas.SetLeft(label, BandInset);
            // Center the text vertically inside its slot height.
            Canvas.SetTop(label, y + (rowHeight - FontSize * 1.35) / 2);
            label.IsVisible = distance <= centerRow + 1.2;
            label.Opacity = Math.Clamp(1.0 - distance * 0.34, 0.15, 1.0);
            label.FontWeight = distance < 0.45 ? FontWeight.Medium : FontWeight.Normal;
        }
    }

    private void SnapVisualToSelection(bool animate)
    {
        if (_offset is null)
        {
            return;
        }
        var target = Math.Max(0, SelectedIndex) * ItemHeight;
        if (!animate || Reduced)
        {
            _offset.Stop();
            _offset.Jump(target);
            LayoutItems();
            return;
        }
        _ = _offset.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
    }

    private void BeginDrag()
    {
        if (_offset is null)
        {
            return;
        }
        _offset.Stop();
        _dragOrigin = _offset.Value;
        _lastHapticIndex = Math.Clamp((int)Math.Round(_offset.Value / ItemHeight), 0, Math.Max(0, Choices.Count - 1));
        _dragging = true;
    }

    private void MoveDrag(double deltaY)
    {
        if (!_dragging || _offset is null || Choices.Count == 0)
        {
            return;
        }
        var max = Math.Max(0, (Choices.Count - 1) * ItemHeight);
        // Dragging finger up (negative deltaY) moves towards later items (larger offset).
        var raw = _dragOrigin - deltaY;
        double clamped;
        if (raw < 0)
        {
            clamped = -RubberBand.Apply(-raw, Stretch);
        }
        else if (raw > max)
        {
            clamped = max + RubberBand.Apply(raw - max, Stretch);
        }
        else
        {
            clamped = raw;
        }
        var currentIndex = Math.Clamp((int)Math.Round(clamped / ItemHeight), 0, Choices.Count - 1);
        if (currentIndex != _lastHapticIndex)
        {
            _lastHapticIndex = currentIndex;
            Haptics.Trigger(HapticKind.Selection);
        }
        if (!Reduced)
        {
            _offset.Jump(clamped);
        }
    }

    private void EndDrag(double velocityY, bool cancelled)
    {
        if (!_dragging || _offset is null || Choices.Count == 0)
        {
            return;
        }
        _dragging = false;
        var rowHeight = ItemHeight;
        var count = Choices.Count;
        if (cancelled)
        {
            SnapVisualToSelection(animate: true);
            return;
        }
        // Invert velocity because upward drag (negative Y) increases scroll offset.
        var offsetVelocity = -velocityY;
        var projected = _offset.Value + offsetVelocity * Projection.TotalSeconds;
        var chosen = Math.Clamp((int)Math.Round(projected / rowHeight), 0, count - 1);
        SetCurrentValue(SelectedIndexProperty, chosen);
        var target = chosen * rowHeight;
        if (Reduced)
        {
            _offset.Jump(target);
            LayoutItems();
        }
        else
        {
            _ = MomentumProjector.SettleTo(_offset, target, offsetVelocity, ArcSpring.Smooth);
        }
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!IsEffectivelyEnabled || Choices.Count == 0 || Math.Abs(e.Delta.Y) < 0.01)
        {
            return;
        }
        var delta = e.Delta.Y < 0 ? 1 : -1;
        SelectWithHaptic(Math.Max(0, SelectedIndex) + delta);
        e.Handled = true;
    }

    private void OnRootPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || Choices.Count == 0 || _root is null)
        {
            return;
        }
        var pos = e.GetPosition(_root);
        var centerRow = (EffectiveVisibleCount - 1) / 2.0;
        var deltaRows = (int)Math.Round((pos.Y - (centerRow + 0.5) * ItemHeight) / ItemHeight);
        if (deltaRows != 0)
        {
            var baseIndex = Math.Max(0, SelectedIndex);
            var target = Math.Clamp(baseIndex + deltaRows, 0, Choices.Count - 1);
            SelectWithHaptic(target);
        }
    }

    private string? TextOf(object? item)
    {
        if (item is null)
        {
            return null;
        }
        if (DisplayMemberBinding is not { } binding)
        {
            return item.ToString();
        }
        var reader = new DisplayReader { DataContext = item };
        using (reader.Bind(DisplayReader.TextProperty, binding))
        {
            return reader.Text;
        }
    }

    private sealed class DisplayReader : StyledElement
    {
        public static readonly StyledProperty<string?> TextProperty =
            AvaloniaProperty.Register<DisplayReader, string?>(nameof(Text));

        public string? Text => GetValue(TextProperty);
    }
}
