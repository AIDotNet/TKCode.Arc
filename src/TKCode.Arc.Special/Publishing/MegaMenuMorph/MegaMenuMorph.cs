using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A site navigation bar whose dropdown is one panel that morphs. Hovering a top-level item opens its panel under the
/// bar; moving to a neighbour does not close and reopen it: the panel springs to the new item's size and place while the
/// old content slides out toward where the pointer came from and the new content slides in from where it went. Items
/// without a panel are plain links.
/// </summary>
/// <remarks>
/// The bar is a row of triggers with a soft highlight gliding under the pointer. The panel lives in an <c>ArcPopup</c>
/// so it floats over the page; it closes when the pointer has left both the bar and the panel for <see cref="CloseDelay"/>,
/// on Escape, on a click elsewhere, and after a link is chosen. Keyboard: Left / Right / Home / End move between triggers
/// (and carry an open panel along), Enter / Space toggle a panel, Down opens it and moves into it, Up / Down move between
/// its links and Escape closes it and returns to the trigger.
/// </remarks>
[TemplatePart(BarPart, typeof(StackPanel))]
[TemplatePart(HighlightPart, typeof(GlidingIndicator))]
[TemplatePart(PopupPart, typeof(ArcPopup))]
[TemplatePart(StagePart, typeof(MegaMenuMorphStage))]
[PseudoClasses(OpenPseudoClass)]
public class MegaMenuMorph : TemplatedControl
{
    internal const string BarPart = "PART_Bar";
    internal const string HighlightPart = "PART_Highlight";
    internal const string PopupPart = "PART_Popup";
    internal const string StagePart = "PART_Stage";
    internal const string OpenPseudoClass = ":open";

