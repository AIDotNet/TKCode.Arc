using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;

using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// Where an order is: the order and carrier, the estimated arrival (or the delivery time), five status steps from ordered
/// to delivered, a drawing of the route with the van on it, and the history of scans, newest first. Moving
/// <see cref="Stage"/> springs the steps forward and announces the new stage; <see cref="RouteProgress"/> moves the van.
/// <code>
/// &lt;arc:DeliveryTracker OrderNumber="4821" Carrier="Larkpost" Stage="Shipped" RouteProgress="0.45"
///                       EstimatedArrival="Thursday, Nov 12" ArrivalWindow="2 – 6 pm" /&gt;
/// </code>
/// </summary>
[TemplatePart(StepsPart, typeof(DeliveryTrackerSteps))]
[TemplatePart(RoutePart, typeof(DeliveryTrackerRoute))]
[TemplatePart(HistoryPart, typeof(StackPanel))]
[TemplatePart(AnnouncerPart, typeof(FinanceAnnouncer))]
[PseudoClasses(CompactPseudoClass, DelayedPseudoClass, DeliveredPseudoClass)]
public class DeliveryTracker : TemplatedControl
{
    public const string StepsPart = "PART_Steps";
    public const string RoutePart = "PART_Route";
    public const string HistoryPart = "PART_History";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>The block is narrower than its compact threshold: the route sits above the history.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>The delivery is late.</summary>
    public const string DelayedPseudoClass = ":delayed";

    /// <summary>The parcel was delivered.</summary>
    public const string DeliveredPseudoClass = ":delivered";

    /// <summary>The order number, without the "#".</summary>
    public static readonly StyledProperty<string?> OrderNumberProperty =
        AvaloniaProperty.Register<DeliveryTracker, string?>(nameof(OrderNumber));

    /// <summary>Who carries the parcel (an invented carrier in the samples).</summary>
    public static readonly StyledProperty<string?> CarrierProperty =
        AvaloniaProperty.Register<DeliveryTracker, string?>(nameof(Carrier));

