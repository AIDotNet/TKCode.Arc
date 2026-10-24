using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What the cache of a <see cref="PromptCache"/> is doing.</summary>
public enum PromptCacheState
{
    /// <summary>Nothing has been written yet.</summary>
    Empty,

    /// <summary>A request is writing (part of) the prefix.</summary>
    Writing,

    /// <summary>A request is reading the cached prefix.</summary>
    Reading,

    /// <summary>The prefix is cached and its time to live counts down.</summary>
    Warm,

    /// <summary>The time to live ran out; the next request writes again.</summary>
    Expired
}

/// <summary>
/// An animated explainer of prompt caching. Up to four requests share a prompt prefix (tool schemas, instructions,
/// documents, the chat so far) and add a new message each. The first processes every segment in one steady sweep and
/// carries the prefix into the cache panel, where a time-to-live ring fills; later requests read the prefix back under one
/// passing band and only process their message, and their processed tokens, input cost and first-token time roll down
/// from the row above. A toggle edits one segment: the next request matches the cache only up to it, shows a break there
/// and writes the rest again. When no request arrives the ring drains on an accelerated clock and the cache expires;
/// then the round starts over. The totals roll into the input cost saved and the first-token speedup. Costs and times are
/// computed from <see cref="Pricing"/> and <see cref="FirstTokenEstimator"/>; the numbers are illustrative.
/// </summary>
[TemplatePart(TablePart, typeof(PromptCacheTable))]
[TemplatePart(CacheBarPart, typeof(PromptCacheBar))]
[TemplatePart(TogglePart, typeof(ToggleButton))]
[PseudoClasses(NarrowPseudoClass, CompactPseudoClass, TinyPseudoClass, EmptyPseudoClass, WritingPseudoClass, ReadingPseudoClass,
    WarmPseudoClass, ExpiredPseudoClass, LowPseudoClass, ArmedPseudoClass)]
public partial class PromptCache : Explainer
{
    internal const string TablePart = "PART_Table";
    internal const string CacheBarPart = "PART_CacheBar";
    internal const string TogglePart = "PART_Toggle";

    /// <summary>Set below 660px of content width: the cache panel moves above the requests.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set below 480px of content width: each request takes three lines and the statistics stack.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set below 260px of content width: the playback buttons move under the title.</summary>
    public const string TinyPseudoClass = ":tiny";

    /// <summary>Set while nothing is cached.</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>Set while a request writes into the cache.</summary>
    public const string WritingPseudoClass = ":writing";

    /// <summary>Set while a request reads the cache.</summary>
    public const string ReadingPseudoClass = ":reading";

    /// <summary>Set while the cache holds the prefix and counts down.</summary>
    public const string WarmPseudoClass = ":warm";

    /// <summary>Set once the cache expired.</summary>
    public const string ExpiredPseudoClass = ":expired";

    /// <summary>Set while at most a minute of the time to live is left.</summary>
    public const string LowPseudoClass = ":low";

    /// <summary>Set while the change toggle is armed.</summary>
    public const string ArmedPseudoClass = ":armed";

    /// <summary>The most requests one round shows.</summary>
    public const int MaxRequests = 4;

    /// <summary>Defines the <see cref="Segments"/> property.</summary>
    public static readonly StyledProperty<AvaloniaList<PromptCacheSegment>> SegmentsProperty =
        AvaloniaProperty.Register<PromptCache, AvaloniaList<PromptCacheSegment>>(nameof(Segments));

    /// <summary>Defines the <see cref="SegmentsSource"/> property.</summary>
    public static readonly StyledProperty<IEnumerable?> SegmentsSourceProperty =
        AvaloniaProperty.Register<PromptCache, IEnumerable?>(nameof(SegmentsSource));

    /// <summary>Defines the <see cref="Requests"/> property.</summary>
    public static readonly StyledProperty<AvaloniaList<PromptCacheRequest>> RequestsProperty =
        AvaloniaProperty.Register<PromptCache, AvaloniaList<PromptCacheRequest>>(nameof(Requests));

    /// <summary>Defines the <see cref="RequestsSource"/> property.</summary>
    public static readonly StyledProperty<IEnumerable?> RequestsSourceProperty =
        AvaloniaProperty.Register<PromptCache, IEnumerable?>(nameof(RequestsSource));