    /// <summary>The index of the item whose panel is open, or -1 when the panel is closed (two-way).</summary>
    public static readonly StyledProperty<int> OpenIndexProperty =
        AvaloniaProperty.Register<MegaMenuMorph, int>(nameof(OpenIndex), -1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How long the pointer rests on a trigger before a closed menu opens (moves between items while open are instant).</summary>
    public static readonly StyledProperty<TimeSpan> OpenDelayProperty =
        AvaloniaProperty.Register<MegaMenuMorph, TimeSpan>(nameof(OpenDelay), TimeSpan.FromMilliseconds(80));

    /// <summary>How long the pointer may be away from the bar and the panel before the panel closes.</summary>
    public static readonly StyledProperty<TimeSpan> CloseDelayProperty =
        AvaloniaProperty.Register<MegaMenuMorph, TimeSpan>(nameof(CloseDelay), TimeSpan.FromMilliseconds(220));

    /// <summary>The panel is never wider than this; content that wants more is measured at this width (and wraps).</summary>
    public static readonly StyledProperty<double> MaxPanelWidthProperty =
        AvaloniaProperty.Register<MegaMenuMorph, double>(nameof(MaxPanelWidth), 760);

    /// <summary>When false, hovering does nothing and panels open only on click, tap or the keyboard.</summary>
    public static readonly StyledProperty<bool> OpensOnHoverProperty =
        AvaloniaProperty.Register<MegaMenuMorph, bool>(nameof(OpensOnHover), true);

    /// <summary>The accessible name of the navigation landmark.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<MegaMenuMorph, string>(nameof(Label), "Main");

    /// <summary>Raised when a link inside a panel, or a top-level item without a panel, is activated. The panel closes after it.</summary>
    public static readonly RoutedEvent<MegaMenuMorphLinkInvokedEventArgs> LinkInvokedEvent =
        RoutedEvent.Register<MegaMenuMorph, MegaMenuMorphLinkInvokedEventArgs>(nameof(LinkInvoked), RoutingStrategies.Bubble);

    private readonly List<MegaMenuMorphTrigger> _triggers = [];
    private StackPanel? _bar;
    private GlidingIndicator? _highlight;
    private ArcPopup? _popup;
    private MegaMenuMorphStage? _stage;
    private DispatcherTimer? _openTimer;
    private DispatcherTimer? _closeTimer;
    private int _pendingOpen = -1;
    private int _hoverIndex = -1;
    private bool _pointerInPanel;
    private bool _syncingIndex;
    private bool _lastInputKeyboard;
    private TopLevel? _pressHost;

    static MegaMenuMorph()
    {
        ClipToBoundsProperty.OverrideDefaultValue<MegaMenuMorph>(false);
        FocusableProperty.OverrideDefaultValue<MegaMenuMorph>(false);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<MegaMenuMorph>(AutomationLandmarkType.Navigation);
    }

    public MegaMenuMorph()
    {
        Items.CollectionChanged += (_, _) => OnItemsChanged();
        AutomationProperties.SetName(this, Label);
        AddHandler(AvaloniaButton.ClickEvent, OnLinkClick, RoutingStrategies.Bubble);
        AddHandler(KeyDownEvent, (_, _) => _lastInputKeyboard = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, _) => _lastInputKeyboard = false, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>The top-level items, in bar order. Write them as children in XAML.</summary>
    [Content]
    public AvaloniaList<MegaMenuMorphItem> Items { get; } = [];

    /// <inheritdoc cref="OpenIndexProperty"/>
    public int OpenIndex
    {
        get => GetValue(OpenIndexProperty);
        set => SetValue(OpenIndexProperty, value);
    }

    /// <inheritdoc cref="OpenDelayProperty"/>
    public TimeSpan OpenDelay
    {
        get => GetValue(OpenDelayProperty);
        set => SetValue(OpenDelayProperty, value);
    }

    /// <inheritdoc cref="CloseDelayProperty"/>
    public TimeSpan CloseDelay
    {
        get => GetValue(CloseDelayProperty);
        set => SetValue(CloseDelayProperty, value);
    }

    /// <inheritdoc cref="MaxPanelWidthProperty"/>
    public double MaxPanelWidth
    {
        get => GetValue(MaxPanelWidthProperty);
        set => SetValue(MaxPanelWidthProperty, value);
    }

    /// <inheritdoc cref="OpensOnHoverProperty"/>
    public bool OpensOnHover
    {
        get => GetValue(OpensOnHoverProperty);
        set => SetValue(OpensOnHoverProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>True while a panel is open (<see cref="OpenIndex"/> is not -1).</summary>
    public bool IsOpen => OpenIndex >= 0;

    /// <summary>The open item, or null.</summary>
    public MegaMenuMorphItem? OpenItem => OpenIndex >= 0 && OpenIndex < Items.Count ? Items[OpenIndex] : null;

    /// <inheritdoc cref="LinkInvokedEvent"/>
    public event EventHandler<MegaMenuMorphLinkInvokedEventArgs>? LinkInvoked
    {
        add => AddHandler(LinkInvokedEvent, value);
        remove => RemoveHandler(LinkInvokedEvent, value);
    }

    internal MegaMenuMorphStage? Stage => _stage;

    internal IReadOnlyList<MegaMenuMorphTrigger> Triggers => _triggers;

    /// <summary>Opens the panel of the item at <paramref name="index"/> (an item without a panel is ignored).</summary>
    /// <param name="index">The item's position in <see cref="Items"/>.</param>
    /// <param name="focusPanel">Moves keyboard focus to the first link of the panel.</param>
    public void Open(int index, bool focusPanel = false)
    {
        if (index < 0 || index >= Items.Count || !Items[index].HasPanel)
        {
            return;
        }
        CancelTimers();
        OpenIndex = index;
        if (focusPanel)
        {
            Dispatcher.UIThread.Post(() => FocusPanel(first: true), DispatcherPriority.Input);
        }
    }

    /// <summary>Closes the open panel.</summary>
    public void Close()
    {
        CancelTimers();
        OpenIndex = -1;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_popup is not null)
        {
            _popup.Closed -= OnPopupClosed;
        }
        if (_stage is not null)
        {
            _stage.Hidden -= OnStageHidden;
            _stage.PointerEntered -= OnStagePointerEntered;
            _stage.PointerExited -= OnStagePointerExited;
            _stage.Place = null;
            _stage.Reset();
        }
        if (_bar is not null)
        {
            _bar.PointerExited -= OnBarPointerExited;
            _bar.Children.Clear();
        }
        _triggers.Clear();

        _bar = e.NameScope.Find<StackPanel>(BarPart);
        _highlight = e.NameScope.Find<GlidingIndicator>(HighlightPart);
        _popup = e.NameScope.Find<ArcPopup>(PopupPart);
        _stage = e.NameScope.Find<MegaMenuMorphStage>(StagePart);

        if (_bar is not null)
        {
            _bar.PointerExited += OnBarPointerExited;
        }
        if (_popup is not null)
        {
            _popup.PlacementTarget = this;
            _popup.Closed += OnPopupClosed;
            // No light dismiss: its transparent layer would cover the bar while the panel is open, so the trigger under
            // the pointer would lose it, the panel would close and reopen on the next move, over and over. Presses
            // outside the bar and the panel close it through WatchPresses instead.
            _popup.IsLightDismissEnabled = false;
            _popup.IsMotionEnabled = false;
            _popup.OverlayHostShadow = default;
            _popup.PopupRootShadow = default;
        }
        if (_stage is not null)
        {
            _stage.Popup = _popup;
            _stage.Hidden += OnStageHidden;
            _stage.PointerEntered += OnStagePointerEntered;
            _stage.PointerExited += OnStagePointerExited;
            _stage.Place = PlacePage;
        }
        BuildTriggers();
        if (OpenIndex >= 0)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!IsValidIndex(OpenIndex))
                {
                    SetCurrentValue(OpenIndexProperty, -1);
                    return;
                }
                if (OpenIndex >= 0 && _stage is { IsShown: false })
                {
                    PseudoClasses.Set(OpenPseudoClass, true);
                    ShowPanel(-1, OpenIndex, animate: false);
                }
            }, DispatcherPriority.Loaded);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OpenIndexProperty)
        {
            var (oldIndex, newIndex) = change.GetOldAndNewValue<int>();
            if (_syncingIndex)
            {
                return;
            }
            // Before the template applies (XAML sets attributes before it adds the items) the index waits to be checked.
            if (_bar is not null && !IsValidIndex(newIndex))
            {
                _syncingIndex = true;
                SetCurrentValue(OpenIndexProperty, -1);
                _syncingIndex = false;
                newIndex = -1;
                if (oldIndex == -1)
                {
                    return;
                }
            }
            PseudoClasses.Set(OpenPseudoClass, newIndex >= 0);
            ShowPanel(oldIndex, newIndex, animate: true);
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == MaxPanelWidthProperty && OpenIndex >= 0)
        {
            ShowPanel(OpenIndex, OpenIndex, animate: true);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CancelTimers();
        WatchPresses(false);
        if (OpenIndex >= 0)
        {
            SetCurrentValue(OpenIndexProperty, -1);
        }
        _stage?.Reset();
        if (_popup is not null)
        {
            _popup.IsOpen = false;
        }
    }

