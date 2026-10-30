using TKCode.Arc.DesignTokens;
using System.Collections;
using System.Collections.Specialized;
using System.Windows.Input;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>
/// Arc expanding search: a search that waits as an icon button until it is needed (headers, toolbars). The
/// button morphs into the field on the morph spring (its corners follow the width, from a circle to the control
/// radius), results unfold beneath it in groups with the match in medium weight, and a highlight glides between
/// them on the snappy spring. ArrowUp / ArrowDown move (wrapping), Enter chooses, Escape folds it back into the
/// icon; leaving an empty field folds it too. Before anything is typed it shows <see cref="Suggestions"/>.
/// Place it in the lane the field may grow into: it fills that lane's width and keeps the button on the
/// <see cref="Anchor"/> edge; the field never grows past <see cref="ExpandedWidth"/> or the lane.
/// </summary>
[TemplatePart(ShellHostPart, typeof(Panel))]
[TemplatePart(ShellPart, typeof(Border))]
[TemplatePart(FocusRingPart, typeof(Border))]
[TemplatePart(TriggerPart, typeof(AvaloniaButton))]
[TemplatePart(InputPart, typeof(AvaloniaTextBox))]
[TemplatePart(ClearPresencePart, typeof(PresenceHost))]
[TemplatePart(ClearButtonPart, typeof(AvaloniaButton))]
[TemplatePart(StatusPart, typeof(TextBlock))]
[TemplatePart(PanelAnchorPart, typeof(Control))]
[PseudoClasses(ExpandedPseudoClass, InputFocusPseudoClass, TriggerFocusPseudoClass, TriggerPressedPseudoClass)]
public class ExpandingSearch : TemplatedControl
{
    public const string ShellHostPart = "PART_ShellHost";
    public const string ShellPart = "PART_Shell";
    public const string FocusRingPart = "PART_FocusRing";
    public const string TriggerPart = "PART_Trigger";
    public const string InputPart = "PART_Input";
    public const string ClearPresencePart = "PART_ClearPresence";
    public const string ClearButtonPart = "PART_ClearButton";
    public const string StatusPart = "PART_Status";
    public const string PanelAnchorPart = "PART_PanelAnchor";

    /// <summary>Room around the floating panel for its shadow (the popup surface itself is transparent there).</summary>
    private static readonly Thickness PanelShadowRoom = new(32, 0, 32, 32);

    public const string ExpandedPseudoClass = ":expanded";
    public const string InputFocusPseudoClass = ":input-focus";
    public const string TriggerFocusPseudoClass = ":trigger-focus";
    public const string TriggerPressedPseudoClass = ":trigger-pressed";

    /// <summary>Names the button, the field and the results, such as "Search projects and docs".</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ExpandingSearch, string>(nameof(Label), "Search");

    /// <summary>Items to search (alternatively add them inline as children).</summary>
    public static readonly StyledProperty<IEnumerable<ExpandingSearchItem>?> ItemsSourceProperty =
        AvaloniaProperty.Register<ExpandingSearch, IEnumerable<ExpandingSearchItem>?>(nameof(ItemsSource));

    /// <summary>Shown before anything is typed, such as recent searches.</summary>
    public static readonly StyledProperty<IEnumerable<ExpandingSearchItem>?> SuggestionsProperty =
        AvaloniaProperty.Register<ExpandingSearch, IEnumerable<ExpandingSearchItem>?>(nameof(Suggestions));

    /// <summary>Heading of the suggestions. Default "Recent".</summary>
    public static readonly StyledProperty<string> SuggestionsLabelProperty =
        AvaloniaProperty.Register<ExpandingSearch, string>(nameof(SuggestionsLabel), "Recent");

    /// <summary>Hint in the open field; null shows <see cref="Label"/>.</summary>
    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<ExpandingSearch, string?>(nameof(PlaceholderText));

    /// <summary>The widest the field grows (it never grows past its lane). Default 360.</summary>
    public static readonly StyledProperty<double> ExpandedWidthProperty =
        AvaloniaProperty.Register<ExpandingSearch, double>(nameof(ExpandedWidth), 360);

    /// <summary>Most results shown. Default 6.</summary>
    public static readonly StyledProperty<int> MaxResultsProperty =
        AvaloniaProperty.Register<ExpandingSearch, int>(nameof(MaxResults), 6);

