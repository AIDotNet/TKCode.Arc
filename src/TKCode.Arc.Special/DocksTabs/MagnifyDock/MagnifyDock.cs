using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// An app dock: a row of icons on a shelf that swell like a lens under the pointer, pushing their neighbours aside, with
/// the name of the icon under the pointer above it and a bounce when an icon is launched.
/// </summary>
/// <remarks>
/// Items are <see cref="MagnifyDockItem"/>s written inline, or containers made for <c>ItemsSource</c> with an
/// <c>ItemTemplate</c> for the icon; set each container's <see cref="MagnifyDockItem.Label"/> and
/// <see cref="MagnifyDockItem.IsRunning"/> with bindings in <c>ItemContainerTheme</c>. The dock reserves the room its widest magnified row needs, so
/// the page never moves while it swells. The row is one Tab stop: the arrow keys, Home and End move between icons and
/// magnify the focused one; Enter or Space launches it. Under reduced motion icons keep their size, launches do not
/// bounce and the labels only fade.
/// </remarks>
[TemplatePart(ShelfPart, typeof(MagnifyDockShelf))]
[TemplatePart(LabelsPart, typeof(MagnifyDockLabels))]
public class MagnifyDock : ItemsControl
{
    public const string ShelfPart = "PART_Shelf";
    public const string LabelsPart = "PART_Labels";

    private const double LabelFadeSeconds = 0.12;

    /// <summary>The side of an icon at rest, in px.</summary>
    public static readonly StyledProperty<double> ItemSizeProperty =
        AvaloniaProperty.Register<MagnifyDock, double>(nameof(ItemSize), 48);

    /// <summary>The space between icons at rest, in px.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<MagnifyDock, double>(nameof(Gap), 8);

    /// <summary>The scale of the icon right under the pointer (1.8 by default; 1 turns magnification off).</summary>
    public static readonly StyledProperty<double> MagnificationProperty =
        AvaloniaProperty.Register<MagnifyDock, double>(nameof(Magnification), 1.8);

    /// <summary>How far from the pointer icons still grow, in px.</summary>
    public static readonly StyledProperty<double> ReachProperty =
        AvaloniaProperty.Register<MagnifyDock, double>(nameof(Reach), 150);

    /// <summary>Whether launching an icon marks it running (a dot under it).</summary>
    public static readonly StyledProperty<bool> MarksLaunchedProperty =
        AvaloniaProperty.Register<MagnifyDock, bool>(nameof(MarksLaunched), true);

    /// <summary>The space between the shelf's edge and the icons, in px.</summary>
    public static readonly StyledProperty<double> ShelfPaddingProperty =
        AvaloniaProperty.Register<MagnifyDock, double>(nameof(ShelfPadding), 8);

    /// <summary>The accessible name of the dock.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<MagnifyDock, string>(nameof(Label), "Dock");

    /// <summary>Raised when an icon is launched by a click, a tap, Enter or Space.</summary>
    public static readonly RoutedEvent<MagnifyDockItemEventArgs> ItemLaunchedEvent =
        RoutedEvent.Register<MagnifyDock, MagnifyDockItemEventArgs>(nameof(ItemLaunched), RoutingStrategies.Bubble);

    private static readonly FuncTemplate<Panel?> DefaultPanel = new(() => new MagnifyDockPanel());

    private readonly MagnifyDockModel _model = new();
    private readonly LiveLoop _loop;
    private MagnifyDockPanel? _panel;
    private MagnifyDockShelf? _shelf;
    private MagnifyDockLabels? _labels;
    private double _rowLeft;
    private int _labelIndex = -1;
    private int _pointerIndex = -1;
    private double _pointerAlong = double.NaN;
    private double _labelOpacity;
    private bool _ownsName;

    static MagnifyDock()
    {
        ItemsPanelProperty.OverrideDefaultValue<MagnifyDock>(DefaultPanel);
        ClipToBoundsProperty.OverrideDefaultValue<MagnifyDock>(false);
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<MagnifyDock>(KeyboardNavigationMode.Once);
    }

    public MagnifyDock()
    {
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        AddHandler(PointerMovedEvent, OnPointerMovedAnywhere, handledEventsToo: true);
        AddHandler(PointerExitedEvent, (_, _) => SetPointer(double.NaN, -1), handledEventsToo: true);
        AddHandler(AvaloniaButton.ClickEvent, OnItemClick);
        Items.CollectionChanged += (_, _) => _labels?.Reset();
    }

    /// <inheritdoc cref="ItemSizeProperty"/>
    public double ItemSize
    {
        get => GetValue(ItemSizeProperty);
        set => SetValue(ItemSizeProperty, value);
    }

    /// <inheritdoc cref="GapProperty"/>
    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    /// <inheritdoc cref="MagnificationProperty"/>
    public double Magnification
    {
        get => GetValue(MagnificationProperty);
        set => SetValue(MagnificationProperty, value);
    }

    /// <inheritdoc cref="ReachProperty"/>
    public double Reach
    {
        get => GetValue(ReachProperty);
        set => SetValue(ReachProperty, value);
    }

