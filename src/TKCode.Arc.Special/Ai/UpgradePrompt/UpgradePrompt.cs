using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// The upgrade nudge shown the moment someone hits a limit (Arc Upgrade prompt, design reference): a title that says what
/// was used, a usage meter, the next plan's price rolling between monthly and yearly with a billing toggle, the three
/// things the plan unlocks, and Maybe later beside Upgrade. Upgrading runs <see cref="UpgradeHandler"/> and rolls into a
/// success state with the new limits while the meter settles to the new limit; a failure shows its message under the
/// price with Try again; Maybe later shows a calm reminder with See Pro again.
/// <para>
/// Every state shares one cell, so the card never changes size between offer, processing, error, success and reminder and
/// the buttons stay in place. Your app gives the plan and the usage: with no <see cref="Plan"/> the card is empty ("No plan
/// to offer", no meter and no offer, Upgrade and Maybe later off). With no handler it simulates a 1.1s upgrade that charges
/// nothing.
/// </para>
/// </summary>
[TemplatePart(RootPart, typeof(Panel))]
[PseudoClasses(NarrowPseudoClass)]
public partial class UpgradePrompt : TemplatedControl
{
    internal const string RootPart = "PART_Root";
    internal const string NarrowPseudoClass = ":narrow";

    /// <summary>How much of the limit is used.</summary>
    public static readonly StyledProperty<double> UsedProperty =
        AvaloniaProperty.Register<UpgradePrompt, double>(nameof(Used));

    /// <summary>The current plan's limit. At or over it the meter reads Limit reached; below it, how many are left.</summary>
    public static readonly StyledProperty<double> LimitProperty =
        AvaloniaProperty.Register<UpgradePrompt, double>(nameof(Limit));

    /// <summary>What is counted, plural ("AI credits", "seats", "generations"). Used in the title, the meter and announcements.</summary>
    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<UpgradePrompt, string>(nameof(Unit), "AI credits");

    /// <summary>When the current limit resets ("Nov 1"). Shown in the description, the meter and the reminder; empty by default.</summary>
    public static readonly StyledProperty<string> ResetsOnProperty =
        AvaloniaProperty.Register<UpgradePrompt, string>(nameof(ResetsOn), string.Empty);

    /// <summary>The plan offered. Null (the default) shows the empty card: nothing to offer.</summary>
    public static readonly StyledProperty<UpgradePromptPlan?> PlanProperty =
        AvaloniaProperty.Register<UpgradePrompt, UpgradePromptPlan?>(nameof(Plan));