    /// <summary>Defines the <see cref="Pricing"/> property.</summary>
    public static readonly StyledProperty<PromptCachePricing?> PricingProperty =
        AvaloniaProperty.Register<PromptCache, PromptCachePricing?>(nameof(Pricing));

    /// <summary>Defines the <see cref="Ttl"/> property.</summary>
    public static readonly StyledProperty<double> TtlProperty =
        AvaloniaProperty.Register<PromptCache, double>(nameof(Ttl), PromptCacheDefaults.Ttl, coerce: CoerceTtl);

    /// <summary>Defines the <see cref="ChangeSegmentId"/> property.</summary>
    public static readonly StyledProperty<string?> ChangeSegmentIdProperty =
        AvaloniaProperty.Register<PromptCache, string?>(nameof(ChangeSegmentId), PromptCacheDefaults.ChangeSegmentId);

    /// <summary>Defines the <see cref="IsChangeArmed"/> property.</summary>
    public static readonly StyledProperty<bool> IsChangeArmedProperty =
        AvaloniaProperty.Register<PromptCache, bool>(nameof(IsChangeArmed), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="FirstTokenEstimator"/> property.</summary>
    public static readonly StyledProperty<PromptCacheFirstTokenEstimator?> FirstTokenEstimatorProperty =
        AvaloniaProperty.Register<PromptCache, PromptCacheFirstTokenEstimator?>(nameof(FirstTokenEstimator));

    /// <summary>Defines the <see cref="Labels"/> property.</summary>
    public static readonly StyledProperty<PromptCacheLabels> LabelsProperty =
        AvaloniaProperty.Register<PromptCache, PromptCacheLabels>(nameof(Labels), new PromptCacheLabels());

    /// <summary>Defines the <see cref="CacheState"/> property.</summary>
    public static readonly DirectProperty<PromptCache, PromptCacheState> CacheStateProperty =
        AvaloniaProperty.RegisterDirect<PromptCache, PromptCacheState>(nameof(CacheState), o => o.CacheState);

    /// <summary>Defines the <see cref="RequestCompleted"/> routed event.</summary>
    public static readonly RoutedEvent<PromptCacheRequestCompletedEventArgs> RequestCompletedEvent =
        RoutedEvent.Register<PromptCache, PromptCacheRequestCompletedEventArgs>(nameof(RequestCompleted), RoutingStrategies.Bubble);

    /// <summary>Defines the <see cref="Expired"/> routed event.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ExpiredEvent =
        RoutedEvent.Register<PromptCache, RoutedEventArgs>(nameof(Expired), RoutingStrategies.Bubble);

    // Ink: theme colors for the drawn bars, set in the ControlTheme from ArcDesign tokens (they follow theme and accent).
    internal static readonly StyledProperty<IBrush?> SurfaceInkProperty =
        AvaloniaProperty.Register<PromptCache, IBrush?>("SurfaceInk");

    internal static readonly StyledProperty<IBrush?> TrackInkProperty =
        AvaloniaProperty.Register<PromptCache, IBrush?>("TrackInk");

    internal static readonly StyledProperty<IBrush?> MessageInkProperty =
        AvaloniaProperty.Register<PromptCache, IBrush?>("MessageInk");

    internal static readonly StyledProperty<IBrush?> WarningInkProperty =
        AvaloniaProperty.Register<PromptCache, IBrush?>("WarningInk");

    internal static readonly StyledProperty<IBrush?> KeyInkProperty =
        AvaloniaProperty.Register<PromptCache, IBrush?>("KeyInk");

    internal static readonly StyledProperty<IBrush?> Series1InkProperty =
        AvaloniaProperty.Register<PromptCache, IBrush?>("Series1Ink");

    internal static readonly StyledProperty<IBrush?> Series2InkProperty =
        AvaloniaProperty.Register<PromptCache, IBrush?>("Series2Ink");

    internal static readonly StyledProperty<IBrush?> Series3InkProperty =
        AvaloniaProperty.Register<PromptCache, IBrush?>("Series3Ink");

    internal static readonly StyledProperty<IBrush?> Series4InkProperty =
        AvaloniaProperty.Register<PromptCache, IBrush?>("Series4Ink");

    internal IBrush? SurfaceInk { get => GetValue(SurfaceInkProperty); set => SetValue(SurfaceInkProperty, value); }

    internal IBrush? TrackInk { get => GetValue(TrackInkProperty); set => SetValue(TrackInkProperty, value); }

    internal IBrush? MessageInk { get => GetValue(MessageInkProperty); set => SetValue(MessageInkProperty, value); }

    internal IBrush? WarningInk { get => GetValue(WarningInkProperty); set => SetValue(WarningInkProperty, value); }

    internal IBrush? KeyInk { get => GetValue(KeyInkProperty); set => SetValue(KeyInkProperty, value); }

    internal IBrush? Series1Ink { get => GetValue(Series1InkProperty); set => SetValue(Series1InkProperty, value); }

    internal IBrush? Series2Ink { get => GetValue(Series2InkProperty); set => SetValue(Series2InkProperty, value); }

    internal IBrush? Series3Ink { get => GetValue(Series3InkProperty); set => SetValue(Series3InkProperty, value); }

    internal IBrush? Series4Ink { get => GetValue(Series4InkProperty); set => SetValue(Series4InkProperty, value); }

    private PromptCacheState _cacheState;
    private AvaloniaList<PromptCacheSegment>? _observedSegments;
    private AvaloniaList<PromptCacheRequest>? _observedRequests;
    private INotifyCollectionChanged? _observedSegmentsSource;
    private INotifyCollectionChanged? _observedRequestsSource;
    private readonly HashSet<AvaloniaObject> _observedItems = new();
    private object?[]? _dataSeen;
    private object?[]? _looksSeen;
    private IReadOnlyList<PromptCacheSegment>? _defaultSegments;
    private IReadOnlyList<PromptCacheRequest>? _defaultRequests;
    private bool _rebuildQueued;

    static PromptCache()
    {
        IsLoopingProperty.OverrideDefaultValue<PromptCache>(true);
        TitleProperty.OverrideDefaultValue<PromptCache>("Prompt cache");
        ModelProperty.OverrideDefaultValue<PromptCache>(new ExplainerModel { Name = PromptCacheDefaults.ModelName });
        PlaybackLabelsProperty.OverrideDefaultValue<PromptCache>(new ExplainerPlaybackLabels { Step = "Send the next request" });
    }

    /// <summary>Creates the control with the built-in story.</summary>
    public PromptCache()
    {
        SetCurrentValue(SegmentsProperty, new AvaloniaList<PromptCacheSegment>());
        SetCurrentValue(RequestsProperty, new AvaloniaList<PromptCacheRequest>());
    }

    /// <summary>
    /// The stable prefix in prompt order. Prefix matching runs left to right, so put what changes least first. While the
    /// list is empty (and <see cref="SegmentsSource"/> is null) the built-in story's four segments are shown.
    /// </summary>
    public AvaloniaList<PromptCacheSegment> Segments { get => GetValue(SegmentsProperty); set => SetValue(SegmentsProperty, value); }

    /// <summary>A collection of <see cref="PromptCacheSegment"/>s that replaces <see cref="Segments"/> (observed when it raises collection changes).</summary>
    public IEnumerable? SegmentsSource { get => GetValue(SegmentsSourceProperty); set => SetValue(SegmentsSourceProperty, value); }

    /// <summary>
    /// One to four requests per round (more are ignored). While the list is empty (and <see cref="RequestsSource"/> is null)
    /// the built-in story's four questions are sent.
    /// </summary>
    public AvaloniaList<PromptCacheRequest> Requests { get => GetValue(RequestsProperty); set => SetValue(RequestsProperty, value); }

    /// <summary>A collection of <see cref="PromptCacheRequest"/>s that replaces <see cref="Requests"/> (observed when it raises collection changes).</summary>
    public IEnumerable? RequestsSource { get => GetValue(RequestsSourceProperty); set => SetValue(RequestsSourceProperty, value); }

    /// <summary>The prices behind every cost (dollars per million tokens); null uses sample prices ($3.00 / $3.75 / $0.30).</summary>
    public PromptCachePricing? Pricing { get => GetValue(PricingProperty); set => SetValue(PricingProperty, value); }

    /// <summary>
    /// Simulated seconds the cache lives after its last use (default 300). Every hit refreshes it. It is shown as the ring,
    /// the countdown and the header note.
    /// </summary>
    public double Ttl { get => GetValue(TtlProperty); set => SetValue(TtlProperty, value); }

    /// <summary>
    /// The id of the segment the change toggle edits (default "rules", the built-in story's instructions). Null, or an id
    /// that matches no segment, hides the toggle.
    /// </summary>
    public string? ChangeSegmentId { get => GetValue(ChangeSegmentIdProperty); set => SetValue(ChangeSegmentIdProperty, value); }

    /// <summary>
    /// True while an edit of the <see cref="ChangeSegmentId"/> segment is armed for the next request (the toggle's state).
    /// It turns false once that request is sent.
    /// </summary>
    public bool IsChangeArmed { get => GetValue(IsChangeArmedProperty); set => SetValue(IsChangeArmedProperty, value); }

    /// <summary>
    /// Seconds to the first output token from the processed and cached tokens. Null uses an illustrative model:
    /// 0.40 + processed / 45,000 + cached / 600,000.
    /// </summary>
    public PromptCacheFirstTokenEstimator? FirstTokenEstimator
    {
        get => GetValue(FirstTokenEstimatorProperty);
        set => SetValue(FirstTokenEstimatorProperty, value);
    }

    /// <summary>Every word the card shows or speaks.</summary>
    public PromptCacheLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>What the cache is doing now.</summary>
    public PromptCacheState CacheState { get => _cacheState; private set => SetAndRaise(CacheStateProperty, ref _cacheState, value); }

    /// <summary>The finished requests of the current round, oldest first.</summary>
    public IReadOnlyList<PromptCacheResult> Results => _results;

    /// <summary>Raised when a simulated request finishes, with its outcome.</summary>
    public event EventHandler<PromptCacheRequestCompletedEventArgs>? RequestCompleted
    {
        add => AddHandler(RequestCompletedEvent, value);
        remove => RemoveHandler(RequestCompletedEvent, value);
    }

    /// <summary>Raised when the cache expires.</summary>
    public event EventHandler<RoutedEventArgs>? Expired
    {
        add => AddHandler(ExpiredEvent, value);
        remove => RemoveHandler(ExpiredEvent, value);
    }

    private static double CoerceTtl(AvaloniaObject owner, double value) => double.IsFinite(value) && value > 0 ? value : PromptCacheDefaults.Ttl;

    /// <summary>The segments in effect (the source, the list, or the built-in story).</summary>
    internal IReadOnlyList<PromptCacheSegment> EffectiveSegments()
    {
        if (SegmentsSource is { } source)
        {
            return source.OfType<PromptCacheSegment>().ToArray();
        }
        if (Segments is { Count: > 0 } list)
        {
            return list.ToArray();
        }
        return _defaultSegments ??= PromptCacheDefaults.CreateSegments();
    }

    /// <summary>The requests in effect (at most <see cref="MaxRequests"/>).</summary>
    internal IReadOnlyList<PromptCacheRequest> EffectiveRequests()
    {
        IEnumerable<PromptCacheRequest> items = RequestsSource is { } source
            ? source.OfType<PromptCacheRequest>()
            : Requests is { Count: > 0 } list ? list : _defaultRequests ??= PromptCacheDefaults.CreateRequests();
        return items.Take(MaxRequests).ToArray();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == SegmentsProperty || p == RequestsProperty || p == SegmentsSourceProperty || p == RequestsSourceProperty)
        {
            ObserveData();
            QueueRebuild();
        }
        else if (p == PricingProperty || p == TtlProperty || p == ChangeSegmentIdProperty || p == FirstTokenEstimatorProperty)
        {
            QueueRebuild();
        }
        else if (p == IsChangeArmedProperty)
        {
            OnArmedChanged();
        }
        else if (p == LabelsProperty || p == TitleProperty || p == ModelProperty)
        {
            RefreshStaticTexts();
        }
        else if (p == IsSteppingProperty && IsStepping)
        {
            AlignStep();
        }
        else if (p == ArcMotion.LevelProperty)
        {
            OnMotionLevelChanged();
        }
        else if (p.Name.EndsWith("Ink", StringComparison.Ordinal) && p.OwnerType == typeof(PromptCache))
        {
            InvalidatePalette();
        }
    }

