// Ported from Arc's free empty-states block (MIT, Copyright (c) 2026 Elia Kuratli): empty-states.tsx and
// empty-states.module.css, made data driven (items, phase and action events instead of the preview's simulation).

using System.Collections.Specialized;
using System.Windows.Input;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
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
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Four empty states in one illustration whose shapes morph between scenes as you switch tabs. Each
/// <see cref="EmptyStatesItem"/> is a tab with a scene (no results, offline, caught up, not found) and its copy; every
/// shape of the drawing travels to its new position, size, radius, rotation and color on a spring, outlines morph point
/// by point and line accents draw in a beat later. The primary action raises <see cref="ActionInvoked"/> (and runs
/// <see cref="ActionCommand"/>); the app does the work and moves <see cref="Phase"/> to Loading and then Done, and the
/// drawing and copy follow. With no items the block shows Arc's four scenes and copy.
/// </summary>
/// <remarks>Ported from Arc's free empty-states block (MIT, Copyright (c) 2026 Elia Kuratli).</remarks>
[TemplatePart(TabsPart, typeof(Panel))]
[TemplatePart(HighlightPart, typeof(GlidingIndicator))]
[TemplatePart(ArtPart, typeof(Control))]
[TemplatePart(CopyFramePart, typeof(Decorator))]
[TemplatePart(ActionPart, typeof(AvaloniaButton))]
[TemplatePart(AnnouncerPart, typeof(Control))]
[PseudoClasses(NarrowPseudoClass, WidePseudoClass)]
public class EmptyStates : TemplatedControl
{
    public const string TabsPart = "PART_Tabs";
    public const string HighlightPart = "PART_Highlight";
    public const string ArtPart = "PART_Art";
    public const string CopyFramePart = "PART_CopyFrame";
    public const string ActionPart = "PART_Action";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the control is at most 420 wide: tighter, smaller tabs.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the control is at least 560 wide: the larger title.</summary>
    public const string WidePseudoClass = ":wide";

    #region Public properties

    /// <summary>The tabs, in order. Empty (the default) shows Arc's four scenes with Arc's copy.</summary>
    public static readonly DirectProperty<EmptyStates, AvaloniaList<EmptyStatesItem>> ItemsProperty =
        AvaloniaProperty.RegisterDirect<EmptyStates, AvaloniaList<EmptyStatesItem>>(nameof(Items), o => o.Items);

    /// <summary>The index of the selected tab (two way).</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<EmptyStates, int>(nameof(SelectedIndex), 0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The selected item (two way). Choosing another item puts <see cref="Phase"/> back to Idle.</summary>
    public static readonly StyledProperty<EmptyStatesItem?> SelectedItemProperty =
        AvaloniaProperty.Register<EmptyStates, EmptyStatesItem?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Where the selected item's action stands: Idle, Loading or Done (two way). Switching tabs resets it to Idle.</summary>
    public static readonly StyledProperty<EmptyStatesPhase> PhaseProperty =
        AvaloniaProperty.Register<EmptyStates, EmptyStatesPhase>(nameof(Phase), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The accessible name of the region.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<EmptyStates, string>(nameof(Label), "Empty states");

    /// <summary>The accessible name of the tab list.</summary>
    public static readonly StyledProperty<string> TabListLabelProperty =
        AvaloniaProperty.Register<EmptyStates, string>(nameof(TabListLabel), "Empty state");

    /// <summary>Runs when the action is pressed, after <see cref="ActionInvoked"/>; its parameter is the event arguments.</summary>
    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<EmptyStates, ICommand?>(nameof(ActionCommand));

    /// <summary>The action of the selected item was pressed (Idle or Done). Do the work and set <see cref="Phase"/>.</summary>
    public static readonly RoutedEvent<EmptyStatesActionEventArgs> ActionInvokedEvent =
        RoutedEvent.Register<EmptyStates, EmptyStatesActionEventArgs>(nameof(ActionInvoked), RoutingStrategies.Bubble);

    private readonly AvaloniaList<EmptyStatesItem> _items = new();

    /// <inheritdoc cref="ItemsProperty"/>
    [Content]
    public AvaloniaList<EmptyStatesItem> Items => _items;

    /// <inheritdoc cref="SelectedIndexProperty"/>
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    /// <inheritdoc cref="SelectedItemProperty"/>
    public EmptyStatesItem? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }

