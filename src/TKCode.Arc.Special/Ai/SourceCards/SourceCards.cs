using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A row of cited sources under an AI answer (Arc Source cards, design reference). Each card shows the site's mark, its
/// domain, the citation number and a two-line title. While searching, cards stream into reserved slots under "Searching
/// 3 of 7"; past <see cref="Max"/> the rest collapse into a stack of overlapping marks ("+4 more"). Hovering or focusing
/// a card lifts a preview with the passage, and a <see cref="SourceCitation"/> in the answer highlights its card.
/// <para>
/// Give it the sources in citation order in <see cref="Sources"/> (append as results arrive), set <see cref="Status"/>
/// and, while searching, <see cref="Total"/>. Put the answer in <see cref="ContentControl.Content"/>: it sits between the
/// status line and the cards, and its height springs as it streams so the cards glide down. With
/// <see cref="Sources"/> null the cards show the state you set (by default "No sources found"), or play a
/// <see cref="Simulation"/> you pass in.
/// </para>
/// </summary>
[TemplatePart(StatusRowPart, typeof(Grid))]
[TemplatePart(StatusLeftPart, typeof(Grid))]
[TemplatePart(StatusIconPart, typeof(ArcContentSwap))]
[TemplatePart(StatusWordPart, typeof(ArcContentSwap))]
[TemplatePart(CountPart, typeof(SourceCardsCount))]
[TemplatePart(MetaPart, typeof(ArcContentSwap))]
[TemplatePart(RetryPart, typeof(SourceCardsActionButton))]
[TemplatePart(TogglePart, typeof(SourceCardsActionButton))]
[TemplatePart(AnswerBoxPart, typeof(SpringSizeDecorator))]
[TemplatePart(AnswerPart, typeof(Panel))]
[TemplatePart(SampleAnswerPart, typeof(ContentPresenter))]
[TemplatePart(GridBoxPart, typeof(SpringSizeDecorator))]
[TemplatePart(GridPart, typeof(SourceCardsGrid))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[TemplatePart(PopupPart, typeof(ArcPopup))]
[TemplatePart(PreviewPart, typeof(SourceCardsPreview))]
[PseudoClasses(SearchingPseudoClass, ErrorPseudoClass, EmptyPseudoClass, ExpandedPseudoClass)]
public partial class SourceCards : ContentControl
{
    internal const string StatusRowPart = "PART_StatusRow";
    internal const string StatusLeftPart = "PART_StatusLeft";
    internal const string StatusIconPart = "PART_StatusIcon";
    internal const string StatusWordPart = "PART_StatusWord";
    internal const string CountPart = "PART_Count";
    internal const string MetaPart = "PART_Meta";
    internal const string RetryPart = "PART_Retry";
    internal const string TogglePart = "PART_Toggle";
    internal const string AnswerBoxPart = "PART_AnswerBox";
    internal const string AnswerPart = "PART_Answer";
    internal const string SampleAnswerPart = "PART_SampleAnswer";
    internal const string GridBoxPart = "PART_GridBox";
    internal const string GridPart = "PART_Grid";
    internal const string AnnouncerPart = "PART_Announcer";
    internal const string PopupPart = "PART_PreviewPopup";
    internal const string PreviewPart = "PART_Preview";

    internal const string SearchingPseudoClass = ":searching";
    internal const string ErrorPseudoClass = ":error";
    internal const string EmptyPseudoClass = ":empty";
    internal const string ExpandedPseudoClass = ":expanded";

    /// <summary>How long a citation's highlight lasts when nobody sets <see cref="Highlighted"/> back.</summary>
    internal static readonly TimeSpan HighlightFor = TimeSpan.FromMilliseconds(2600);

    /// <summary>The sources in citation order (source 1 is cited as [1]). Append as results arrive; each new card fades into
    /// its slot. An observable list is followed as it changes. While null, a <see cref="Simulation"/> plays.</summary>
    public static readonly StyledProperty<IEnumerable<SourceCardsSource>?> SourcesProperty =
        AvaloniaProperty.Register<SourceCards, IEnumerable<SourceCardsSource>?>(nameof(Sources));

    /// <summary>Searching shows a spinner, a shimmering "Searching", a rolling count and reserved slots; Done shows "7
    /// sources" and the time ("No sources found" with none); Error keeps what arrived and shows "Search failed".</summary>
    public static readonly StyledProperty<SourceCardsStatus> StatusProperty =
        AvaloniaProperty.Register<SourceCards, SourceCardsStatus>(nameof(Status), SourceCardsStatus.Done);

    /// <summary>How many sources the search will return. Drives "Searching 3 of 7" and reserves that many slots, so the row
    /// never shifts while it fills.</summary>
    public static readonly StyledProperty<int?> TotalProperty =
        AvaloniaProperty.Register<SourceCards, int?>(nameof(Total));

    /// <summary>The search query, shown muted after the status while searching or when nothing was found.</summary>
    public static readonly StyledProperty<string?> QueryProperty =
        AvaloniaProperty.Register<SourceCards, string?>(nameof(Query));

    /// <summary>The search time, shown after the count when done ("7 sources · 2.1s").</summary>
    public static readonly StyledProperty<TimeSpan?> DurationProperty =
        AvaloniaProperty.Register<SourceCards, TimeSpan?>(nameof(Duration));

    /// <summary>Cards shown before the rest collapse into the stack. The grid has Max + 1 columns on wide containers and two
    /// below 540px.</summary>
    public static readonly StyledProperty<int> MaxProperty =
        AvaloniaProperty.Register<SourceCards, int>(nameof(Max), 3, coerce: (_, value) => Math.Max(1, value));

    /// <summary>Whether the stack is open and every card shows (two-way). The stack, the Show all toggle and a citation
    /// that points into the stack change it and raise <see cref="ExpandedChanged"/>.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<SourceCards, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The highlighted source number (1-based), or null (two-way). A citation press sets it and clears it again
    /// after 2.6s unless it has changed meanwhile; set it yourself to hold a highlight.</summary>
    public static readonly StyledProperty<int?> HighlightedProperty =
        AvaloniaProperty.Register<SourceCards, int?>(nameof(Highlighted), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether pressing a card opens its address in the browser when no <see cref="SourceInvoked"/> handler marks
    /// the event handled. Cards say so in their name.</summary>
    public static readonly StyledProperty<bool> OpensLinksProperty =
        AvaloniaProperty.Register<SourceCards, bool>(nameof(OpensLinks), true);

    /// <summary>Shows Retry after a failed search and Search again when nothing was found; both raise
    /// <see cref="RetryRequested"/>.</summary>
    public static readonly StyledProperty<bool> AllowsRetryProperty =
        AvaloniaProperty.Register<SourceCards, bool>(nameof(AllowsRetry));

    /// <summary>The detail shown after "Search failed", such as "The search provider timed out after 8s."</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<SourceCards, string?>(nameof(ErrorText));

    /// <summary>Whether the status line shows; turn it off when your UI already shows the search progress (the Show all
    /// toggle and Retry stay).</summary>
    public static readonly StyledProperty<bool> IsStatusVisibleProperty =
        AvaloniaProperty.Register<SourceCards, bool>(nameof(IsStatusVisible), true);

    /// <summary>A simulated search to play while <see cref="Sources"/> is null (demos and prototypes): its sources stream
    /// in and its answer streams with citations. Simulated cards open no address; null shows the cards' own state.</summary>
    public static readonly StyledProperty<SourceCardsSimulation?> SimulationProperty =
        AvaloniaProperty.Register<SourceCards, SourceCardsSimulation?>(nameof(Simulation));

    /// <summary>Every word the cards and their citations show or say.</summary>
    public static readonly StyledProperty<SourceCardsLabels> LabelsProperty =
        AvaloniaProperty.Register<SourceCards, SourceCardsLabels>(nameof(Labels), SourceCardsLabels.Default);

    /// <summary>Raised when a card is pressed, before its address opens. Bubbles. Mark it handled to open the source in your
    /// own reader instead.</summary>
    public static readonly RoutedEvent<SourceCardsSourceEventArgs> SourceInvokedEvent =
        RoutedEvent.Register<SourceCards, SourceCardsSourceEventArgs>(nameof(SourceInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when the stack, the Show all toggle or a citation opens or closes the stack. Bubbles.</summary>
    public static readonly RoutedEvent<SourceCardsExpandedEventArgs> ExpandedChangedEvent =
        RoutedEvent.Register<SourceCards, SourceCardsExpandedEventArgs>(nameof(ExpandedChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a citation highlights its card, and when that highlight ends. Bubbles.</summary>
    public static readonly RoutedEvent<SourceCardsHighlightEventArgs> HighlightChangedEvent =
        RoutedEvent.Register<SourceCards, SourceCardsHighlightEventArgs>(nameof(HighlightChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses Retry or Search again. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RetryRequestedEvent =
        RoutedEvent.Register<SourceCards, RoutedEventArgs>(nameof(RetryRequested), RoutingStrategies.Bubble);

    static SourceCards()
    {
        SpecialFocusModality.Watch();
        FocusableProperty.OverrideDefaultValue<SourceCards>(false);
    }

    /// <summary>Creates the cards.</summary>
    public SourceCards()
    {
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnAnyKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <inheritdoc cref="SourcesProperty"/>
    public IEnumerable<SourceCardsSource>? Sources { get => GetValue(SourcesProperty); set => SetValue(SourcesProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public SourceCardsStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="TotalProperty"/>
    public int? Total { get => GetValue(TotalProperty); set => SetValue(TotalProperty, value); }

    /// <inheritdoc cref="QueryProperty"/>
    public string? Query { get => GetValue(QueryProperty); set => SetValue(QueryProperty, value); }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan? Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <inheritdoc cref="MaxProperty"/>
    public int Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="HighlightedProperty"/>
    public int? Highlighted { get => GetValue(HighlightedProperty); set => SetValue(HighlightedProperty, value); }

    /// <inheritdoc cref="OpensLinksProperty"/>
    public bool OpensLinks { get => GetValue(OpensLinksProperty); set => SetValue(OpensLinksProperty, value); }

    /// <inheritdoc cref="AllowsRetryProperty"/>
    public bool AllowsRetry { get => GetValue(AllowsRetryProperty); set => SetValue(AllowsRetryProperty, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    /// <inheritdoc cref="IsStatusVisibleProperty"/>
    public bool IsStatusVisible { get => GetValue(IsStatusVisibleProperty); set => SetValue(IsStatusVisibleProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public SourceCardsSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public SourceCardsLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="SourceInvokedEvent"/>
    public event EventHandler<SourceCardsSourceEventArgs>? SourceInvoked
    {
        add => AddHandler(SourceInvokedEvent, value);
        remove => RemoveHandler(SourceInvokedEvent, value);
    }

    /// <inheritdoc cref="ExpandedChangedEvent"/>
    public event EventHandler<SourceCardsExpandedEventArgs>? ExpandedChanged
    {
        add => AddHandler(ExpandedChangedEvent, value);
        remove => RemoveHandler(ExpandedChangedEvent, value);
    }

    /// <inheritdoc cref="HighlightChangedEvent"/>
    public event EventHandler<SourceCardsHighlightEventArgs>? HighlightChanged
    {
        add => AddHandler(HighlightChangedEvent, value);
        remove => RemoveHandler(HighlightChangedEvent, value);
    }

    /// <inheritdoc cref="RetryRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RetryRequested
    {
        add => AddHandler(RetryRequestedEvent, value);
        remove => RemoveHandler(RetryRequestedEvent, value);
    }

    /// <summary>Highlights source <paramref name="number"/> as a citation press would: opens the stack when the card is in it
    /// and clears the highlight after 2.6s.</summary>
    public void HighlightSource(int number) => Highlight(number, fromCitation: false);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourcesProperty)
        {
            Observe(change.GetNewValue<IEnumerable<SourceCardsSource>?>());
            UpdateSimulation();
            Refresh();
        }
        else if (change.Property == StatusProperty || change.Property == TotalProperty || change.Property == QueryProperty ||
                 change.Property == DurationProperty || change.Property == ErrorTextProperty || change.Property == MaxProperty ||
                 change.Property == AllowsRetryProperty || change.Property == OpensLinksProperty ||
                 change.Property == IsStatusVisibleProperty || change.Property == LabelsProperty)
        {
            Refresh();
        }
        else if (change.Property == IsExpandedProperty)
        {
            Refresh();
        }
        else if (change.Property == HighlightedProperty)
        {
            SyncHighlight();
        }
        else if (change.Property == SimulationProperty)
        {
            UpdateSimulation();
            Refresh();
        }
        else if (change.Property == ContentProperty)
        {
            RefreshAnswer();
        }
    }

    private INotifyCollectionChanged? _observed;

    private void Observe(IEnumerable<SourceCardsSource>? sources)
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnSourcesChanged;
            _observed = null;
        }
        if (sources is INotifyCollectionChanged observable && IsAttached)
        {
            _observed = observable;
            _observed.CollectionChanged += OnSourcesChanged;
        }
    }

    private bool IsAttached => TopLevel.GetTopLevel(this) is not null;

    private void OnSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(Sources);
        UpdateSimulation();
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnSourcesChanged;
            _observed = null;
        }
        StopHighlightTimer();
        HidePreview(immediately: true);
        StopPreviewTimers();
        UpdateSimulation();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsPreviewOpen)
        {
            HidePreview(immediately: false);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    // Pressing or typing inside the cards hands them to the person: a simulation stops touring and looping.
    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e) => MarkInteracted();

    private void OnAnyKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Tab or Key.LeftShift or Key.RightShift))
        {
            MarkInteracted();
        }
    }

    private void MarkInteracted()
    {
        if (ActiveSimulation is { } simulation)
        {
            simulation.Interacted = true;
        }
    }

    /// <summary>The sources on screen now (the simulation's while it plays), in citation order.</summary>
    internal IReadOnlyList<SourceCardsSource> DisplayedSources =>
        ActiveSimulation is { } simulation ? simulation.Arrived : Sources switch
        {
            null => [],
            IReadOnlyList<SourceCardsSource> list => list,
            _ => Sources.ToList()
        };

    internal SourceCardsStatus EffectiveStatus => ActiveSimulation?.Status ?? Status;

    internal int? EffectiveTotal => ActiveSimulation is { } simulation ? simulation.Total : Total;

    internal string? EffectiveQuery => ActiveSimulation is { } simulation ? simulation.ShownQuery : Query;

    internal TimeSpan? EffectiveDuration => ActiveSimulation is { } simulation ? simulation.ShownDuration : Duration;

    internal string? EffectiveError => ActiveSimulation is { } simulation ? simulation.ShownError : ErrorText;

    internal bool EffectiveAllowsRetry => IsSimulating || AllowsRetry;

    internal bool EffectiveOpensLinks => !IsSimulating && OpensLinks;

    /// <summary>Whether the status is Done with nothing found.</summary>
    internal bool IsEmptyResult => EffectiveStatus == SourceCardsStatus.Done && DisplayedSources.Count == 0;
}