    private protected override void WatchData(bool watch)
    {
        ObserveData();
        if (!watch || _dataSeen is null)
        {
            return;
        }
        // Catch up with changes made while nothing listened, as the same changes seen live would.
        if (!_dataSeen.SequenceEqual(DataState()))
        {
            QueueRebuild();
        }
        else if (!_looksSeen!.SequenceEqual(LooksState()))
        {
            _looksSeen = LooksState();
            InvalidatePalette();
            RebuildLegend();
            RefreshStaticTexts();
        }
    }

    /// <summary>The lists and items may be the host's: they are observed only while the card is attached.</summary>
    private void ObserveData()
    {
        foreach (var item in _observedItems)
        {
            item.PropertyChanged -= OnItemChanged;
        }
        _observedItems.Clear();
        var attached = IsAttached;
        Swap(ref _observedSegments, attached ? Segments : null);
        Swap(ref _observedRequests, attached ? Requests : null);
        SwapSource(ref _observedSegmentsSource, attached ? SegmentsSource as INotifyCollectionChanged : null);
        SwapSource(ref _observedRequestsSource, attached ? RequestsSource as INotifyCollectionChanged : null);
        if (!attached)
        {
            return;
        }
        foreach (var item in (SegmentsSource?.OfType<AvaloniaObject>() ?? Segments ?? Enumerable.Empty<AvaloniaObject>())
                 .Concat(RequestsSource?.OfType<AvaloniaObject>() ?? Requests ?? Enumerable.Empty<AvaloniaObject>()))
        {
            if (_observedItems.Add(item))
            {
                item.PropertyChanged += OnItemChanged;
            }
        }
    }