    /// <summary>The edge the button sits on. Default End.</summary>
    public static readonly StyledProperty<ExpandingSearchAnchor> AnchorProperty =
        AvaloniaProperty.Register<ExpandingSearch, ExpandingSearchAnchor>(nameof(Anchor), ExpandingSearchAnchor.End);

    /// <summary>A short tip under the empty state.</summary>
    public static readonly StyledProperty<string> EmptyHintProperty =
        AvaloniaProperty.Register<ExpandingSearch, string>(nameof(EmptyHint), "Try a shorter word or check the spelling.");

    /// <summary>Whether the field is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ExpandingSearch, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The text in the field (two-way).</summary>
    public static readonly StyledProperty<string> QueryProperty =
        AvaloniaProperty.Register<ExpandingSearch, string>(nameof(Query), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Executed with the chosen item.</summary>
    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<ExpandingSearch, ICommand?>(nameof(SelectCommand));

    /// <summary>Raised when a result or suggestion is chosen (after the field folds back).</summary>
    public static readonly RoutedEvent<ExpandingSearchItemSelectedEventArgs> ItemSelectedEvent =
        RoutedEvent.Register<ExpandingSearch, ExpandingSearchItemSelectedEventArgs>(nameof(ItemSelected), RoutingStrategies.Bubble);

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public IEnumerable<ExpandingSearchItem>? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public IEnumerable<ExpandingSearchItem>? Suggestions { get => GetValue(SuggestionsProperty); set => SetValue(SuggestionsProperty, value); }
    public string SuggestionsLabel { get => GetValue(SuggestionsLabelProperty); set => SetValue(SuggestionsLabelProperty, value); }
    public string? PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }
    public double ExpandedWidth { get => GetValue(ExpandedWidthProperty); set => SetValue(ExpandedWidthProperty, value); }
    public int MaxResults { get => GetValue(MaxResultsProperty); set => SetValue(MaxResultsProperty, value); }
    public ExpandingSearchAnchor Anchor { get => GetValue(AnchorProperty); set => SetValue(AnchorProperty, value); }
    public string EmptyHint { get => GetValue(EmptyHintProperty); set => SetValue(EmptyHintProperty, value); }
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public string Query { get => GetValue(QueryProperty); set => SetValue(QueryProperty, value); }
    public ICommand? SelectCommand { get => GetValue(SelectCommandProperty); set => SetValue(SelectCommandProperty, value); }

    /// <summary>Inline items (used when <see cref="ItemsSource"/> is null).</summary>
    [Content]
    public AvaloniaList<ExpandingSearchItem> Items { get; } = new();

    /// <summary>Raised when a result or suggestion is chosen.</summary>
    public event EventHandler<ExpandingSearchItemSelectedEventArgs>? ItemSelected
    {
        add => AddHandler(ItemSelectedEvent, value);
        remove => RemoveHandler(ItemSelectedEvent, value);
    }

    /// <summary>Whether the results panel is open.</summary>
    public bool IsPanelOpen => _popup?.IsOpen == true;

    /// <summary>The items currently listed (suggestions or results), in order.</summary>
    public IReadOnlyList<ExpandingSearchItem> VisibleItems => _flat;

    /// <summary>Index of the highlighted item in <see cref="VisibleItems"/>, or -1.</summary>
    public int ActiveIndex => _flat.Count == 0 ? -1 : Math.Min(_active, _flat.Count - 1);

    private enum ViewMode
    {
        None,
        Suggestions,
        Results,
        Empty
    }

    // The shell morphs between a pill (collapsed) and RadiusControl (open); both come from the tokens, so a square corner
    // style flattens it. The defaults apply until the tokens are read.
    private double _openRadius = 18;
    private double _pillRadius = double.PositiveInfinity;

    private Panel? _shellHost;
    private Border? _shell;
    private Border? _focusRing;
    private AvaloniaButton? _trigger;
    private AvaloniaTextBox? _input;
    private PresenceHost? _clearPresence;
    private AvaloniaButton? _clearButton;
    private TextBlock? _status;
    private Control? _panelAnchor;

    private ArcPopup? _popup;
    private Panel? _panelLane;
    private Border? _panelSurface;
    private SpringSizeDecorator? _panelHeight;
    private ScrollViewer? _panelScroll;
    private Border? _panelBody;
    private ArcContentSwap? _panelView;
    private Panel? _listHost;
    private StackPanel? _listStack;
    private GlidingIndicator? _highlight;
    private StackPanel? _emptyView;
    private TextBlock? _emptyTitle;
    private TextBlock? _emptyHint;

