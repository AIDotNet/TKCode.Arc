using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A live stats strip for a streaming LLM response (Arc Generation stats, design reference). The head shows the model's
/// mark, its name and a status with an icon and a word (Waiting, Streaming, Done in 4.18s, Stopped at 1.90s, Failed).
/// Below it four cells split by hairlines: time to first token, speed in tokens per second with a small sparkline, output
/// tokens and cost. While the response streams every number counts up in tabular digits and the sparkline draws; when it
/// ends the numbers settle on exact values. Details opens a breakdown with a latency bar (first token, generation, total)
/// and a token table (input, cached input, output, reasoning, total) with rates and cost. A failed response gets a row with
/// the reason and Retry.
/// <para>
/// Feed it from your streaming handler: set <see cref="StartedAt"/> when the request leaves, <see cref="Status"/> Waiting
/// until the first chunk, then Streaming while you set a new <see cref="Usage"/> with the growing output tokens, then Done
/// with the duration. Speed, latency and cost follow. Leave <see cref="Status"/> unset and set <see cref="Simulation"/> to
/// play simulated runs of your own; with neither the strip is idle and every value reads a dash.
/// </para>
/// </summary>
[TemplatePart(FramePart, typeof(Border))]
[TemplatePart(DetailsPart, typeof(GenerationStatsDetailsButton))]
[TemplatePart(InlineDetailsPart, typeof(GenerationStatsDetailsButton))]
[TemplatePart(RetryPart, typeof(AvaloniaButton))]
[TemplatePart(ErrorBoxPart, typeof(AiHeightBox))]
[TemplatePart(BreakdownBoxPart, typeof(AiHeightBox))]
[TemplatePart(CopyPart, typeof(CopyButton))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(InlinePseudoClass, ExpandedPseudoClass, NarrowPseudoClass, CompactPseudoClass, ErrorPseudoClass, SamplePseudoClass)]
public partial class GenerationStats : TemplatedControl
{
    internal const string FramePart = "PART_Frame";
    internal const string DetailsPart = "PART_Details";
    internal const string InlineDetailsPart = "PART_InlineDetails";
    internal const string RetryPart = "PART_Retry";
    internal const string ErrorBoxPart = "PART_ErrorBox";
    internal const string BreakdownBoxPart = "PART_BreakdownBox";
    internal const string CopyPart = "PART_Copy";
    internal const string AnnouncerPart = "PART_Announcer";

    internal const string InlinePseudoClass = ":inline";
    internal const string ExpandedPseudoClass = ":expanded";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string CompactPseudoClass = ":compact";
    internal const string ErrorPseudoClass = ":error";
    internal const string SamplePseudoClass = ":sample";

    /// <summary>At and above this strip width the four cells sit in one row; below it they form a two by two grid.</summary>
    internal const double WideWidth = 600;

    /// <summary>Below this strip width Details becomes an icon button and the table's Rate column hides.</summary>
    internal const double CompactWidth = 440;

    /// <summary>Where the response is. Null (the default) plays <see cref="Simulation"/>, or shows the idle strip without one.</summary>
    public static readonly StyledProperty<GenerationStatus?> StatusProperty =
        AvaloniaProperty.Register<GenerationStats, GenerationStatus?>(nameof(Status));

    /// <summary>The model that answered: its name, your provider key, its API id and, optionally, its mark.</summary>
    public static readonly StyledProperty<GenerationModel?> ModelProperty =
        AvaloniaProperty.Register<GenerationStats, GenerationModel?>(nameof(Model));

    /// <summary>
    /// When the request was sent. While waiting, time to first token counts live from here; while streaming, total latency
    /// does.
    /// </summary>
    public static readonly StyledProperty<DateTimeOffset?> StartedAtProperty =
        AvaloniaProperty.Register<GenerationStats, DateTimeOffset?>(nameof(StartedAt));

    /// <summary>
    /// The token usage and timing. Set a new one as chunks arrive (output tokens), on the first token (<c>Ttft</c>) and on
    /// the last (<c>Duration</c>). Speed and cost are derived unless it carries them.
    /// </summary>
    public static readonly StyledProperty<GenerationUsage?> UsageProperty =
        AvaloniaProperty.Register<GenerationStats, GenerationUsage?>(nameof(Usage));

    /// <summary>
    /// Prices in US dollars per million tokens. Enables the Rate and Cost columns and the live cost. Leave it out when the
    /// usage carries the provider's cost; the Rate column then hides.
    /// </summary>
    public static readonly StyledProperty<GenerationPricing?> PricingProperty =
        AvaloniaProperty.Register<GenerationStats, GenerationPricing?>(nameof(Pricing));