    private void Swap<T>(ref AvaloniaList<T>? field, AvaloniaList<T>? value)
    {
        if (ReferenceEquals(field, value))
        {
            return;
        }
        if (field is not null)
        {
            field.CollectionChanged -= OnDataCollectionChanged;
        }
        field = value;
        if (field is not null)
        {
            field.CollectionChanged += OnDataCollectionChanged;
        }
    }

    private void SwapSource(ref INotifyCollectionChanged? field, INotifyCollectionChanged? value)
    {
        if (ReferenceEquals(field, value))
        {
            return;
        }
        if (field is not null)
        {
            field.CollectionChanged -= OnDataCollectionChanged;
        }
        field = value;
        if (field is not null)
        {
            field.CollectionChanged += OnDataCollectionChanged;
        }
    }

    /// <summary>The items the story is built from and the values that rebuild it, to tell whether they changed unobserved.</summary>
    private object?[] DataState()
    {
        var state = new List<object?>();
        foreach (var segment in EffectiveSegments())
        {
            state.Add(segment);
            state.Add(segment.Id);
            state.Add(segment.Tokens);
        }
        foreach (var request in EffectiveRequests())
        {
            state.Add(request);
            state.Add(request.Tokens);
            state.Add(request.Gap);
        }
        return state.ToArray();
    }