    private MotionValue<double>? _width;
    private MotionValue<double>? _inputOpacity;
    private double _collapsed = 44;
    private double _full = 360;
    private bool _open;
    private bool _focused;
    private int _active = -1;
    private bool _keyboard;
    private ViewMode _mode;
    private List<ExpandingSearchItem> _flat = new();
    private readonly List<ExpandingSearchOption> _rows = new();
    private string _layoutKey = string.Empty;
    private (string Id, string Set)? _lastHighlight;
    private bool _syncing;

    static ExpandingSearch()
    {
        AffectsMeasure<ExpandingSearch>(ExpandedWidthProperty);
        FocusableProperty.OverrideDefaultValue<ExpandingSearch>(false);
        FocusModality.Watch();
    }

    public ExpandingSearch()
    {
        Items.CollectionChanged += OnItemsChanged;
    }

    private IReadOnlyList<ExpandingSearchItem> SourceItems => ItemsSource switch
    {
        null => Items,
        IReadOnlyList<ExpandingSearchItem> list => list,
        { } items => items.ToList()
    };

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshPanel();

    // ---- template ------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        Detach();
        base.OnApplyTemplate(e);
        _shellHost = e.NameScope.Find<Panel>(ShellHostPart);
        _shell = e.NameScope.Find<Border>(ShellPart);
        _focusRing = e.NameScope.Find<Border>(FocusRingPart);
        _trigger = e.NameScope.Find<AvaloniaButton>(TriggerPart);
        _input = e.NameScope.Find<AvaloniaTextBox>(InputPart);
        _clearPresence = e.NameScope.Find<PresenceHost>(ClearPresencePart);
        _clearButton = e.NameScope.Find<AvaloniaButton>(ClearButtonPart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        _panelAnchor = e.NameScope.Find<Control>(PanelAnchorPart);
        if (_popup is not null)
        {
            _popup.PlacementTarget = _panelAnchor;
        }
        if (_trigger is not null)
        {
            _trigger.Click += OnTriggerClick;
            _trigger.PropertyChanged += OnTriggerPropertyChanged;
            _trigger.GotFocus += OnTriggerGotFocus;
            _trigger.LostFocus += OnTriggerLostFocus;
        }
        if (_input is not null)
        {
            _input.GotFocus += OnInputGotFocus;
            _input.LostFocus += OnInputLostFocus;
            _input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
            _input.PropertyChanged += OnInputPropertyChanged;
            _input.Text = Query;
        }
        if (_clearButton is not null)
        {
            _clearButton.Click += OnClearClick;
            AutomationProperties.SetName(_clearButton, "Clear search");
        }
        UpdateAutomation();
        ApplyExpandedState(animate: false);
    }

    private void Detach()
    {
        if (_trigger is not null)
        {
            _trigger.Click -= OnTriggerClick;
            _trigger.PropertyChanged -= OnTriggerPropertyChanged;
            _trigger.GotFocus -= OnTriggerGotFocus;
            _trigger.LostFocus -= OnTriggerLostFocus;
        }
        if (_input is not null)
        {
            _input.GotFocus -= OnInputGotFocus;
            _input.LostFocus -= OnInputLostFocus;
            _input.RemoveHandler(KeyDownEvent, OnInputKeyDown);
            _input.PropertyChanged -= OnInputPropertyChanged;
        }
        if (_clearButton is not null)
        {
            _clearButton.Click -= OnClearClick;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty && !_syncing)
        {
            if (IsExpanded)
            {
                Expand();
            }
            else
            {
                Collapse(returnFocus: false);
            }
        }
        else if (change.Property == QueryProperty && _input is not null && _input.Text != Query)
        {
            _input.Text = Query;
        }
        else if (change.Property == ItemsSourceProperty || change.Property == SuggestionsProperty ||
                 change.Property == SuggestionsLabelProperty || change.Property == MaxResultsProperty ||
                 change.Property == EmptyHintProperty)
        {
            RefreshPanel();
        }
        else if (change.Property == LabelProperty || change.Property == PlaceholderTextProperty)
        {
            UpdateAutomation();
        }
        else if (change.Property == AnchorProperty && _panelSurface is not null)
        {
            _panelSurface.HorizontalAlignment = SurfaceAlignment;
        }
    }