    private bool IsValidIndex(int index) => index == -1 || (index >= 0 && index < Items.Count && Items[index].HasPanel);

    internal void OnItemChanged(MegaMenuMorphItem item, AvaloniaProperty property)
    {
        var index = Items.IndexOf(item);
        if (index < 0 || index >= _triggers.Count)
        {
            return;
        }
        var trigger = _triggers[index];
        trigger.Header = item.Header;
        trigger.HasPanel = item.HasPanel;
        trigger.IsCurrent = item.IsCurrent;
        if (property == MegaMenuMorphItem.ContentProperty || property == MegaMenuMorphItem.ContentTemplateProperty)
        {
            _stage?.PageFor(item).Sync();
            if (OpenIndex == index)
            {
                if (item.HasPanel)
                {
                    ShowPanel(index, index, animate: true);
                }
                else
                {
                    Close();
                }
            }
        }
    }

    private void OnItemsChanged()
    {
        foreach (var item in Items)
        {
            item.Owner = this;
        }
        BuildTriggers();
        _stage?.Prune(Items);
        if (_bar is not null && !IsValidIndex(OpenIndex))
        {
            Close();
        }
    }

    private void BuildTriggers()
    {
        if (_bar is null)
        {
            return;
        }
        foreach (var trigger in _triggers)
        {
            trigger.Click -= OnTriggerClick;
            trigger.PointerEntered -= OnTriggerPointerEntered;
            trigger.GotFocus -= OnTriggerGotFocus;
        }
        _bar.Children.Clear();
        _triggers.Clear();
        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            item.Owner = this;
            var trigger = new MegaMenuMorphTrigger(this, item, i)
            {
                Header = item.Header,
                HasPanel = item.HasPanel,
                IsCurrent = item.IsCurrent,
                IsOpen = i == OpenIndex
            };
            trigger.Click += OnTriggerClick;
            trigger.PointerEntered += OnTriggerPointerEntered;
            trigger.GotFocus += OnTriggerGotFocus;
            _triggers.Add(trigger);
            _bar.Children.Add(trigger);
        }
    }

    // ---- Opening, morphing, closing ----

    private void ShowPanel(int oldIndex, int newIndex, bool animate)
    {
        for (var i = 0; i < _triggers.Count; i++)
        {
            _triggers[i].IsOpen = i == newIndex;
        }
        if (_stage is null || _popup is null)
        {
            return;
        }
        if (newIndex < 0)
        {
            WatchPresses(false);
            if (_hoverIndex < 0)
            {
                _highlight?.Hide();
            }
            _stage.Hide(animate && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off && _popup.IsOpen);
            if (!_popup.IsOpen)
            {
                _stage.Reset();
            }
            return;
        }

        var item = Items[newIndex];
        if (!_popup.IsOpen)
        {
            _popup.IsOpen = true;
        }
        WatchPresses(true);
        var page = _stage.PageFor(item);
        page.Sync();
        _stage.ContentMaxWidth = ContentMaxWidth();
        var size = _stage.MeasurePanel(page);
        var target = PlacePage(page, size);
        var motion = animate && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        _stage.Show(page, target, MegaMenuMorphMath.Direction(oldIndex, newIndex), motion);
        MoveHighlight(newIndex, animate: motion);
    }

    /// <summary>Centres a panel of <paramref name="size"/> under its trigger, inside the viewport's margins.</summary>
    private Rect PlacePage(MegaMenuMorphPage page, Size size)
    {
        var index = Items.IndexOf(page.Item);
        var anchor = index >= 0 && index < _triggers.Count ? TriggerRect(_triggers[index]).Center.X : Bounds.Width / 2;
        var (min, max) = HorizontalRange();
        return MegaMenuMorphMath.PanelRect(anchor, size, min, max);
    }

    /// <summary>The room the panel may use, in this control's coordinates: the viewport less its margins.</summary>
    private (double Min, double Max) HorizontalRange()
    {
        var margin = ViewportMargin();
        if (TopLevel.GetTopLevel(this) is { } top && ArcViewport.GetBounds(this) is { } viewport
            && this.TranslatePoint(default, top) is { } origin)
        {
            var min = viewport.Left + margin - origin.X;
            var max = viewport.Right - margin - origin.X;
            if (max > min)
            {
                return (min, max);
            }
        }
        return (0, Math.Max(Bounds.Width, MaxPanelWidth));
    }

    private double ContentMaxWidth()
    {
        var (min, max) = HorizontalRange();
        var padding = _stage?.PanelPadding ?? default;
        var width = Math.Min(MaxPanelWidth, max - min);
        return Math.Max(0, width - padding.Left - padding.Right);
    }

    private double ViewportMargin() =>
        this.TryFindResource(MegaMenuMorphTokens.Key(MegaMenuMorphTokenKind.ViewportMargin), out var value) && value is double margin
            ? margin
            : 12;

    private Rect TriggerRect(Control trigger) =>
        trigger.TranslatePoint(default, this) is { } p ? new Rect(p, trigger.Bounds.Size) : trigger.Bounds;

    private void MoveHighlight(int index, bool animate)
    {
        if (_highlight is null || index < 0 || index >= _triggers.Count)
        {
            return;
        }
        var trigger = _triggers[index];
        if (_highlight.GetVisualParent() is Visual parent && trigger.TranslatePoint(default, parent) is { } p)
        {
            _highlight.MoveTo(new Rect(p, trigger.Bounds.Size), animate && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off);
        }
    }

    private void OnStageHidden(object? sender, EventArgs e)
    {
        if (OpenIndex < 0 && _popup is not null)
        {
            _popup.IsOpen = false;
        }
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        // The popup can close on its own (the window lost its overlay); the menu follows.
        _stage?.Reset();
        if (OpenIndex >= 0)
        {
            SetCurrentValue(OpenIndexProperty, -1);
        }
    }

    // ---- Pointer ----

    private void OnTriggerPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not MegaMenuMorphTrigger trigger || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        _lastInputKeyboard = false;
        _hoverIndex = trigger.Index;
        CancelClose();
        MoveHighlight(trigger.Index, animate: true);
        if (!OpensOnHover)
        {
            return;
        }
        if (!trigger.HasPanel)
        {
            // A plain link under the pointer: an open panel stays until the pointer settles elsewhere.
            CancelOpen();
            if (OpenIndex >= 0)
            {
                ScheduleClose();
            }
            return;
        }
        if (OpenIndex >= 0)
        {
            CancelOpen();
            OpenIndex = trigger.Index;
        }
        else
        {
            ScheduleOpen(trigger.Index);
        }
    }

    private void OnBarPointerExited(object? sender, PointerEventArgs e)
    {
        _hoverIndex = -1;
        CancelOpen();
        if (OpenIndex >= 0)
        {
            if (OpensOnHover && e.Pointer.Type != PointerType.Touch)
            {
                ScheduleClose();
            }
        }
        else
        {
            _highlight?.Hide();
        }
    }

    private void OnStagePointerEntered(object? sender, PointerEventArgs e)
    {
        _pointerInPanel = true;
        CancelClose();
    }

    private void OnStagePointerExited(object? sender, PointerEventArgs e)
    {
        _pointerInPanel = false;
        if (OpenIndex >= 0 && OpensOnHover && e.Pointer.Type != PointerType.Touch && _hoverIndex < 0)
        {
            ScheduleClose();
        }
    }

    private void ScheduleOpen(int index)
    {
        _pendingOpen = index;
        if (OpenDelay <= TimeSpan.Zero)
        {
            OpenIndex = index;
            return;
        }
        _openTimer ??= new DispatcherTimer();
        _openTimer.Stop();
        _openTimer.Interval = OpenDelay;
        _openTimer.Tick -= OnOpenTimer;
        _openTimer.Tick += OnOpenTimer;
        _openTimer.Start();
    }

    private void OnOpenTimer(object? sender, EventArgs e)
    {
        _openTimer?.Stop();
        if (_pendingOpen >= 0 && _pendingOpen == _hoverIndex)
        {
            OpenIndex = _pendingOpen;
        }
        _pendingOpen = -1;
    }

    private void ScheduleClose()
    {
        if (CloseDelay <= TimeSpan.Zero)
        {
            Close();
            return;
        }
        _closeTimer ??= new DispatcherTimer();
        _closeTimer.Stop();
        _closeTimer.Interval = CloseDelay;
        _closeTimer.Tick -= OnCloseTimer;
        _closeTimer.Tick += OnCloseTimer;
        _closeTimer.Start();
    }

    private void OnCloseTimer(object? sender, EventArgs e)
    {
        _closeTimer?.Stop();
        if (_pointerInPanel || (_hoverIndex >= 0 && _hoverIndex == OpenIndex) || IsFocusInside())
        {
            return;
        }
        Close();
    }

    private void CancelOpen()
    {
        _pendingOpen = -1;
        _openTimer?.Stop();
    }

    private void CancelClose() => _closeTimer?.Stop();

    private void CancelTimers()
    {
        CancelOpen();
        CancelClose();
    }

    /// <summary>While open, a press outside the bar and the panel closes it (the panel floats; nothing else would).</summary>
    private void WatchPresses(bool watch)
    {
        var top = watch ? TopLevel.GetTopLevel(this) : null;
        if (top == _pressHost)
        {
            return;
        }
        _pressHost?.RemoveHandler(PointerPressedEvent, OnAnyPress);
        _pressHost = top;
        _pressHost?.AddHandler(PointerPressedEvent, OnAnyPress, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnAnyPress(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && (IsInside(source, this) || (_stage is not null && IsInside(source, _stage))))
        {
            return;
        }
        Close();
    }

    private static bool IsInside(Visual source, Visual root) => source == root || root.IsVisualAncestorOf(source);

    // ---- Clicks and keys ----

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MegaMenuMorphTrigger trigger)
        {
            return;
        }
        e.Handled = true;
        var item = trigger.Item;
        if (!item.HasPanel)
        {
            Close();
            RaiseEvent(new MegaMenuMorphLinkInvokedEventArgs(LinkInvokedEvent, item, null));
            return;
        }
        CancelTimers();
        // A click on the trigger that hovering just opened keeps it open; otherwise the click toggles.
        OpenIndex = OpenIndex == trigger.Index && (_lastInputKeyboard || !OpensOnHover || !trigger.IsPointerOver) ? -1 : trigger.Index;
    }

    private void OnLinkClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not MegaMenuMorphLink link || OpenItem is not { } item)
        {
            return;
        }
        var keyboard = _lastInputKeyboard;
        var index = OpenIndex;
        Close();
        if (keyboard && index >= 0 && index < _triggers.Count)
        {
            _triggers[index].Focus(NavigationMethod.Tab);
        }
        RaiseEvent(new MegaMenuMorphLinkInvokedEventArgs(LinkInvokedEvent, item, link));
    }

    private void OnTriggerGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is MegaMenuMorphTrigger trigger && e.NavigationMethod != NavigationMethod.Pointer)
        {
            MoveHighlight(trigger.Index, animate: true);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        _lastInputKeyboard = true;
        if (e.Source is Visual source && _stage is not null && IsInside(source, _stage))
        {
            HandlePanelKey(e);
            return;
        }
        if (e.Source is not Visual triggerSource || triggerSource.FindAncestorOfType<MegaMenuMorphTrigger>(includeSelf: true) is not { } trigger)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Right:
                MoveFocus(trigger.Index + 1);
                e.Handled = true;
                break;
            case Key.Left:
                MoveFocus(trigger.Index - 1);
                e.Handled = true;
                break;
            case Key.Home:
                MoveFocus(0);
                e.Handled = true;
                break;
            case Key.End:
                MoveFocus(_triggers.Count - 1);
                e.Handled = true;
                break;
            case Key.Down when trigger.HasPanel:
                Open(trigger.Index, focusPanel: true);
                e.Handled = true;
                break;
            case Key.Escape when OpenIndex >= 0:
                Close();
                e.Handled = true;
                break;
        }
    }

    private void MoveFocus(int index)
    {
        if (_triggers.Count == 0)
        {
            return;
        }
        index = (index % _triggers.Count + _triggers.Count) % _triggers.Count;
        var trigger = _triggers[index];
        trigger.Focus(NavigationMethod.Directional);
        if (OpenIndex >= 0)
        {
            if (trigger.HasPanel)
            {
                OpenIndex = index;
            }
            else
            {
                Close();
            }
        }
    }

    private void HandlePanelKey(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                ReturnToTrigger();
                e.Handled = true;
                break;
            case Key.Down:
                e.Handled = MovePanelFocus(+1);
                break;
            case Key.Up:
                e.Handled = MovePanelFocus(-1);
                break;
            case Key.Home:
                e.Handled = FocusPanel(first: true);
                break;
            case Key.End:
                e.Handled = FocusPanel(first: false);
                break;
            case Key.Tab:
                e.Handled = TabOutOfPanel((e.KeyModifiers & KeyModifiers.Shift) != 0);
                break;
        }
    }

    private void ReturnToTrigger()
    {
        var index = OpenIndex;
        if (index >= 0 && index < _triggers.Count)
        {
            _triggers[index].Focus(NavigationMethod.Tab);
        }
        Close();
    }

    /// <summary>The focusable controls of the open page, in tab order.</summary>
    private List<InputElement> PanelFocusables()
    {
        var list = new List<InputElement>();
        if (_stage?.CurrentPage is not { } page)
        {
            return list;
        }
        foreach (var visual in page.GetVisualDescendants())
        {
            if (visual is InputElement { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true } element
                && KeyboardNavigation.GetIsTabStop(element))
            {
                list.Add(element);
            }
        }
        return list;
    }

    private bool FocusPanel(bool first)
    {
        var items = PanelFocusables();
        if (items.Count == 0)
        {
            return false;
        }
        items[first ? 0 : items.Count - 1].Focus(NavigationMethod.Tab);
        return true;
    }

    private bool MovePanelFocus(int step)
    {
        var items = PanelFocusables();
        if (items.Count == 0)
        {
            return false;
        }
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        var current = items.FindIndex(i => i == focused || (focused is not null && i.IsVisualAncestorOf(focused)));
        var next = current < 0 ? 0 : Math.Clamp(current + step, 0, items.Count - 1);
        items[next].Focus(NavigationMethod.Tab);
        return true;
    }

    /// <summary>Tab past the last link leaves the panel for the next trigger; Shift+Tab before the first returns to the trigger.</summary>
    private bool TabOutOfPanel(bool backwards)
    {
        var items = PanelFocusables();
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        var current = items.FindIndex(i => i == focused || (focused is not null && i.IsVisualAncestorOf(focused)));
        if (current < 0)
        {
            return false;
        }
        if (backwards)
        {
            if (current > 0)
            {
                items[current - 1].Focus(NavigationMethod.Tab);
            }
            else if (OpenIndex >= 0 && OpenIndex < _triggers.Count)
            {
                _triggers[OpenIndex].Focus(NavigationMethod.Tab);
            }
            return true;
        }
        if (current < items.Count - 1)
        {
            items[current + 1].Focus(NavigationMethod.Tab);
            return true;
        }
        var nextIndex = OpenIndex + 1;
        Close();
        if (nextIndex < _triggers.Count)
        {
            _triggers[nextIndex].Focus(NavigationMethod.Tab);
        }
        else if (OpenIndex < _triggers.Count && _triggers.Count > 0)
        {
            _triggers[^1].Focus(NavigationMethod.Tab);
        }
        return true;
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (OpenIndex >= 0 && _lastInputKeyboard)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (OpenIndex >= 0 && !IsFocusInside() && !_pointerInPanel)
                {
                    Close();
                }
            }, DispatcherPriority.Input);
        }
    }

    private bool IsFocusInside()
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Visual focused)
        {
            return false;
        }
        return IsInside(focused, this) || (_stage is not null && IsInside(focused, _stage));
    }
}
