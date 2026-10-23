using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The tab strip of the model arena's narrow layout (spec 7, Arc's <c>role="tablist"</c>): a 60px SurfaceMuted box (1px
/// Border, radius 18, 3 inside) with the two answers as 52px tabs side by side, each with two lines: the slot's identity
/// (its short name once revealed) and its compact status. A Surface highlight (1px Border, radius 15) slides under the
/// selected tab on a 220ms tween with the enter curve (no bounce); the selected tab's name is Foreground, the other's
/// TextSecondary (160ms). Reduced motion jumps the highlight and changes the color in 120ms; Off jumps.
/// </summary>
/// <remarks>
/// Keyboard: one Tab stop (the selected tab); Left / Right move to the other tab and select it (wrapping: selection follows
/// focus), Home and End go to A and B. The tabs only draw the strip; the owner shows the selected pane. Privacy: a tab
/// shows and names its model only while <see cref="IsRevealed"/> (hiding at once, showing at the end of the dispatcher
/// turn, as the pane).
/// </remarks>
[TemplatePart(TabAPart, typeof(ArenaPaneTab))]
[TemplatePart(TabBPart, typeof(ArenaPaneTab))]
[TemplatePart(HighlightPart, typeof(GlidingIndicator))]
internal sealed class ArenaPaneTabs : TemplatedControl
{
    public const string TabAPart = "PART_TabA";
    public const string TabBPart = "PART_TabB";
    public const string HighlightPart = "PART_Highlight";

    /// <summary>The highlight's slide (Arc: about 220ms, a strong ease-out, no bounce).</summary>
    internal static readonly TimeSpan GlideDuration = TimeSpan.FromMilliseconds(220);

    #region Properties

    /// <summary>The first answer's state (identity and status of the A tab).</summary>
    public static readonly StyledProperty<ArenaPaneState?> PaneAProperty =
        AvaloniaProperty.Register<ArenaPaneTabs, ArenaPaneState?>(nameof(PaneA));

    /// <summary>The second answer's state (identity and status of the B tab).</summary>
    public static readonly StyledProperty<ArenaPaneState?> PaneBProperty =
        AvaloniaProperty.Register<ArenaPaneTabs, ArenaPaneState?>(nameof(PaneB));

    /// <summary>The words (names and status words); none shows no text.</summary>
    public static readonly StyledProperty<ModelArenaLabels?> LabelsProperty =
        AvaloniaProperty.Register<ArenaPaneTabs, ModelArenaLabels?>(nameof(Labels));

    /// <summary>Whether the models are shown (blind off, or voted): the tabs show their short names.</summary>
    public static readonly StyledProperty<bool> IsRevealedProperty =
        AvaloniaProperty.Register<ArenaPaneTabs, bool>(nameof(IsRevealed));

    /// <summary>The selected tab (two-way): the answer the owner shows.</summary>
    public static readonly StyledProperty<ArenaSlot> SelectedSlotProperty =
        AvaloniaProperty.Register<ArenaPaneTabs, ArenaSlot>(nameof(SelectedSlot), ArenaSlot.A, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The accessible name of the tab list ("Answers").</summary>
    public static readonly StyledProperty<string?> TabsNameProperty =
        AvaloniaProperty.Register<ArenaPaneTabs, string?>(nameof(TabsName));

    /// <summary>Raised (bubbling) after <see cref="SelectedSlot"/> changed, whoever changed it.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SelectedSlotChangedEvent =
        RoutedEvent.Register<ArenaPaneTabs, RoutedEventArgs>(nameof(SelectedSlotChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="PaneAProperty"/>
    public ArenaPaneState? PaneA { get => GetValue(PaneAProperty); set => SetValue(PaneAProperty, value); }

    /// <inheritdoc cref="PaneBProperty"/>
    public ArenaPaneState? PaneB { get => GetValue(PaneBProperty); set => SetValue(PaneBProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ModelArenaLabels? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsRevealedProperty"/>
    public bool IsRevealed { get => GetValue(IsRevealedProperty); set => SetValue(IsRevealedProperty, value); }

    /// <inheritdoc cref="SelectedSlotProperty"/>
    public ArenaSlot SelectedSlot { get => GetValue(SelectedSlotProperty); set => SetValue(SelectedSlotProperty, value); }

    /// <inheritdoc cref="TabsNameProperty"/>
    public string? TabsName { get => GetValue(TabsNameProperty); set => SetValue(TabsNameProperty, value); }

    /// <inheritdoc cref="SelectedSlotChangedEvent"/>
    public event EventHandler<RoutedEventArgs>? SelectedSlotChanged
    {
        add => AddHandler(SelectedSlotChangedEvent, value);
        remove => RemoveHandler(SelectedSlotChangedEvent, value);
    }

    #endregion

    private ArenaPaneTab? _tabA;
    private ArenaPaneTab? _tabB;
    private GlidingIndicator? _highlight;
    private Rect? _placed;
    private bool _glide;

    static ArenaPaneTabs()
    {
        FocusableProperty.OverrideDefaultValue<ArenaPaneTabs>(false);
        IsTabStopProperty.OverrideDefaultValue<ArenaPaneTabs>(false);
    }

    /// <summary>The A tab (template part).</summary>
    internal ArenaPaneTab? TabA => _tabA;

    /// <summary>The B tab (template part).</summary>
    internal ArenaPaneTab? TabB => _tabB;

    /// <summary>The sliding highlight (template part).</summary>
    internal GlidingIndicator? Highlight => _highlight;

    /// <summary>The tab of <paramref name="slot"/>.</summary>
    internal ArenaPaneTab? TabOf(ArenaSlot slot) => slot == ArenaSlot.A ? _tabA : _tabB;

    /// <summary>Focuses the selected tab by <paramref name="method"/>; false when it cannot take focus.</summary>
    public bool FocusSelected(NavigationMethod method = NavigationMethod.Tab) =>
        TabOf(SelectedSlot) is { } tab && tab.IsEffectivelyVisible && tab.IsEffectivelyEnabled && tab.Focus(method);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_tabA is not null)
        {
            _tabA.Click -= OnTabClick;
        }
        if (_tabB is not null)
        {
            _tabB.Click -= OnTabClick;
        }
        _tabA = e.NameScope.Find<ArenaPaneTab>(TabAPart);
        _tabB = e.NameScope.Find<ArenaPaneTab>(TabBPart);
        _highlight = e.NameScope.Find<GlidingIndicator>(HighlightPart);
        if (_tabA is not null)
        {
            _tabA.Slot = ArenaSlot.A;
            _tabA.Click += OnTabClick;
        }
        if (_tabB is not null)
        {
            _tabB.Slot = ArenaSlot.B;
            _tabB.Click += OnTabClick;
        }
        _placed = null;
        _glide = false;
        UpdateTabs();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedSlotProperty)
        {
            _glide = true;
            UpdateSelection();
            InvalidateArrange();
            if (ControlAutomationPeer.FromElement(this) is TabsPeer peer)
            {
                peer.SelectionChanged();
            }
            RaiseEvent(new RoutedEventArgs(SelectedSlotChangedEvent, this));
        }
        else if (change.Property == PaneAProperty || change.Property == PaneBProperty || change.Property == LabelsProperty
                 || change.Property == IsRevealedProperty)
        {
            UpdateTabs();
        }
        else if (change.Property == TabsNameProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
        }
    }

    private void UpdateTabs()
    {
        Update(_tabA, PaneA);
        Update(_tabB, PaneB);
        UpdateSelection();
    }

    private void Update(ArenaPaneTab? tab, ArenaPaneState? state)
    {
        if (tab is null)
        {
            return;
        }
        tab.Labels = Labels;
        tab.IsRevealed = IsRevealed;
        tab.State = state;
    }

    private void UpdateSelection()
    {
        Mark(_tabA);
        Mark(_tabB);

        void Mark(ArenaPaneTab? tab)
        {
            if (tab is null)
            {
                return;
            }
            var selected = tab.Slot == SelectedSlot;
            tab.IsSelected = selected;
            // Roving tab stop: only the selected tab is reached with Tab.
            KeyboardNavigation.SetIsTabStop(tab, selected);
        }
    }

    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ArenaPaneTab tab)
        {
            Select(tab.Slot);
        }
    }

    /// <summary>The person chose <paramref name="slot"/>.</summary>
    internal void Select(ArenaSlot slot)
    {
        if (slot != SelectedSlot)
        {
            SetCurrentValue(SelectedSlotProperty, slot);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || e.Source is not Visual source)
        {
            return;
        }
        var from = _tabA is { } a && (ReferenceEquals(a, source) || a.IsVisualAncestorOf(source)) ? ArenaSlot.A
            : _tabB is { } b && (ReferenceEquals(b, source) || b.IsVisualAncestorOf(source)) ? ArenaSlot.B
            : (ArenaSlot?)null;
        if (from is not { } current)
        {
            return;
        }
        ArenaSlot? to = e.Key switch
        {
            // Two tabs: the next one either way, wrapping.
            Key.Left or Key.Right => current == ArenaSlot.A ? ArenaSlot.B : ArenaSlot.A,
            Key.Home => ArenaSlot.A,
            Key.End => ArenaSlot.B,
            _ => null
        };
        if (to is not { } target)
        {
            return;
        }
        e.Handled = true;
        // Automatic activation: the tab that takes focus is the selected one.
        Select(target);
        TabOf(target)?.Focus(NavigationMethod.Directional);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        // The tabs were just arranged: the highlight follows the selected one, sliding when the selection changed and
        // jumping when the strip was resized or laid out for the first time.
        if (TabOf(SelectedSlot) is { } tab && _highlight is not null && tab.Bounds.Width > 0)
        {
            var origin = tab.TranslatePoint(default, _highlight) ?? tab.Bounds.Position;
            var rect = new Rect(origin, tab.Bounds.Size);
            if (_placed != rect || !_highlight.HasTarget)
            {
                var animate = _glide && _highlight.HasTarget;
                // The outline is drawn on the rectangle's edge: half a stroke in, so it sits inside the tab like a CSS border.
                var drawn = rect.Deflate(_highlight.StrokeThickness / 2);
                _highlight.MoveTo(drawn, GlideDuration, ArcEasings.Enter, animate);
                _placed = rect;
            }
        }
        _glide = false;
        return size;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TabsPeer(this);

    /// <summary>A tab list (Arc's <c>role="tablist"</c>, "Answers") whose selection is the selected tab.</summary>
    private sealed class TabsPeer(ArenaPaneTabs owner) : ControlAutomationPeer(owner), ISelectionProvider
    {
        private ArenaPaneTabs Tabs => (ArenaPaneTabs)Owner;

        public bool CanSelectMultiple => false;

        public bool IsSelectionRequired => true;

        public IReadOnlyList<AutomationPeer> GetSelection() =>
            Tabs.TabOf(Tabs.SelectedSlot) is { } tab && CreatePeerForElement(tab) is { } peer ? [peer] : [];

        public void SelectionChanged() => RaisePropertyChangedEvent(SelectionPatternIdentifiers.SelectionProperty, null, null);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;

        /// <summary>The two tabs, without the template's frame and highlight in between.</summary>
        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore()
        {
            var children = new List<AutomationPeer>(2);
            foreach (var tab in new[] { Tabs.TabA, Tabs.TabB })
            {
                if (tab is { IsVisible: true })
                {
                    children.Add(CreatePeerForElement(tab));
                }
            }
            return children;
        }

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(ISelectionProvider) ? this : base.GetProviderCore(providerType);
    }
}