    /// <inheritdoc cref="MarksLaunchedProperty"/>
    public bool MarksLaunched
    {
        get => GetValue(MarksLaunchedProperty);
        set => SetValue(MarksLaunchedProperty, value);
    }

    /// <inheritdoc cref="ShelfPaddingProperty"/>
    public double ShelfPadding
    {
        get => GetValue(ShelfPaddingProperty);
        set => SetValue(ShelfPaddingProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="ItemLaunchedEvent"/>
    public event EventHandler<MagnifyDockItemEventArgs>? ItemLaunched
    {
        add => AddHandler(ItemLaunchedEvent, value);
        remove => RemoveHandler(ItemLaunchedEvent, value);
    }

    internal MagnifyDockModel Model => _model;

    /// <summary>The icon whose label shows, or -1.</summary>
    internal int LabelIndex => _labelIndex;

    internal double LabelOpacity => _labelOpacity;

    /// <summary>The row's current left edge, relative to its rest position.</summary>
    internal double RowLeft => _rowLeft;

    /// <summary>The room above the icons kept for the label pill.</summary>
    internal double LabelSpace => 40;

    /// <summary>Launches icon <paramref name="index"/>: it bounces, may be marked running, and <see cref="ItemLaunched"/> is raised.</summary>
    public void Launch(int index)
    {
        if (ContainerFromIndex(index) is not MagnifyDockItem item)
        {
            return;
        }
        if (ArcMotion.IsFull(this))
        {
            _model.Launch(index);
            Wake();
        }
        if (MarksLaunched)
        {
            item.SetCurrentValue(MagnifyDockItem.IsRunningProperty, true);
        }
        RaiseEvent(new MagnifyDockItemEventArgs(ItemLaunchedEvent, index, item, ItemFromContainer(item)));
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new MagnifyDockItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<MagnifyDockItem>(item, out recycleKey);

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is MagnifyDockItem dockItem)
        {
            dockItem.Dock = this;
        }
        _labels?.Reset();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is MagnifyDockItem dockItem)
        {
            dockItem.Dock = null;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_shelf is not null)
        {
            _shelf.Dock = null;
        }
        if (_labels is not null)
        {
            _labels.Dock = null;
        }
        _shelf = e.NameScope.Find<MagnifyDockShelf>(ShelfPart);
        _labels = e.NameScope.Find<MagnifyDockLabels>(LabelsPart);
        if (_shelf is not null)
        {
            _shelf.Dock = this;
        }
        if (_labels is not null)
        {
            _labels.Dock = this;
            _labels.Reset();
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        ApplySettings();
        UpdateName();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ItemSizeProperty || property == GapProperty || property == MagnificationProperty
            || property == ReachProperty || property == ShelfPaddingProperty)
        {
            ApplySettings();
            _panel?.InvalidateMeasure();
            Wake();
        }
        else if (property == LabelProperty)
        {
            UpdateName();
        }
        else if (property == ArcMotion.LevelProperty)
        {
            ApplySettings();
            _panel?.InvalidateMeasure();
            if (!ArcMotion.IsFull(this))
            {
                _model.Settle();
                ApplyPoses();
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _panel is null || _panel.Children.Count == 0)
        {
            return;
        }
        var count = _panel.Children.Count;
        var current = FocusedIndex();
        int? next = e.Key switch
        {
            Key.Left => current <= 0 ? count - 1 : current - 1,
            Key.Right => current < 0 || current >= count - 1 ? 0 : current + 1,
            Key.Home => 0,
            Key.End => count - 1,
            _ => null
        };
        if (next is { } index && _panel.Children[index] is MagnifyDockItem item)
        {
            e.Handled = true;
            item.Focus(NavigationMethod.Directional);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MagnifyDockAutomationPeer(this);

    internal void AttachPanel(MagnifyDockPanel panel) => _panel = panel;

    internal void DetachPanel(MagnifyDockPanel panel)
    {
        if (ReferenceEquals(_panel, panel))
        {
            _panel = null;
        }
    }

    /// <summary>The x of the rest row's left edge in a box <paramref name="width"/> wide (the row is centred).</summary>
    internal double RowOrigin(double width) => (width - _model.RestWidth) / 2;

    internal string? LabelOf(int index) => ContainerFromIndex(index) is MagnifyDockItem item ? item.Label : null;

    internal bool IsItemRunning(int index) => ContainerFromIndex(index) is MagnifyDockItem { IsRunning: true };

    internal void OnItemLabelChanged() => _labels?.Reset();

    internal void OnItemRunningChanged() => _shelf?.InvalidateVisual();

    internal void OnItemFocusChanged()
    {
        UpdateLens();
        Wake();
    }

    /// <summary>Writes every icon's transform from the model and redraws the shelf and the label.</summary>
    internal void ApplyPoses()
    {
        if (_panel is null)
        {
            return;
        }
        _rowLeft = _model.Layout();
        var children = _panel.Children;
        var count = Math.Min(children.Count, _model.Count);
        for (var i = 0; i < count; i++)
        {
            if (children[i] is MagnifyDockItem item)
            {
                item.Pose(_model.Center(i) - _model.RestCenter(i), _model.Scale(i), _model.Lift(i));
            }
        }
        _shelf?.InvalidateVisual();
        _labels?.InvalidateVisual();
    }

    private bool Step(double dt)
    {
        var moving = _model.Step(dt);
        var target = _labelIndex >= 0 ? 1.0 : 0.0;
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _labelOpacity = target;
        }
        else if (_labelOpacity != target)
        {
            var delta = dt / LabelFadeSeconds;
            _labelOpacity = target > _labelOpacity ? Math.Min(target, _labelOpacity + delta) : Math.Max(target, _labelOpacity - delta);
        }
        ApplyPoses();
        return moving || _labelOpacity != target;
    }

    private void Wake()
    {
        ApplySettings();
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off || VisualRoot is null)
        {
            _model.Settle();
            _labelOpacity = _labelIndex >= 0 ? 1 : 0;
            ApplyPoses();
            return;
        }
        _loop.Start();
    }

    private void OnPointerMovedAnywhere(object? sender, PointerEventArgs e)
    {
        if (_panel is null || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        var point = e.GetPosition(_panel);
        var along = point.X - RowOrigin(_panel.Bounds.Width);
        // Only the band the icons can grow into counts; the label room above the dock does not wake the lens.
        var top = _panel.Bounds.Height - ShelfPadding - _model.ItemSize * Math.Max(1, _model.Magnification) - ShelfPadding;
        if (point.Y < top)
        {
            SetPointer(double.NaN, -1);
            return;
        }
        SetPointer(along, HitIcon(along, point.Y));
    }

    /// <summary>The icon whose current (magnified) span covers <paramref name="along"/>, if the pointer is level with the icons.</summary>
    private int HitIcon(double along, double y)
    {
        if (_panel is null)
        {
            return -1;
        }
        var bottom = _panel.Bounds.Height - ShelfPadding;
        for (var i = 0; i < _model.Count; i++)
        {
            var size = _model.ItemSize * _model.Scale(i);
            if (Math.Abs(along - _model.Center(i)) <= size / 2 + Gap / 2 && y >= bottom - size - _model.Lift(i) - 4 && y <= bottom + ShelfPadding)
            {
                return i;
            }
        }
        return -1;
    }

    private void SetPointer(double along, int index)
    {
        _pointerIndex = index;
        _pointerAlong = along;
        UpdateLens();
        Wake();
    }

    /// <summary>The lens follows the pointer; without one it sits on the keyboard-focused icon.</summary>
    private void UpdateLens()
    {
        var focused = FocusedIndex();
        var keyboard = focused >= 0 && _panel is { } panel && panel.Children[focused].Classes.Contains(":focus-visible");
        if (double.IsNaN(_pointerAlong))
        {
            _model.Lens = keyboard ? _model.RestCenter(focused) : double.NaN;
        }
        else
        {
            _model.Lens = _pointerAlong;
        }
        _labelIndex = _pointerIndex >= 0 ? _pointerIndex : keyboard ? focused : -1;
    }

    private int FocusedIndex()
    {
        if (_panel is null)
        {
            return -1;
        }
        var children = _panel.Children;
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].IsFocused)
            {
                return i;
            }
        }
        return -1;
    }

    private void OnItemClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not MagnifyDockItem item || !ReferenceEquals(item.Dock, this))
        {
            return;
        }
        var index = IndexFromContainer(item);
        if (index >= 0)
        {
            Launch(index);
        }
    }

    private void ApplySettings()
    {
        _model.ItemSize = Math.Max(8, ItemSize);
        _model.Gap = Math.Max(0, Gap);
        _model.Reach = Math.Max(1, Reach);
        var full = ArcMotion.IsFull(this);
        _model.Magnification = full ? Math.Max(1, Magnification) : 1;
        _model.Springs = full;
    }

    private void UpdateName()
    {
        if (AutomationProperties.GetName(this) is not { Length: > 0 } || _ownsName)
        {
            _ownsName = true;
            AutomationProperties.SetName(this, Label);
        }
    }

    private sealed class MagnifyDockAutomationPeer(MagnifyDock owner) : ItemsControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolBar;
    }
}

/// <summary>The icon a <see cref="MagnifyDock"/> launched.</summary>
public sealed class MagnifyDockItemEventArgs : RoutedEventArgs
{
    public MagnifyDockItemEventArgs(RoutedEvent routedEvent, int index, MagnifyDockItem container, object? item) : base(routedEvent)
    {
        Index = index;
        Container = container;
        Item = item;
    }

    /// <summary>The icon's position in the dock.</summary>
    public int Index { get; }

    /// <summary>The icon's container.</summary>
    public MagnifyDockItem Container { get; }

    /// <summary>The data item the icon was made for (the container itself when written inline).</summary>
    public object? Item { get; }
}