    /// <summary>The segment names and colors (a change redraws the legend without starting over).</summary>
    private object?[] LooksState()
    {
        var state = new List<object?>();
        foreach (var segment in EffectiveSegments())
        {
            state.Add(segment.Label);
            state.Add(segment.Color);
        }
        return state.ToArray();
    }

    private void OnDataCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ObserveData();
        QueueRebuild();
    }

    private void OnItemChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == PromptCacheSegment.ColorProperty)
        {
            InvalidatePalette();
            RebuildLegend();
            return;
        }
        if (e.Property == PromptCacheSegment.LabelProperty)
        {
            RebuildLegend();
            RefreshStaticTexts();
            return;
        }
        if (e.Property == PromptCacheRequest.MessageProperty)
        {
            return;
        }
        QueueRebuild();
    }

    // Data changes arrive in bursts (a collection filled item by item, several properties set together): rebuild once.
    private void QueueRebuild()
    {
        // Before the first build there is nothing to rebuild: the build reads the latest data.
        if (_rebuildQueued || !_isBuilt)
        {
            return;
        }
        _rebuildQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (_rebuildQueued)
            {
                RebuildNow();
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>Applies queued data changes now and restarts the story (tests; the dispatcher does it otherwise).</summary>
    internal void RebuildNow()
    {
        _rebuildQueued = false;
        if (!_isBuilt)
        {
            SnapshotData();
            RefreshAllViews();
            return;
        }
        Rebuild();
    }
}