    /// <summary>
    /// Speed readings in tokens per second, oldest first, for the sparkline. Null (the default) reads the speed every 160ms
    /// while the output grows, smoothed, keeping the whole run's shape in at most 48 points.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> SpeedSamplesProperty =
        AvaloniaProperty.Register<GenerationStats, IReadOnlyList<double>?>(nameof(SpeedSamples));

    /// <summary>The reason shown in the error row when <see cref="Status"/> is Error.</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<GenerationStats, string?>(nameof(ErrorText));

    /// <summary>Shows Retry in the error row; it raises <see cref="RetryRequested"/>.</summary>
    public static readonly StyledProperty<bool> AllowsRetryProperty =
        AvaloniaProperty.Register<GenerationStats, bool>(nameof(AllowsRetry), true);

    /// <summary>A bordered card with four cells, or one quiet wrapping line for under a chat message.</summary>
    public static readonly StyledProperty<GenerationStatsVariant> VariantProperty =
        AvaloniaProperty.Register<GenerationStats, GenerationStatsVariant>(nameof(Variant));

    /// <summary>Whether the breakdown is open (two-way). Set it in XAML to open it on first render.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<GenerationStats, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// A footnote under the breakdown, for example where the rates come from (text or any content). A playing
    /// <see cref="Simulation"/> says its runs are simulated when it is not set.
    /// </summary>
    public static readonly StyledProperty<object?> NoteProperty =
        AvaloniaProperty.Register<GenerationStats, object?>(nameof(Note));

    /// <summary>
    /// Simulated runs to play while <see cref="Status"/> is null: they loop while the strip is on screen, hold while the
    /// breakdown is open, settle at once under reduced motion and retry from the error row. Null (the default) shows the
    /// idle strip. Give each strip its own simulation.
    /// </summary>
    public static readonly StyledProperty<GenerationStatsSimulation?> SimulationProperty =
        AvaloniaProperty.Register<GenerationStats, GenerationStatsSimulation?>(nameof(Simulation));

    /// <summary>Every word the strip shows or says.</summary>
    public static readonly StyledProperty<GenerationStatsLabels> LabelsProperty =
        AvaloniaProperty.Register<GenerationStats, GenerationStatsLabels>(nameof(Labels), GenerationStatsLabels.Default);