    /// <inheritdoc cref="PhaseProperty"/>
    public EmptyStatesPhase Phase { get => GetValue(PhaseProperty); set => SetValue(PhaseProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="TabListLabelProperty"/>
    public string TabListLabel { get => GetValue(TabListLabelProperty); set => SetValue(TabListLabelProperty, value); }

    /// <inheritdoc cref="ActionCommandProperty"/>
    public ICommand? ActionCommand { get => GetValue(ActionCommandProperty); set => SetValue(ActionCommandProperty, value); }

    /// <inheritdoc cref="ActionInvokedEvent"/>
    public event EventHandler<EmptyStatesActionEventArgs>? ActionInvoked
    {
        add => AddHandler(ActionInvokedEvent, value);
        remove => RemoveHandler(ActionInvokedEvent, value);
    }

    #endregion

    #region Template properties

    internal static readonly DirectProperty<EmptyStates, Thickness> CardPaddingProperty =
        AvaloniaProperty.RegisterDirect<EmptyStates, Thickness>(nameof(CardPadding), o => o.CardPadding);

    internal static readonly DirectProperty<EmptyStates, double> CardGapProperty =
        AvaloniaProperty.RegisterDirect<EmptyStates, double>(nameof(CardGap), o => o.CardGap);

    internal static readonly DirectProperty<EmptyStates, string?> DisplayTitleProperty =
        AvaloniaProperty.RegisterDirect<EmptyStates, string?>(nameof(DisplayTitle), o => o.DisplayTitle);

    internal static readonly DirectProperty<EmptyStates, string?> DisplayDescriptionProperty =
        AvaloniaProperty.RegisterDirect<EmptyStates, string?>(nameof(DisplayDescription), o => o.DisplayDescription);

    internal static readonly DirectProperty<EmptyStates, string?> DisplayActionLabelProperty =
        AvaloniaProperty.RegisterDirect<EmptyStates, string?>(nameof(DisplayActionLabel), o => o.DisplayActionLabel);

    internal static readonly DirectProperty<EmptyStates, bool> IsActionLoadingProperty =
        AvaloniaProperty.RegisterDirect<EmptyStates, bool>(nameof(IsActionLoading), o => o.IsActionLoading);

    internal static readonly DirectProperty<EmptyStates, bool> HasTabsProperty =
        AvaloniaProperty.RegisterDirect<EmptyStates, bool>(nameof(HasTabs), o => o.HasTabs);

    private Thickness _cardPadding = new(32, 40);
    private double _cardGap = 32;
    private string? _displayTitle;
    private string? _displayDescription;
    private string? _displayActionLabel;
    private bool _isActionLoading;
    private bool _hasTabs = true;

    /// <summary>clamp(16px, 5cqi, 48px) above and below, clamp(16px, 4cqi, 40px) at the sides.</summary>
    internal Thickness CardPadding { get => _cardPadding; private set => SetAndRaise(CardPaddingProperty, ref _cardPadding, value); }

    /// <summary>clamp(20px, 4cqi, 32px) between the tabs and the panel.</summary>
    internal double CardGap { get => _cardGap; private set => SetAndRaise(CardGapProperty, ref _cardGap, value); }

    internal string? DisplayTitle { get => _displayTitle; private set => SetAndRaise(DisplayTitleProperty, ref _displayTitle, value); }

    internal string? DisplayDescription { get => _displayDescription; private set => SetAndRaise(DisplayDescriptionProperty, ref _displayDescription, value); }

    internal string? DisplayActionLabel { get => _displayActionLabel; private set => SetAndRaise(DisplayActionLabelProperty, ref _displayActionLabel, value); }

    internal bool IsActionLoading { get => _isActionLoading; private set => SetAndRaise(IsActionLoadingProperty, ref _isActionLoading, value); }

    /// <summary>The tab row shows only when there is something to switch between (two items or more).</summary>
    internal bool HasTabs { get => _hasTabs; private set => SetAndRaise(HasTabsProperty, ref _hasTabs, value); }

    #endregion

    private readonly EmptyStatesItem[] _defaults =
    [
        new(EmptyStatesScene.NoResults), new(EmptyStatesScene.Offline), new(EmptyStatesScene.CaughtUp), new(EmptyStatesScene.NotFound)
    ];

    private readonly List<EmptyStatesItem> _watched = new();
    private EmptyStatesTabPanel? _tabs;
    private EmptyStatesArt? _art;
    private EmptyStatesCopyFrame? _copyFrame;
    private AvaloniaButton? _action;
    private EmptyStatesAnnouncer? _announcer;
    private EmptyStatesItem? _selected;
    private EmptyStatesPhase _from = EmptyStatesPhase.Idle;
    private bool _ready;
    private bool _syncing;
    private bool _attached;

    static EmptyStates()
    {
        FocusableProperty.OverrideDefaultValue<EmptyStates>(false);
    }

    public EmptyStates()
    {
        _items.CollectionChanged += OnItemsChanged;
        AutomationProperties.SetName(this, Label);
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Region);
    }

    /// <summary>The items the tabs show: <see cref="Items"/>, or Arc's four scenes while it is empty.</summary>
    private IReadOnlyList<EmptyStatesItem> EffectiveItems => _items.Count > 0 ? _items : _defaults;

    #region Test hooks

    internal EmptyStatesArt? Art => _art;

    internal EmptyStatesTabPanel? TabPanel => _tabs;

    internal EmptyStatesCopyFrame? CopyFrame => _copyFrame;

    internal AvaloniaButton? ActionButton => _action;

    internal string? Announcement => _announcer?.Announcement;

    #endregion

    #region Selection and phase

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _ready = true;
        ResolveSelection();
        Update();
    }