    /// <summary>The billing interval (two-way). Yearly by default.</summary>
    public static readonly StyledProperty<UpgradePromptInterval> IntervalProperty =
        AvaloniaProperty.Register<UpgradePrompt, UpgradePromptInterval>(nameof(Interval), UpgradePromptInterval.Yearly,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Replaces the limit title of the offer, such as "You've used all 3 seats". Null writes it from the usage.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<UpgradePrompt, string?>(nameof(Title));

    /// <summary>Replaces the line under the offer's title. Null writes it from the plan and the reset date.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<UpgradePrompt, string?>(nameof(Description));

    /// <summary>
    /// Starts the upgrade with the selection (plan, interval, amount charged today, currency). Complete the task when the
    /// plan is active; throw to show the exception's message under the price with Try again. The token is cancelled when
    /// the prompt leaves the visual tree. Null simulates a 1.1s upgrade that charges nothing.
    /// </summary>
    public static readonly StyledProperty<Func<UpgradePromptSelection, CancellationToken, Task>?> UpgradeHandlerProperty =
        AvaloniaProperty.Register<UpgradePrompt, Func<UpgradePromptSelection, CancellationToken, Task>?>(nameof(UpgradeHandler));

    /// <summary>Every word the prompt shows or announces.</summary>
    public static readonly StyledProperty<UpgradePromptLabels> LabelsProperty =
        AvaloniaProperty.Register<UpgradePrompt, UpgradePromptLabels>(nameof(Labels), UpgradePromptLabels.Default);

    /// <summary>What the prompt shows: the offer, processing, an error, success or the reminder.</summary>
    public static readonly DirectProperty<UpgradePrompt, UpgradePromptState> StateProperty =
        AvaloniaProperty.RegisterDirect<UpgradePrompt, UpgradePromptState>(nameof(State), o => o.State);

    /// <summary>The message of the last failed upgrade while the prompt is in the error state, otherwise null.</summary>
    public static readonly DirectProperty<UpgradePrompt, string?> ErrorMessageProperty =
        AvaloniaProperty.RegisterDirect<UpgradePrompt, string?>(nameof(ErrorMessage), o => o.ErrorMessage);

    /// <summary>Raised when the person changes the billing toggle. Bubbles.</summary>
    public static readonly RoutedEvent<UpgradePromptIntervalChangedEventArgs> IntervalChangedEvent =
        RoutedEvent.Register<UpgradePrompt, UpgradePromptIntervalChangedEventArgs>(nameof(IntervalChanged), RoutingStrategies.Bubble);

    /// <summary>Raised once the upgrade succeeded, after the success state shows. Bubbles.</summary>
    public static readonly RoutedEvent<UpgradePromptUpgradedEventArgs> UpgradedEvent =
        RoutedEvent.Register<UpgradePrompt, UpgradePromptUpgradedEventArgs>(nameof(Upgraded), RoutingStrategies.Bubble);

    /// <summary>Raised when the upgrade handler threw, with the exception. Bubbles.</summary>
    public static readonly RoutedEvent<UpgradePromptFailedEventArgs> UpgradeFailedEvent =
        RoutedEvent.Register<UpgradePrompt, UpgradePromptFailedEventArgs>(nameof(UpgradeFailed), RoutingStrategies.Bubble);

    /// <summary>Raised by Maybe later; record the snooze here. The prompt then shows the reminder. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> DismissedEvent =
        RoutedEvent.Register<UpgradePrompt, RoutedEventArgs>(nameof(Dismissed), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised by Continue on the success state, usually to close the prompt and retry the blocked action. Set
    /// <see cref="RoutedEventArgs.Handled"/> to keep the success state; left unhandled, the prompt resets to the offer. Bubbles.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> ContinueRequestedEvent =
        RoutedEvent.Register<UpgradePrompt, RoutedEventArgs>(nameof(ContinueRequested), RoutingStrategies.Bubble);

    private UpgradePromptState _state;
    private string? _errorMessage;

    /// <inheritdoc cref="UsedProperty"/>
    public double Used { get => GetValue(UsedProperty); set => SetValue(UsedProperty, value); }

    /// <inheritdoc cref="LimitProperty"/>
    public double Limit { get => GetValue(LimitProperty); set => SetValue(LimitProperty, value); }

    /// <inheritdoc cref="UnitProperty"/>
    public string Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    /// <inheritdoc cref="ResetsOnProperty"/>
    public string ResetsOn { get => GetValue(ResetsOnProperty); set => SetValue(ResetsOnProperty, value); }

    /// <inheritdoc cref="PlanProperty"/>
    public UpgradePromptPlan? Plan { get => GetValue(PlanProperty); set => SetValue(PlanProperty, value); }

    /// <inheritdoc cref="IntervalProperty"/>
    public UpgradePromptInterval Interval { get => GetValue(IntervalProperty); set => SetValue(IntervalProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="UpgradeHandlerProperty"/>
    public Func<UpgradePromptSelection, CancellationToken, Task>? UpgradeHandler
    {
        get => GetValue(UpgradeHandlerProperty);
        set => SetValue(UpgradeHandlerProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public UpgradePromptLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="StateProperty"/>
    public UpgradePromptState State { get => _state; private set => SetAndRaise(StateProperty, ref _state, value); }

    /// <inheritdoc cref="ErrorMessageProperty"/>
    public string? ErrorMessage { get => _errorMessage; private set => SetAndRaise(ErrorMessageProperty, ref _errorMessage, value); }

    /// <inheritdoc cref="IntervalChangedEvent"/>
    public event EventHandler<UpgradePromptIntervalChangedEventArgs>? IntervalChanged
    {
        add => AddHandler(IntervalChangedEvent, value);
        remove => RemoveHandler(IntervalChangedEvent, value);
    }

    /// <inheritdoc cref="UpgradedEvent"/>
    public event EventHandler<UpgradePromptUpgradedEventArgs>? Upgraded
    {
        add => AddHandler(UpgradedEvent, value);
        remove => RemoveHandler(UpgradedEvent, value);
    }

    /// <inheritdoc cref="UpgradeFailedEvent"/>
    public event EventHandler<UpgradePromptFailedEventArgs>? UpgradeFailed
    {
        add => AddHandler(UpgradeFailedEvent, value);
        remove => RemoveHandler(UpgradeFailedEvent, value);
    }

    /// <inheritdoc cref="DismissedEvent"/>
    public event EventHandler<RoutedEventArgs>? Dismissed
    {
        add => AddHandler(DismissedEvent, value);
        remove => RemoveHandler(DismissedEvent, value);
    }

    /// <inheritdoc cref="ContinueRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? ContinueRequested
    {
        add => AddHandler(ContinueRequestedEvent, value);
        remove => RemoveHandler(ContinueRequestedEvent, value);
    }

    /// <summary>The stand-in while there is no plan: no name, no prices, no unlocks (never shown as an offer).</summary>
    private static readonly UpgradePromptPlan s_noPlan = new() { Features = [], Limits = [] };

    /// <summary>True while there is no <see cref="Plan"/>: the card shows its empty state.</summary>
    internal bool IsEmpty => Plan is null;

    /// <summary>The plan on screen: <see cref="Plan"/>, or an empty stand-in.</summary>
    internal UpgradePromptPlan ShownPlan => Plan ?? s_noPlan;

    /// <summary>What Upgrade would start now: the plan, the interval, today's charge and the currency.</summary>
    public UpgradePromptSelection CurrentSelection
    {
        get
        {
            var plan = ShownPlan;
            return new UpgradePromptSelection(plan, Interval, UpgradePromptFormat.Charge(plan, Interval), plan.Currency);
        }
    }

    /// <summary>How long the built-in simulation takes when there is no <see cref="UpgradeHandler"/> (tests shorten it).</summary>
    internal TimeSpan SimulatedDuration { get; set; } = TimeSpan.FromMilliseconds(1100);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Teardown();
        var root = e.NameScope.Find<Panel>(RootPart);
        if (root is not null)
        {
            Build(root);
            ApplyBreakpoint(Bounds.Width);
            Refresh(animate: false);
            WatchViewport();
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IntervalProperty)
        {
            Refresh(animate: true);
        }
        else if (change.Property == UsedProperty || change.Property == LimitProperty || change.Property == UnitProperty
                 || change.Property == ResetsOnProperty || change.Property == PlanProperty || change.Property == TitleProperty
                 || change.Property == DescriptionProperty)
        {
            Refresh(animate: true);
        }
        else if (change.Property == LabelsProperty)
        {
            Refresh(animate: false);
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyBreakpoint(e.NewSize.Width);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        WatchViewport();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _viewport?.Dispose();
        _viewport = null;
        AbortUpgrade();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new UpgradePromptAutomationPeer(this);

    private sealed class UpgradePromptAutomationPeer(UpgradePrompt owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => AutomationProperties.GetName(Owner) is { Length: > 0 } name ? name : base.GetNameCore();
    }
}