    /// <summary>Raised when the person presses Retry on a failed response. Bubbles. Send the request again.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RetryRequestedEvent =
        RoutedEvent.Register<GenerationStats, RoutedEventArgs>(nameof(RetryRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses Details (not when <see cref="IsExpanded"/> is set in code). Bubbles.</summary>
    public static readonly RoutedEvent<GenerationStatsDetailsToggledEventArgs> DetailsToggledEvent =
        RoutedEvent.Register<GenerationStats, GenerationStatsDetailsToggledEventArgs>(nameof(DetailsToggled), RoutingStrategies.Bubble);

    /// <summary>Creates the strip.</summary>
    public GenerationStats()
    {
        AddHandler(PointerPressedEvent, (_, _) => _interacted = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, (_, _) => _interacted = true, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <inheritdoc cref="StatusProperty"/>
    public GenerationStatus? Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="ModelProperty"/>
    public GenerationModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <inheritdoc cref="StartedAtProperty"/>
    public DateTimeOffset? StartedAt { get => GetValue(StartedAtProperty); set => SetValue(StartedAtProperty, value); }

    /// <inheritdoc cref="UsageProperty"/>
    public GenerationUsage? Usage { get => GetValue(UsageProperty); set => SetValue(UsageProperty, value); }

    /// <inheritdoc cref="PricingProperty"/>
    public GenerationPricing? Pricing { get => GetValue(PricingProperty); set => SetValue(PricingProperty, value); }

    /// <inheritdoc cref="SpeedSamplesProperty"/>
    public IReadOnlyList<double>? SpeedSamples { get => GetValue(SpeedSamplesProperty); set => SetValue(SpeedSamplesProperty, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    /// <inheritdoc cref="AllowsRetryProperty"/>
    public bool AllowsRetry { get => GetValue(AllowsRetryProperty); set => SetValue(AllowsRetryProperty, value); }

    /// <inheritdoc cref="VariantProperty"/>
    public GenerationStatsVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="NoteProperty"/>
    public object? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public GenerationStatsSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public GenerationStatsLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="RetryRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RetryRequested
    {
        add => AddHandler(RetryRequestedEvent, value);
        remove => RemoveHandler(RetryRequestedEvent, value);
    }

    /// <inheritdoc cref="DetailsToggledEvent"/>
    public event EventHandler<GenerationStatsDetailsToggledEventArgs>? DetailsToggled
    {
        add => AddHandler(DetailsToggledEvent, value);
        remove => RemoveHandler(DetailsToggledEvent, value);
    }

    /// <summary>Clears the live speed series and the counted widths, as at the start of a new request.</summary>
    public void Reset()
    {
        ResetRun();
        Refresh(instant: true);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        Hook(e.NameScope);
        PseudoClasses.Set(InlinePseudoClass, Variant == GenerationStatsVariant.Inline);
        PseudoClasses.Set(ExpandedPseudoClass, IsExpanded);
        PseudoClasses.Set(SamplePseudoClass, IsSample);
        UpdateBreakpoints(Bounds.Width);
        Refresh(instant: true);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty)
        {
            PseudoClasses.Set(SamplePseudoClass, IsSample);
            if (IsSample)
            {
                // Back to the simulation: it starts again from its first run.
                ResetRun();
                Simulation!.Restart();
                SettleIfStill();
            }
            OnDataChanged();
        }
        else if (change.Property == SimulationProperty)
        {
            var old = change.GetOldValue<GenerationStatsSimulation?>();
            if (old is not null)
            {
                old.Changed -= OnDataChanged;
            }
            var simulation = change.GetNewValue<GenerationStatsSimulation?>();
            if (simulation is not null)
            {
                simulation.Hold = IsExpanded;
                if (IsAttachedToVisualTreeSafe)
                {
                    simulation.Changed += OnDataChanged;
                }
            }
            PseudoClasses.Set(SamplePseudoClass, IsSample);
            if (Status is null)
            {
                ResetRun();
                SettleIfStill();
                OnDataChanged();
            }
        }
        else if (change.Property == UsageProperty || change.Property == StartedAtProperty || change.Property == ModelProperty
                 || change.Property == PricingProperty || change.Property == ErrorTextProperty)
        {
            if (!IsSample)
            {
                OnDataChanged();
            }
        }
        else if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(ExpandedPseudoClass, IsExpanded);
            if (Simulation is { } simulation)
            {
                simulation.Hold = IsExpanded;
            }
            Refresh(instant: false);
        }
        else if (change.Property == VariantProperty)
        {
            PseudoClasses.Set(InlinePseudoClass, Variant == GenerationStatsVariant.Inline);
            _card.StatusKey = null;
            _inline.StatusKey = null;
            Refresh(instant: true);
        }
        else if (change.Property == LabelsProperty || change.Property == AllowsRetryProperty || change.Property == SpeedSamplesProperty
                 || change.Property == NoteProperty)
        {
            _card.StatusKey = null;
            _inline.StatusKey = null;
            Refresh(instant: false);
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBreakpoints(e.NewSize.Width);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Simulation is { } simulation)
        {
            simulation.Changed -= OnDataChanged;
            simulation.Changed += OnDataChanged;
        }
        if (IsSample)
        {
            if (!ArcMotion.IsFull(this) && Simulation!.Status is not GenerationStatus.Done)
            {
                Simulation.Settle();
            }
            if (!_simulatedSaid)
            {
                _simulatedSaid = true;
                Dispatcher.UIThread.Post(() => _announcer?.Announce(Labels.SimulatedAnnouncement), DispatcherPriority.Background);
            }
        }
        UpdateLoop();
        Refresh(instant: true);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (Simulation is { } simulation)
        {
            simulation.Changed -= OnDataChanged;
        }
        _loop?.Stop();
    }

    /// <summary>Under reduced motion (or off) a simulation shows its current run settled instead of playing it.</summary>
    private void SettleIfStill()
    {
        if (IsSample && IsAttachedToVisualTreeSafe && !ArcMotion.IsFull(this) && Simulation!.Status is not GenerationStatus.Done)
        {
            Simulation.Settle();
        }
    }

    private void UpdateBreakpoints(double width)
    {
        if (!(width > 0))
        {
            return;
        }
        var narrow = width < WideWidth;
        var compact = width < CompactWidth;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (_narrow == narrow && _compact == compact)
        {
            return;
        }
        _narrow = narrow;
        _compact = compact;
        Refresh(instant: true);
    }

    private void OnDetailsClick(object? sender, RoutedEventArgs e)
    {
        _interacted = true;
        var open = !IsExpanded;
        SetCurrentValue(IsExpandedProperty, open);
        RaiseEvent(new GenerationStatsDetailsToggledEventArgs(DetailsToggledEvent, open));
    }

    private void OnRetryClick(object? sender, RoutedEventArgs e)
    {
        _interacted = true;
        if (IsSample)
        {
            Simulation!.Retry();
        }
        RaiseEvent(new RoutedEventArgs(RetryRequestedEvent));
    }

    private void OnCopyClick(object? sender, RoutedEventArgs e) => _interacted = true;
}