    /// <summary>The current stage (two-way).</summary>
    public static readonly StyledProperty<DeliveryStage> StageProperty =
        AvaloniaProperty.Register<DeliveryTracker, DeliveryStage>(nameof(Stage), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The arrival day as shown ("Thursday, Nov 12"), or the delivery time once delivered.</summary>
    public static readonly StyledProperty<string?> EstimatedArrivalProperty =
        AvaloniaProperty.Register<DeliveryTracker, string?>(nameof(EstimatedArrival));

    /// <summary>The time window beside the arrival ("2 – 6 pm").</summary>
    public static readonly StyledProperty<string?> ArrivalWindowProperty =
        AvaloniaProperty.Register<DeliveryTracker, string?>(nameof(ArrivalWindow));

    /// <summary>How far along the route the van is, 0 to 1. Delivered draws it at the end whatever the value.</summary>
    public static readonly StyledProperty<double> RouteProgressProperty =
        AvaloniaProperty.Register<DeliveryTracker, double>(nameof(RouteProgress), coerce: (_, v) => double.IsNaN(v) ? 0 : Math.Clamp(v, 0, 1));

    /// <summary>Where the parcel leaves from, for the route's name ("Riverside hub").</summary>
    public static readonly StyledProperty<string?> OriginProperty =
        AvaloniaProperty.Register<DeliveryTracker, string?>(nameof(Origin));

    /// <summary>Where it goes, for the route's name ("Home").</summary>
    public static readonly StyledProperty<string?> DestinationProperty =
        AvaloniaProperty.Register<DeliveryTracker, string?>(nameof(Destination));

    /// <summary>Whether the delivery is late: shows the Delayed pill.</summary>
    public static readonly StyledProperty<bool> IsDelayedProperty =
        AvaloniaProperty.Register<DeliveryTracker, bool>(nameof(IsDelayed));

    /// <summary>The day relative times are counted from; null uses the local date.</summary>
    public static readonly StyledProperty<DateTime?> TodayProperty =
        AvaloniaProperty.Register<DeliveryTracker, DateTime?>(nameof(Today));

    /// <summary>The block's built-in text.</summary>
    public static readonly StyledProperty<DeliveryTrackerLabels> LabelsProperty =
        AvaloniaProperty.Register<DeliveryTracker, DeliveryTrackerLabels>(nameof(Labels), new DeliveryTrackerLabels());

    internal static readonly StyledProperty<double> CompactThresholdProperty =
        AvaloniaProperty.Register<DeliveryTracker, double>(nameof(CompactThreshold), 720);

    /// <summary>Raised when <see cref="Stage"/> changes.</summary>
    public static readonly RoutedEvent<DeliveryStageChangedEventArgs> StageChangedEvent =
        RoutedEvent.Register<DeliveryTracker, DeliveryStageChangedEventArgs>(nameof(StageChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="StageChangedEvent"/>
    public event EventHandler<DeliveryStageChangedEventArgs>? StageChanged
    {
        add => AddHandler(StageChangedEvent, value);
        remove => RemoveHandler(StageChangedEvent, value);
    }

    private INameScope? _scope;
    private DeliveryTrackerSteps? _steps;
    private DeliveryTrackerRoute? _route;
    private StackPanel? _history;
    private FinanceAnnouncer? _announcer;
    private bool _isCompact;

    static DeliveryTracker()
    {
        FocusableProperty.OverrideDefaultValue<DeliveryTracker>(false);
    }

    public DeliveryTracker()
    {
        Events.CollectionChanged += (_, _) => BuildHistory();
    }

    /// <inheritdoc cref="OrderNumberProperty"/>
    public string? OrderNumber { get => GetValue(OrderNumberProperty); set => SetValue(OrderNumberProperty, value); }

    /// <inheritdoc cref="CarrierProperty"/>
    public string? Carrier { get => GetValue(CarrierProperty); set => SetValue(CarrierProperty, value); }

    /// <inheritdoc cref="StageProperty"/>
    public DeliveryStage Stage { get => GetValue(StageProperty); set => SetValue(StageProperty, value); }

    /// <inheritdoc cref="EstimatedArrivalProperty"/>
    public string? EstimatedArrival { get => GetValue(EstimatedArrivalProperty); set => SetValue(EstimatedArrivalProperty, value); }

    /// <inheritdoc cref="ArrivalWindowProperty"/>
    public string? ArrivalWindow { get => GetValue(ArrivalWindowProperty); set => SetValue(ArrivalWindowProperty, value); }

    /// <inheritdoc cref="RouteProgressProperty"/>
    public double RouteProgress { get => GetValue(RouteProgressProperty); set => SetValue(RouteProgressProperty, value); }

    /// <inheritdoc cref="OriginProperty"/>
    public string? Origin { get => GetValue(OriginProperty); set => SetValue(OriginProperty, value); }

    /// <inheritdoc cref="DestinationProperty"/>
    public string? Destination { get => GetValue(DestinationProperty); set => SetValue(DestinationProperty, value); }

    /// <inheritdoc cref="IsDelayedProperty"/>
    public bool IsDelayed { get => GetValue(IsDelayedProperty); set => SetValue(IsDelayedProperty, value); }

    /// <inheritdoc cref="TodayProperty"/>
    public DateTime? Today { get => GetValue(TodayProperty); set => SetValue(TodayProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public DeliveryTrackerLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    internal double CompactThreshold { get => GetValue(CompactThresholdProperty); set => SetValue(CompactThresholdProperty, value); }

    /// <summary>The scans and updates, in any order (the history lists them newest first).</summary>
    [Content]
    public AvaloniaList<DeliveryEvent> Events { get; } = new();

    /// <summary>True while the block is narrower than its compact threshold.</summary>
    public bool IsCompact => _isCompact;

    /// <summary>Where the van is drawn: the route progress, or the end once delivered.</summary>
    public double EffectiveRouteProgress => Stage == DeliveryStage.Delivered ? 1 : RouteProgress;

    /// <summary>The last text sent to the live region (tests and diagnostics).</summary>
    internal string? LastAnnouncement => _announcer?.Announcement;

    /// <summary>Moves to the next stage (nothing once delivered).</summary>
    public void Advance()
    {
        if (Stage < DeliveryStage.Delivered)
        {
            SetCurrentValue(StageProperty, Stage + 1);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _scope = e.NameScope;
        _steps = _scope.Find<DeliveryTrackerSteps>(StepsPart);
        _route = _scope.Find<DeliveryTrackerRoute>(RoutePart);
        _history = _scope.Find<StackPanel>(HistoryPart);
        _announcer = _scope.Find<FinanceAnnouncer>(AnnouncerPart);
        UpdateHeader();
        UpdateSteps();
        UpdateRoute();
        BuildHistory();
        UpdateCompact(force: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == StageProperty)
        {
            UpdateHeader();
            UpdateSteps();
            UpdateRoute();
            var labels = Labels;
            _announcer?.Announce(labels.StageAnnouncement(labels.Stage(Stage)));
            RaiseEvent(new DeliveryStageChangedEventArgs(StageChangedEvent, change.GetOldValue<DeliveryStage>(), Stage));
        }
        else if (p == RouteProgressProperty || p == OriginProperty || p == DestinationProperty || p == OrderNumberProperty)
        {
            UpdateRoute();
            UpdateHeader();
        }
        else if (p == CarrierProperty || p == EstimatedArrivalProperty || p == ArrivalWindowProperty || p == IsDelayedProperty)
        {
            UpdateHeader();
        }
        else if (p == LabelsProperty)
        {
            UpdateHeader();
            UpdateSteps();
            UpdateRoute();
            BuildHistory();
        }
        else if (p == TodayProperty)
        {
            BuildHistory();
        }
        else if (p == BoundsProperty || p == CompactThresholdProperty)
        {
            UpdateCompact();
        }
    }

    private void UpdateHeader()
    {
        var labels = Labels;
        SetText("PART_OrderLine", labels.OrderLine(OrderNumber ?? string.Empty, Carrier ?? string.Empty));
        SetText("PART_ArrivalHeading", labels.ArrivalHeading(Stage));
        SetText("PART_Arrival", EstimatedArrival);
        SetText("PART_Window", ArrivalWindow);
        if (_scope?.Find<Badge>("PART_Delayed") is { } delayed)
        {
            delayed.Content = labels.Delayed;
            delayed.IsVisible = IsDelayed && Stage != DeliveryStage.Delivered;
        }
        PseudoClasses.Set(DelayedPseudoClass, IsDelayed);
        PseudoClasses.Set(DeliveredPseudoClass, Stage == DeliveryStage.Delivered);
    }

    private void UpdateSteps()
    {
        if (_steps is null)
        {
            return;
        }
        var labels = Labels;
        var stages = Enum.GetValues<DeliveryStage>();
        _steps.SetNames(stages.Select(labels.Stage).ToArray());
        _steps.Current = (int)Stage;
        _steps.Fill = (int)Stage;
        AutomationProperties.SetName(_steps, labels.StepsName(labels.Stage(Stage), (int)Stage + 1, stages.Length));
    }

    private void UpdateRoute()
    {
        if (_route is null)
        {
            return;
        }
        _route.Seed = SeedOf(OrderNumber);
        _route.Progress = EffectiveRouteProgress;
        _route.IsPulsing = Stage == DeliveryStage.OutForDelivery;
        AutomationProperties.SetName(_route, Labels.RouteName(Origin ?? string.Empty, Destination ?? string.Empty,
            (int)Math.Round(EffectiveRouteProgress * 100)));
    }

    private static int SeedOf(string? text)
    {
        var hash = 7;
        foreach (var c in text ?? string.Empty)
        {
            hash = unchecked(hash * 31 + c);
        }
        return hash & 0x7FFFFFFF;
    }

    private void BuildHistory()
    {
        if (_history is null)
        {
            return;
        }
        var labels = Labels;
        SetText("PART_HistoryTitle", labels.History);
        SetText("PART_NoHistory", labels.NoHistory);
        _history.Children.Clear();
        var today = Today?.Date ?? DateTime.Today;
        var ordered = Events.OrderByDescending(e => e.At).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var item = ordered[i];
            var dot = new DeliveryTrackerDot { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 5, 0, 0) };
            if (i == 0)
            {
                dot.Classes.Add("latest");
            }
            if (item.IsException)
            {
                dot.Classes.Add("exception");
            }
            var rail = new Border { Width = 2, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0), IsVisible = i < ordered.Count - 1 };
            ArcTokens.Bind(rail, Border.BackgroundProperty, ArcDesignTokenKind.BorderSubtle);
            var marker = new DockPanel { Width = 10, Margin = new Thickness(0, 0, 12, 0) };
            DockPanel.SetDock(dot, Dock.Top);
            marker.Children.Add(dot);
            marker.Children.Add(rail);

            var title = new TextBlock { Text = item.Title, TextWrapping = TextWrapping.Wrap };
            if (i == 0)
            {
                ArcTokens.Bind(title, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
            }
            var meta = item.Location is { Length: > 0 } location ? $"{labels.Time(item.At, today)} · {location}" : labels.Time(item.At, today);
            var metaText = new TextBlock { Text = meta, TextWrapping = TextWrapping.Wrap };
            ArcTokens.Bind(metaText, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            ArcTokens.Bind(metaText, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
            var texts = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 14), Children = { title, metaText } };

            var row = new DockPanel();
            DockPanel.SetDock(marker, Dock.Left);
            row.Children.Add(marker);
            row.Children.Add(texts);
            AutomationProperties.SetName(row, $"{item.Title}, {meta}");
            _history.Children.Add(row);
        }
        if (_scope?.Find<TextBlock>("PART_NoHistory") is { } empty)
        {
            empty.IsVisible = ordered.Count == 0;
        }
    }

    private void UpdateCompact(bool force = false)
    {
        var width = Bounds.Width;
        var compact = width > 0 ? width < CompactThreshold : _isCompact;
        if (!force && compact == _isCompact)
        {
            return;
        }
        _isCompact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
    }

    private void SetText(string part, string? text)
    {
        if (_scope?.Find<TextBlock>(part) is { } block)
        {
            block.Text = text;
            block.IsVisible = !string.IsNullOrEmpty(text);
        }
    }
}

/// <summary>The dot of a history row (classes latest, exception).</summary>
internal sealed class DeliveryTrackerDot : Border
{
    protected override Type StyleKeyOverride => typeof(DeliveryTrackerDot);
}