    /// <summary>Keeps <see cref="SelectedItem"/> and <see cref="SelectedIndex"/> on an item of the current list.</summary>
    private void ResolveSelection()
    {
        var items = EffectiveItems;
        HasTabs = items.Count > 1;
        var index = SelectedItem is { } selected ? IndexOf(items, selected) : -1;
        if (index < 0)
        {
            index = Math.Clamp(SelectedIndex, 0, items.Count - 1);
        }
        Select(index);
    }

    private static int IndexOf(IReadOnlyList<EmptyStatesItem> items, EmptyStatesItem item)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], item))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Selects the item at <paramref name="index"/>; switching to a different item resets the phase to Idle.</summary>
    private void Select(int index)
    {
        var items = EffectiveItems;
        var item = items[index];
        // Compare with the item last selected here: when SelectedItem was set from outside it already holds the new one.
        var previous = _selected;
        var changed = !ReferenceEquals(item, previous);
        _selected = item;
        _syncing = true;
        try
        {
            SetCurrentValue(SelectedIndexProperty, index);
            SetCurrentValue(SelectedItemProperty, item);
            // The first selection keeps a phase set up front (XAML sets Phase before the items arrive).
            if (changed && previous is not null)
            {
                _from = EmptyStatesPhase.Idle;
                SetCurrentValue(PhaseProperty, EmptyStatesPhase.Idle);
            }
        }
        finally
        {
            _syncing = false;
        }
        if (_tabs is not null)
        {
            _tabs.SelectedIndex = index;
        }
        if (changed)
        {
            _announcer?.Announce(null);
            Update();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty)
        {
            if (!_syncing && _ready)
            {
                Select(Math.Clamp(change.GetNewValue<int>(), 0, EffectiveItems.Count - 1));
            }
        }
        else if (change.Property == SelectedItemProperty)
        {
            if (!_syncing && _ready)
            {
                var index = change.GetNewValue<EmptyStatesItem?>() is { } item ? IndexOf(EffectiveItems, item) : -1;
                // An item that is not in the list cannot be selected: the selection stays where it was.
                Select(index >= 0 ? index : Math.Clamp(SelectedIndex, 0, EffectiveItems.Count - 1));
            }
        }
        else if (change.Property == PhaseProperty)
        {
            if (!_syncing)
            {
                OnPhaseChanged(change.GetOldValue<EmptyStatesPhase>(), change.GetNewValue<EmptyStatesPhase>());
            }
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
    }

    private void OnPhaseChanged(EmptyStatesPhase previous, EmptyStatesPhase phase)
    {
        // Loading keeps the title and action label of the phase the action started from.
        if (phase == EmptyStatesPhase.Loading && previous != EmptyStatesPhase.Loading)
        {
            _from = previous;
        }
        if (!_ready)
        {
            return;
        }
        Update();
        if (SelectedItem is { } item)
        {
            var (title, description, _) = item.CopyFor(phase, _from);
            _announcer?.Announce(phase == EmptyStatesPhase.Loading ? description : $"{title}. {description}");
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Watch();
        if (!_ready)
        {
            return;
        }
        UpdateTabHeaders();
        ResolveSelection();
        // The same item may now show different copy (its scene's text moved in from another item).
        Update();
    }

    /// <summary>
    /// Follows property changes of the listed items (copy edits update the view in place), while the block is in a window:
    /// items often outlive the page that shows them, and a subscription kept after the block left would keep it alive.
    /// </summary>
    private void Watch()
    {
        foreach (var item in _watched)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }
        _watched.Clear();
        if (!_attached)
        {
            return;
        }
        foreach (var item in EffectiveItems)
        {
            item.PropertyChanged += OnItemPropertyChanged;
            _watched.Add(item);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Watch();
        if (_ready)
        {
            // Back in a window: what changed in the items while it was away shows now.
            UpdateTabHeaders();
            Update();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Watch();
    }

    private void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }
        if (e.Property == EmptyStatesItem.HeaderProperty || e.Property == EmptyStatesItem.SceneProperty)
        {
            UpdateTabHeaders();
        }
        if (ReferenceEquals(sender, SelectedItem))
        {
            Update();
        }
    }

    /// <summary>Shows the selected item's copy and drawing for the current phase.</summary>
    private void Update()
    {
        if (!_ready || SelectedItem is not { } item)
        {
            return;
        }
        var phase = Phase;
        var (title, description, label) = item.CopyFor(phase, _from);
        if (title != DisplayTitle || description != DisplayDescription)
        {
            _copyFrame?.Arm();
        }
        DisplayTitle = title;
        DisplayDescription = description;
        DisplayActionLabel = label;
        IsActionLoading = phase == EmptyStatesPhase.Loading;
        _art?.Show(item.Scene, phase, item.IllustrationLabelFor(phase));
    }

    #endregion

    #region Template, input

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_tabs is not null)
        {
            _tabs.Invoked = null;
        }
        if (_action is not null)
        {
            _action.Click -= OnActionClick;
        }
        _tabs = e.NameScope.Find<EmptyStatesTabPanel>(TabsPart);
        _art = e.NameScope.Find<EmptyStatesArt>(ArtPart);
        _copyFrame = e.NameScope.Find<EmptyStatesCopyFrame>(CopyFramePart);
        _action = e.NameScope.Find<AvaloniaButton>(ActionPart);
        _announcer = e.NameScope.Find<EmptyStatesAnnouncer>(AnnouncerPart);
        if (_tabs is not null)
        {
            _tabs.Highlight = e.NameScope.Find<GlidingIndicator>(HighlightPart);
            _tabs.Invoked = OnTabInvoked;
            UpdateTabHeaders();
            _tabs.SelectedIndex = _ready ? SelectedIndex : -1;
            ApplyDensity(Bounds.Width);
        }
        if (_action is not null)
        {
            _action.Click += OnActionClick;
        }
        Update();
    }

    private void UpdateTabHeaders()
    {
        if (_tabs is null)
        {
            return;
        }
        var items = EffectiveItems;
        var headers = new string[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            headers[i] = items[i].EffectiveHeader;
        }
        _tabs.SetTabs(headers);
        _tabs.SelectedIndex = _ready ? SelectedIndex : -1;
        ApplyDensity(Bounds.Width);
    }

    private void OnTabInvoked(int index)
    {
        if (index >= 0 && index < EffectiveItems.Count)
        {
            Select(index);
        }
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        // The button swallows presses while loading; this guards programmatic clicks too.
        if (Phase == EmptyStatesPhase.Loading || SelectedItem is not { } item)
        {
            return;
        }
        var args = new EmptyStatesActionEventArgs(ActionInvokedEvent, item, Phase);
        RaiseEvent(args);
        if (ActionCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _tabs is null || e.Source is not EmptyStatesTab tab)
        {
            return;
        }
        var tabs = _tabs.Tabs;
        var current = -1;
        for (var i = 0; i < tabs.Count; i++)
        {
            if (ReferenceEquals(tabs[i], tab))
            {
                current = i;
            }
        }
        if (current < 0)
        {
            return;
        }
        var count = tabs.Count;
        var next = e.Key switch
        {
            Key.Right => (current + 1) % count,
            Key.Left => (current + count - 1) % count,
            Key.Home => 0,
            Key.End => count - 1,
            _ => -1
        };
        if (next < 0)
        {
            return;
        }
        e.Handled = true;
        Select(next);
        tabs[next].Focus(NavigationMethod.Directional);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        // The copy block springs the block's height for half a second after a switch: only the width sets the density.
        if (e.WidthChanged)
        {
            ApplyDensity(e.NewSize.Width);
        }
    }

    /// <summary>Arc's container queries on the block's own width (cqi).</summary>
    private void ApplyDensity(double width)
    {
        if (width <= 0)
        {
            return;
        }
        CardPadding = new Thickness(Math.Clamp(width * .04, 16, 40), Math.Clamp(width * .05, 16, 48));
        CardGap = Math.Clamp(width * .04, 20, 32);
        var narrow = width <= 420;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(WidePseudoClass, width >= 560);
        _tabs?.SetDensity(narrow, new Thickness(narrow ? 6 : Math.Clamp(width * .026, 8, 14), 0));
    }

    #endregion
}