    private void UpdateAutomation()
    {
        if (_trigger is not null)
        {
            AutomationProperties.SetName(_trigger, Label);
        }
        if (_input is not null)
        {
            AutomationProperties.SetName(_input, Label);
            _input.PlaceholderText = PlaceholderText ?? Label;
        }
    }

    // ---- size ----------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        // The lane keeps its size while the shell morphs inside it, so nothing outside relayouts per frame.
        var measured = base.MeasureOverride(availableSize);
        var width = double.IsInfinity(availableSize.Width) ? Math.Max(ExpandedWidth, measured.Width) : Math.Min(availableSize.Width, Math.Max(ExpandedWidth, _collapsed));
        return new Size(width, Math.Max(measured.Height, _collapsed));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        var collapsed = _shellHost?.Bounds.Height is > 0 and var h ? h : _collapsed;
        _collapsed = collapsed;
        var full = Math.Max(collapsed, Math.Min(ExpandedWidth, finalSize.Width));
        if (Math.Abs(full - _full) > 0.5 || _width is null)
        {
            _full = full;
            // Lane resizes are followed at once; they are not the person's action.
            var goal = _open ? full : collapsed;
            if (_width is null)
            {
                EnsureWidth(goal);
            }
            else if (_width.IsAnimating)
            {
                _ = _width.AnimateTo(goal, MotionSpec.Spring(ArcSpring.Morph));
            }
            else
            {
                _width.Jump(goal);
            }
            if (_panelBody is not null)
            {
                _panelBody.Width = Math.Max(0, full - 2);
            }
            if (_panelLane is not null)
            {
                _panelLane.Width = full;
            }
            if (_panelAnchor is not null)
            {
                _panelAnchor.Width = full;
            }
        }
        return result;
    }

    private void RefreshRadii()
    {
        _openRadius = ArcTokens.GetCornerRadius(this, ArcDesignTokenKind.RadiusControl, new CornerRadius(18)).TopLeft;
        _pillRadius = ArcTokens.GetCornerRadius(this, ArcDesignTokenKind.RadiusPill, new CornerRadius(9999)).TopLeft;
    }

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e)
    {
        RefreshRadii();
        if (_width is not null)
        {
            SyncShape(_width.Value);
        }
    }

    private void EnsureWidth(double initial)
    {
        if (_width is not null)
        {
            return;
        }
        RefreshRadii();
        _width = new MotionValue<double>(this, initial, MotionVectors.Double);
        _width.Changed += SyncShape;
        SyncShape(initial);
    }

    /// <summary>The radius rides the same spring as the width, so the two never drift apart.</summary>
    private void SyncShape(double width)
    {
        if (_shellHost is not null)
        {
            _shellHost.Width = Math.Max(_collapsed, width);
        }
        var open = Math.Clamp((width - _collapsed) / Math.Max(1, _full - _collapsed), 0, 1);
        var half = Math.Min(_collapsed / 2, _pillRadius);
        var radius = half - (half - Math.Min(_openRadius, half)) * open;
        if (_shell is not null)
        {
            _shell.CornerRadius = new CornerRadius(radius);
        }
        if (_focusRing is not null)
        {
            _focusRing.CornerRadius = new CornerRadius(radius > 0 ? radius + 3 : 0);
        }
        if (_panelSurface is not null)
        {
            _panelSurface.Width = Math.Max(_collapsed, width);
        }
    }

    private void MorphTo(double goal)
    {
        EnsureWidth(_open ? _collapsed : _full);
        if (!IsLoaded || !ArcMotion.IsFull(this))
        {
            _width!.Jump(goal);
            return;
        }
        // Opening grows with a hint of overshoot; folding back settles without one, so the circle never dips below its size.
        _ = _width!.AnimateTo(goal, MotionSpec.Spring(goal >= _width.Value ? ArcSpring.Morph : ArcSpring.Smooth));
    }

    // ---- expand / collapse --------------------------------------------------------------------------------------

    /// <summary>Opens the field and moves focus into it.</summary>
    public void Expand()
    {
        if (_open)
        {
            return;
        }
        _open = true;
        _focused = true;
        SyncIsExpanded(true);
        ApplyExpandedState(animate: true);
        _input?.Focus(NavigationMethod.Pointer);
        MorphTo(_full);
        RefreshPanel();
    }

    /// <summary>Folds the field back into the icon, clearing the query.</summary>
    public void Collapse(bool returnFocus = true)
    {
        if (!_open)
        {
            return;
        }
        _open = false;
        _focused = false;
        _active = -1;
        SyncIsExpanded(false);
        if (_input is not null)
        {
            _input.Text = string.Empty;
        }
        ApplyExpandedState(animate: true);
        if (returnFocus)
        {
            // A result clicked with the mouse hands focus back without the keyboard ring.
            _trigger?.Focus(FocusModality.For(this));
        }
        MorphTo(_collapsed);
        RefreshPanel();
    }

    private void SyncIsExpanded(bool value)
    {
        _syncing = true;
        try
        {
            SetCurrentValue(IsExpandedProperty, value);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void ApplyExpandedState(bool animate)
    {
        PseudoClasses.Set(ExpandedPseudoClass, _open);
        if (_trigger is not null)
        {
            _trigger.IsVisible = !_open;
        }
        if (_input is not null)
        {
            // Inert while collapsed: not focusable, not hit-testable.
            _input.IsHitTestVisible = _open;
            _input.Focusable = _open;
            _input.IsTabStop = _open;
            _inputOpacity ??= CreateInputOpacity();
            var target = _open ? 1.0 : 0.0;
            var level = animate && IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
            if (level == ArcMotionLevel.Off)
            {
                _inputOpacity.Jump(target);
            }
            else if (level == ArcMotionLevel.Reduced)
            {
                _ = _inputOpacity.AnimateTo(target, MotionSpec.Tween(TimeSpan.FromMilliseconds(_open ? 150 : 100), _open ? ArcEasings.Enter : ArcEasings.Standard));
            }
            else
            {
                // The placeholder waits for the shape to start moving, then fades in; on close it leaves first.
                _ = _inputOpacity.AnimateTo(target, _open
                    ? MotionSpec.Tween(TimeSpan.FromMilliseconds(200), ArcEasings.Enter, TimeSpan.FromMilliseconds(80))
                    : MotionSpec.Tween(TimeSpan.FromMilliseconds(80), ArcEasings.Standard));
            }
        }
        UpdateClear();
    }

    private MotionValue<double> CreateInputOpacity()
    {
        var value = new MotionValue<double>(this, _open ? 1 : 0, MotionVectors.Double);
        value.Changed += o =>
        {
            if (_input is not null)
            {
                _input.Opacity = o;
            }
        };
        if (_input is not null)
        {
            _input.Opacity = value.Value;
        }
        return value;
    }

    private void UpdateClear()
    {
        if (_clearPresence is not null)
        {
            _clearPresence.IsPresent = _open && !string.IsNullOrEmpty(_input?.Text);
        }
    }

    // ---- input ---------------------------------------------------------------------------------------------------

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        Expand();
        e.Handled = true;
    }

    private void OnTriggerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AvaloniaButton.IsPressedProperty)
        {
            PseudoClasses.Set(TriggerPressedPseudoClass, _trigger?.IsPressed == true);
        }
    }

    // The shell draws the trigger's focus ring for keyboard focus only (Arc: .trigger:focus-visible).
    private void OnTriggerGotFocus(object? sender, FocusChangedEventArgs e)
        => PseudoClasses.Set(TriggerFocusPseudoClass, e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional);

    private void OnTriggerLostFocus(object? sender, RoutedEventArgs e) => PseudoClasses.Set(TriggerFocusPseudoClass, false);

    private void OnInputGotFocus(object? sender, FocusChangedEventArgs e)
    {
        PseudoClasses.Set(InputFocusPseudoClass, true);
        if (_open && !_focused)
        {
            _focused = true;
            RefreshPanel();
        }
    }

    private void OnInputLostFocus(object? sender, FocusChangedEventArgs e)
    {
        PseudoClasses.Set(InputFocusPseudoClass, false);
        // Switching windows keeps the search as it was; the field refocuses on return.
        if (TopLevel.GetTopLevel(this) is WindowBase { IsActive: false })
        {
            return;
        }
        _focused = false;
        if (_open && string.IsNullOrWhiteSpace(_input?.Text))
        {
            Collapse(returnFocus: false);
            return;
        }
        RefreshPanel();
    }

    private void OnInputPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != AvaloniaTextBox.TextProperty)
        {
            return;
        }
        var text = _input?.Text ?? string.Empty;
        if (Query != text)
        {
            SetCurrentValue(QueryProperty, text);
        }
        _active = text.Trim().Length > 0 ? 0 : -1;
        _keyboard = false;
        UpdateClear();
        RefreshPanel();
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        if (_input is not null)
        {
            _input.Text = string.Empty;
            _input.Focus(NavigationMethod.Pointer);
        }
        _active = -1;
        e.Handled = true;
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Collapse(returnFocus: true);
            return;
        }
        if (!IsPanelOpen || _flat.Count == 0)
        {
            return;
        }
        if (e.Key is Key.Down or Key.Up)
        {
            e.Handled = true;
            var step = e.Key == Key.Down ? 1 : -1;
            var current = ActiveIndex;
            _keyboard = true;
            _active = current < 0 ? (step > 0 ? 0 : _flat.Count - 1) : (current + step + _flat.Count) % _flat.Count;
            UpdateActive();
        }
        else if (e.Key == Key.Enter && ActiveIndex >= 0)
        {
            e.Handled = true;
            Choose(_flat[ActiveIndex]);
        }
    }

    private void Choose(ExpandingSearchItem item)
    {
        Collapse(returnFocus: true);
        RaiseEvent(new ExpandingSearchItemSelectedEventArgs(ItemSelectedEvent, item));
        ExpandingSearchCommands.Execute(SelectCommand, item);
    }

    // ---- results panel -------------------------------------------------------------------------------------------

    private HorizontalAlignment SurfaceAlignment => Anchor == ExpandingSearchAnchor.Start ? HorizontalAlignment.Left : HorizontalAlignment.Right;

    private void RefreshPanel()
    {
        var query = (_input?.Text ?? string.Empty).Trim();
        var source = SourceItems;
        var suggestions = Suggestions?.ToList() ?? new List<ExpandingSearchItem>();
        List<(string Name, List<ExpandingSearchItem> Items)> groups;
        ViewMode mode;
        if (query.Length == 0)
        {
            mode = suggestions.Count > 0 ? ViewMode.Suggestions : ViewMode.None;
            groups = mode == ViewMode.Suggestions ? new() { (SuggestionsLabel, suggestions) } : new();
        }
        else
        {
            var results = ExpandingSearchRanking.Rank(source, query, Math.Max(0, MaxResults));
            mode = results.Count > 0 ? ViewMode.Results : ViewMode.Empty;
            groups = mode == ViewMode.Results ? ExpandingSearchRanking.Group(results, source) : new();
        }
        var flat = groups.SelectMany(g => g.Items).ToList();
        var panelOpen = _open && _focused && mode != ViewMode.None;
        var modeChanged = mode != _mode;
        _mode = mode;
        _flat = flat;
        if (!panelOpen)
        {
            ClosePanel();
            Announce(string.Empty);
            return;
        }
        EnsurePanel();
        var key = string.Join('|', flat.Select(i => i.Key)) + "#" + mode + "#" + query;
        if (key != _layoutKey || modeChanged)
        {
            _layoutKey = key;
            if (mode == ViewMode.Empty)
            {
                _emptyTitle!.Text = $"No results for “{query}”";
                _emptyHint!.Text = EmptyHint;
                _panelView!.Content = _emptyView;
                _rows.Clear();
            }
            else
            {
                BuildRows(groups, mode == ViewMode.Results ? query : string.Empty);
                _panelView!.Content = _listHost;
            }
        }
        OpenPanel();
        UpdateActive();
        Announce(mode switch
        {
            ViewMode.Empty => $"No results for {query}",
            ViewMode.Results => $"{flat.Count} {(flat.Count == 1 ? "result" : "results")}",
            _ => $"{flat.Count} {SuggestionsLabel.ToLowerInvariant()} {(flat.Count == 1 ? "item" : "items")}"
        });
    }

    private void Announce(string text)
    {
        if (_status is not null && _status.Text != text)
        {
            _status.Text = text;
        }
    }

    private void BuildRows(List<(string Name, List<ExpandingSearchItem> Items)> groups, string query)
    {
        _listStack!.Children.Clear();
        _rows.Clear();
        var index = 0;
        foreach (var (name, items) in groups)
        {
            var group = new StackPanel();
            if (!string.IsNullOrEmpty(name))
            {
                var heading = new TextBlock { Text = name, Padding = new Thickness(12, 8, 12, 4), LineHeight = 12 * 1.4 };
                ArcTokens.Bind(heading, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
                ArcTokens.Bind(heading, TextBlock.FontSizeProperty, DesignTokens.ArcDesignTokenKind.TypeXS);
                ArcTokens.Bind(heading, TextBlock.FontWeightProperty, DesignTokens.ArcDesignTokenKind.WeightMedium);
                group.Children.Add(heading);
            }
            foreach (var item in items)
            {
                var row = new ExpandingSearchOption(item, query, index++);
                row.PointerMoved += OnOptionPointerMoved;
                row.PointerPressed += OnOptionPressed;
                row.PointerReleased += OnOptionReleased;
                group.Children.Add(row);
                _rows.Add(row);
            }
            _listStack.Children.Add(group);
        }
    }

    private void OnOptionPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is ExpandingSearchOption row && row.Index != ActiveIndex)
        {
            _keyboard = false;
            _active = row.Index;
            UpdateActive();
        }
    }

    // Arc's onMouseDown preventDefault: pressing a result keeps focus (and the panel) in the field.
    private static void OnOptionPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void OnOptionReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is ExpandingSearchOption row && e.InitialPressMouseButton == MouseButton.Left)
        {
            e.Handled = true;
            Choose(row.Item);
        }
    }

    private void UpdateActive()
    {
        var active = ActiveIndex;
        foreach (var row in _rows)
        {
            row.SetActive(row.Index == active);
        }
        if (_input is not null)
        {
            AutomationProperties.SetHelpText(_input, active >= 0 && active < _flat.Count ? _flat[active].Title : string.Empty);
        }
        // Rows need a layout pass before their position is known.
        Dispatcher.UIThread.Post(MoveHighlight, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Moving between rows of the same result set glides on the snappy spring (keeping velocity when retargeted);
    /// a new result set or a first highlight fades in on its row without travel.
    /// </summary>
    private void MoveHighlight()
    {
        if (_highlight is null || _listHost is null || _popup?.IsOpen != true)
        {
            return;
        }
        var active = ActiveIndex;
        var row = active >= 0 ? _rows.FirstOrDefault(r => r.Index == active) : null;
        if (row is null || !row.IsArrangeValid || row.TranslatePoint(default, _listHost) is not { } origin)
        {
            _highlight.Hide(animate: false);
            _lastHighlight = null;
            return;
        }
        var rect = new Rect(origin, row.Bounds.Size);
        var previous = _lastHighlight;
        var id = row.Item.Key;
        _lastHighlight = (id, _layoutKey);
        if (previous is { } last && last.Id == id && last.Set == _layoutKey)
        {
            _highlight.MoveTo(rect, animate: false);
            return;
        }
        if (previous is { } prior && prior.Set == _layoutKey && _highlight.HasTarget)
        {
            _highlight.MoveTo(rect, animate: true, ArcSpring.Snappy);
        }
        else
        {
            _highlight.Hide(animate: false);
            _highlight.MoveTo(rect, animate: true);
        }
        if (_keyboard)
        {
            row.BringIntoView();
        }
    }

    private void EnsurePanel()
    {
        if (_popup is not null)
        {
            return;
        }
        _highlight = new GlidingIndicator { Spring = ArcSpring.Snappy };
        _highlight.BindCornerRadius(ArcDesignTokenKind.RadiusStep20);
        ArcTokens.Bind(_highlight, GlidingIndicator.FillProperty, DesignTokens.ArcDesignTokenKind.Foreground);
        _highlight.Opacity = 0.06;
        _listStack = new StackPanel { Spacing = 4 };
        _listHost = new Panel { Children = { _highlight, _listStack } };
        _emptyTitle = new TextBlock { TextWrapping = TextWrapping.Wrap, LineHeight = 14 * 1.4 };
        ArcTokens.Bind(_emptyTitle, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_emptyTitle, TextBlock.FontSizeProperty, DesignTokens.ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_emptyTitle, TextBlock.FontWeightProperty, DesignTokens.ArcDesignTokenKind.WeightMedium);
        _emptyHint = new TextBlock { TextWrapping = TextWrapping.Wrap, LineHeight = 12 * 1.4 };
        ArcTokens.Bind(_emptyHint, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_emptyHint, TextBlock.FontSizeProperty, DesignTokens.ArcDesignTokenKind.TypeXS);
        _emptyView = new StackPanel { Spacing = 2, Margin = new Thickness(12, 12, 12, 14), Children = { _emptyTitle, _emptyHint } };
        _panelView = new ArcContentSwap { Mode = ContentSwapMode.Fade };
        // The content keeps the final width and rides the leading edge while the surface unfolds with the field.
        _panelBody = new Border
        {
            Padding = new Thickness(6),
            Width = Math.Max(0, _full - 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = _panelView
        };
        _panelScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = _panelBody
        };
        _panelHeight = new SpringSizeDecorator { Axis = SpringSizeAxis.Height, Spring = ArcSpring.Smooth, Child = _panelScroll };
        // The shadow lives on an unclipped outer surface; the inner one clips the content to the rounded panel.
        var clip = new Border
        {
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = _panelHeight
        };
        ArcTokens.Bind(clip, Border.BorderBrushProperty, DesignTokens.ArcDesignTokenKind.Border);
        ArcTokens.Bind(clip, Border.CornerRadiusProperty, DesignTokens.ArcDesignTokenKind.RadiusPanel);
        _panelSurface = new Border
        {
            HorizontalAlignment = SurfaceAlignment,
            VerticalAlignment = VerticalAlignment.Top,
            Child = clip
        };
        ArcTokens.Bind(_panelSurface, Border.BoxShadowProperty, DesignTokens.ArcDesignTokenKind.ShadowFloating);
        ArcTokens.Bind(_panelSurface, Border.BackgroundProperty, DesignTokens.ArcDesignTokenKind.SurfaceRaised);
        ArcTokens.Bind(_panelSurface, Border.CornerRadiusProperty, DesignTokens.ArcDesignTokenKind.RadiusPanel);
        ArcTokens.Bind(_panelSurface, TextBlock.FontFamilyProperty, DesignTokens.ArcDesignTokenKind.BodyFontFamily);
        // The popup spans the full field width under a fixed anchor (the lane edge the field grows from), so it never
        // moves while the visible surface inside it unfolds with the field; the surface draws its own floating shadow.
        _panelLane = new Panel { Width = _full, Children = { _panelSurface } };
        var shadowRoom = new Border { Padding = PanelShadowRoom, Background = null, Child = _panelLane };
        _popup = new ArcPopup
        {
            Child = shadowRoom,
            PlacementTarget = _panelAnchor ?? _shellHost,
            Placement = PlacementMode.Bottom,
            IsLightDismissEnabled = false,
            EnterOffset = 0,
            EnterScale = 1,
            MarginToAnchor = 8,
            TakesFocusFromNativeControl = false,
            WindowManagerAddShadowHint = false
        };
        _popup.SetValue(ArcPopup.PopupRootShadowProperty, new BoxShadows());
        _popup.SetValue(ArcPopup.OverlayHostShadowProperty, new BoxShadows());
        ((ISetLogicalParent)_popup).SetParent(this);
        SyncShape(_width?.Value ?? _full);
    }

    private void OpenPanel()
    {
        if (_popup is null || _popup.IsOpen)
        {
            return;
        }
        _lastHighlight = null;
        _highlight?.Hide(animate: false);
        if (TopLevel.GetTopLevel(this) is { } top && _panelScroll is not null)
        {
            _panelScroll.MaxHeight = Math.Min(352, top.ClientSize.Height * 0.6);
        }
        // The panel unfolds from the field: its height opens from zero (full motion); reduced motion only fades.
        var unfold = IsLoaded && ArcMotion.IsFull(this);
        _panelHeight!.IsExpanded = !unfold;
        _popup.IsOpen = true;
        if (unfold)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_panelHeight is not null)
                {
                    _panelHeight.IsExpanded = true;
                }
            }, DispatcherPriority.Loaded);
        }
    }

    private void ClosePanel()
    {
        if (_popup is { IsOpen: true } popup)
        {
            popup.IsOpen = false;
        }
        _lastHighlight = null;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ResourcesChanged += OnResourcesChanged;
        RefreshRadii();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ResourcesChanged -= OnResourcesChanged;
        ClosePanel();
    }
}
