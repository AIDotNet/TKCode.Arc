using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A live charging session card for an EV or any device (Arc Charge session, design reference). Energy packets travel from
/// a brushed-metal charger along the cable and through the pack to the fill front, where each one lands and brightens it.
/// The pack fills toward a draggable charge limit, the charge curve tapers near full with the live point riding on it, and
/// power, energy added, time to the limit and cost update every frame. Stop charging drains the flow: the power falls
/// away, the last packets land and the point drops to the baseline; Resume ramps it back up; at the limit the session
/// completes and Start new session returns to <see cref="StartCharge"/>.
/// <para>
/// The card runs its own simulation (one second on screen is forty seconds of charging at <see cref="Speed"/> 1): set the
/// battery and price figures, read <see cref="State"/>, <see cref="Charge"/> and the other figures, and handle
/// <see cref="StateChanged"/> and <see cref="LimitChanged"/> to mirror the session in your backend. Drag on the pack or the
/// curve, or use the keys on the limit marker, to move <see cref="Limit"/>.
/// </para>
/// </summary>
[TemplatePart(CardPart, typeof(Border))]
[TemplatePart(HeroPart, typeof(ChargeSessionHero))]
[TemplatePart(ScenePart, typeof(ChargeSessionScene))]
[TemplatePart(HandlePart, typeof(ChargeSessionLimitHandle))]
[TemplatePart(PlotPart, typeof(ChargeSessionPlot))]
[TemplatePart(ActionPart, typeof(ChargeSessionActionButton))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(ChargingPseudoClass, StoppedPseudoClass, CompletePseudoClass, StackedPseudoClass, CompactPseudoClass)]
public sealed partial class ChargeSession : TemplatedControl
{
    internal const string CardPart = "PART_Card";
    internal const string HeroPart = "PART_Hero";
    internal const string ScenePart = "PART_Scene";
    internal const string HandlePart = "PART_Handle";
    internal const string PlotPart = "PART_Plot";
    internal const string ActionPart = "PART_Action";
    internal const string AnnouncerPart = "PART_Announcer";
    internal const string ChargingPseudoClass = ":charging";
    internal const string StoppedPseudoClass = ":stopped";
    internal const string CompletePseudoClass = ":complete";
    internal const string StackedPseudoClass = ":stacked";
    internal const string CompactPseudoClass = ":compact";

    /// <summary>The card is stacked (curve over the stats) below this width.</summary>
    internal const double SideBySideWidth = 640;

    /// <summary>The card is compact (tighter padding, smaller type, full-width button) at or below this width.</summary>
    internal const double CompactWidth = 420;

    /// <summary>The charger or device name, shown as the card's heading.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ChargeSession, string?>(nameof(Title), "Bay 6 rapid point");

    /// <summary>The line under the title; null or empty hides it.</summary>
    public static readonly StyledProperty<string?> LocationProperty =
        AvaloniaProperty.Register<ChargeSession, string?>(nameof(Location), "Lantern Quay depot, 180 kW");

    /// <summary>The card's accessible name; null uses the title.</summary>
    public static readonly StyledProperty<string?> AccessibleLabelProperty =
        AvaloniaProperty.Register<ChargeSession, string?>(nameof(AccessibleLabel));

    /// <summary>Usable battery capacity in kWh: how fast the pack fills and what each percent is worth.</summary>
    public static readonly StyledProperty<double> CapacityProperty =
        AvaloniaProperty.Register<ChargeSession, double>(nameof(Capacity), 82, coerce: static (_, v) => double.IsFinite(v) ? Math.Max(0.001, v) : 82);

    /// <summary>Peak charging power in kW, reached on the flat part of the curve.</summary>
    public static readonly StyledProperty<double> MaxPowerProperty =
        AvaloniaProperty.Register<ChargeSession, double>(nameof(MaxPower), 180, coerce: static (_, v) => double.IsFinite(v) ? Math.Max(0, v) : 180);

    /// <summary>State of charge in percent when a session starts, and where Start new session returns to.</summary>
    public static readonly StyledProperty<double> StartChargeProperty =
        AvaloniaProperty.Register<ChargeSession, double>(nameof(StartCharge), 28, coerce: static (_, v) => double.IsFinite(v) ? Math.Clamp(v, 0, 100) : 28);

    /// <summary>
    /// The charge limit in whole percent, between <see cref="MinLimit"/> and 100. Two-way: drags and keys change it, and
    /// setting it moves the marker (and completes or resumes the session like a drag would).
    /// </summary>
    public static readonly StyledProperty<double> LimitProperty =
        AvaloniaProperty.Register<ChargeSession, double>(nameof(Limit), 80, defaultBindingMode: BindingMode.TwoWay,
            coerce: static (o, v) => ((ChargeSession)o).CoerceLimit(v));

    /// <summary>The lowest limit the marker can be set to, in percent.</summary>
    public static readonly StyledProperty<double> MinLimitProperty =
        AvaloniaProperty.Register<ChargeSession, double>(nameof(MinLimit), 50, coerce: static (_, v) => double.IsFinite(v) ? Math.Clamp(v, 0, 100) : 50);

    /// <summary>State of charge in percent where the power starts to taper toward full.</summary>
    public static readonly StyledProperty<double> TaperAtProperty =
        AvaloniaProperty.Register<ChargeSession, double>(nameof(TaperAt), 55, coerce: static (_, v) => double.IsFinite(v) ? Math.Clamp(v, 10, 100) : 55);

    /// <summary>Price per kWh in the session currency.</summary>
    public static readonly StyledProperty<decimal> RateProperty =
        AvaloniaProperty.Register<ChargeSession, decimal>(nameof(Rate), 0.39m);

    /// <summary>ISO 4217 code of the cost's currency.</summary>
    public static readonly StyledProperty<string> CurrencyProperty =
        AvaloniaProperty.Register<ChargeSession, string>(nameof(Currency), "USD");

    /// <summary>Locale name used to format the cost and the numbers ("en-US", "de-DE"); empty uses the invariant culture.</summary>
    public static readonly StyledProperty<string> LocaleProperty =
        AvaloniaProperty.Register<ChargeSession, string>(nameof(Locale), "en-US");

    /// <summary>
    /// Multiplies simulated time: at 1, one second on screen is forty seconds of charging. 0 holds the session still while
    /// the packets keep flowing.
    /// </summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<ChargeSession, double>(nameof(Speed), 1, coerce: static (_, v) => double.IsFinite(v) ? Math.Max(0, v) : 1);

    /// <summary>Holds the session and the flow on their current frame. The limit and the button still work.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<ChargeSession, bool>(nameof(IsPaused));

    /// <summary>Color of the energy, the fill, the LED, the travelled curve and the live point; null uses the accent.</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<ChargeSession, IBrush?>(nameof(AccentBrush));

    /// <summary>
    /// A custom charge curve: state of charge (0..1) to the share of <see cref="MaxPower"/> the pack takes (0..1). Null uses
    /// the built-in curve (62% at empty, full from 10% to <see cref="TaperAt"/>, then an exponential taper).
    /// </summary>
    public static readonly StyledProperty<Func<double, double>?> PowerCurveProperty =
        AvaloniaProperty.Register<ChargeSession, Func<double, double>?>(nameof(PowerCurve));

    /// <summary>The words and number formats; replace to translate or reword.</summary>
    public static readonly StyledProperty<ChargeSessionLabels> LabelsProperty =
        AvaloniaProperty.Register<ChargeSession, ChargeSessionLabels>(nameof(Labels), new ChargeSessionLabels());

    /// <summary>The clock for the 350 ms wait before a limit change counts as settled (the system clock; tests pass their own).</summary>
    public static readonly StyledProperty<TimeProvider> TimeProviderProperty =
        AvaloniaProperty.Register<ChargeSession, TimeProvider>(nameof(TimeProvider), TimeProvider.System);

    /// <summary>Charging, stopped or complete.</summary>
    public static readonly DirectProperty<ChargeSession, ChargeSessionState> StateProperty =
        AvaloniaProperty.RegisterDirect<ChargeSession, ChargeSessionState>(nameof(State), o => o._state);

    /// <summary>State of charge in percent (to a tenth).</summary>
    public static readonly DirectProperty<ChargeSession, double> ChargeProperty =
        AvaloniaProperty.RegisterDirect<ChargeSession, double>(nameof(Charge), o => o._charge);

    /// <summary>Charging power in kW now (whole kW), falling to 0 as a stop drains.</summary>
    public static readonly DirectProperty<ChargeSession, double> PowerProperty =
        AvaloniaProperty.RegisterDirect<ChargeSession, double>(nameof(Power), o => o._power);

    /// <summary>Energy added this session in kWh (to a tenth).</summary>
    public static readonly DirectProperty<ChargeSession, double> EnergyAddedProperty =
        AvaloniaProperty.RegisterDirect<ChargeSession, double>(nameof(EnergyAdded), o => o._energy);

    /// <summary>Cost of the energy added, rounded to the currency's digits.</summary>
    public static readonly DirectProperty<ChargeSession, decimal> CostProperty =
        AvaloniaProperty.RegisterDirect<ChargeSession, decimal>(nameof(Cost), o => o._cost);

    /// <summary>
    /// Minutes of charging at full power to the limit (whole minutes; 0 under a minute); null while stopped or once the
    /// limit is reached.
    /// </summary>
    public static readonly DirectProperty<ChargeSession, double?> MinutesToLimitProperty =
        AvaloniaProperty.RegisterDirect<ChargeSession, double?>(nameof(MinutesToLimit), o => o._minutes);

    /// <summary>Raised when charging stops, resumes, reaches the limit or a new session starts.</summary>
    public static readonly RoutedEvent<ChargeSessionStateChangedEventArgs> StateChangedEvent =
        RoutedEvent.Register<ChargeSession, ChargeSessionStateChangedEventArgs>(nameof(StateChanged), RoutingStrategies.Bubble);

    /// <summary>Raised with the new limit when a drag or key press settles (350 ms after the last change).</summary>
    public static readonly RoutedEvent<ChargeSessionLimitChangedEventArgs> LimitChangedEvent =
        RoutedEvent.Register<ChargeSession, ChargeSessionLimitChangedEventArgs>(nameof(LimitChanged), RoutingStrategies.Bubble);

    /// <summary>The accent the parts draw with: the token, or <see cref="AccentBrush"/> as a local value.</summary>
    internal static readonly StyledProperty<IBrush?> AccentInkProperty =
        AvaloniaProperty.Register<ChargeSession, IBrush?>(nameof(AccentInk));

    /// <summary>The muted ink of the status icon when not charging.</summary>
    internal static readonly StyledProperty<IBrush?> MutedInkProperty =
        AvaloniaProperty.Register<ChargeSession, IBrush?>(nameof(MutedInk));

    private ChargeSessionState _state;
    private double _charge = 28;
    private double _power;
    private double _energy;
    private decimal _cost;
    private double? _minutes;

    static ChargeSession()
    {
        SpecialFocusModality.Watch();
        FocusableProperty.OverrideDefaultValue<ChargeSession>(false);
        IsTabStopProperty.OverrideDefaultValue<ChargeSession>(false);
        HorizontalAlignmentProperty.OverrideDefaultValue<ChargeSession>(HorizontalAlignment.Stretch);
        VerticalAlignmentProperty.OverrideDefaultValue<ChargeSession>(VerticalAlignment.Top);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="LocationProperty"/>
    public string? Location { get => GetValue(LocationProperty); set => SetValue(LocationProperty, value); }

    /// <inheritdoc cref="AccessibleLabelProperty"/>
    public string? AccessibleLabel { get => GetValue(AccessibleLabelProperty); set => SetValue(AccessibleLabelProperty, value); }

    /// <inheritdoc cref="CapacityProperty"/>
    public double Capacity { get => GetValue(CapacityProperty); set => SetValue(CapacityProperty, value); }

    /// <inheritdoc cref="MaxPowerProperty"/>
    public double MaxPower { get => GetValue(MaxPowerProperty); set => SetValue(MaxPowerProperty, value); }

    /// <inheritdoc cref="StartChargeProperty"/>
    public double StartCharge { get => GetValue(StartChargeProperty); set => SetValue(StartChargeProperty, value); }

    /// <inheritdoc cref="LimitProperty"/>
    public double Limit { get => GetValue(LimitProperty); set => SetValue(LimitProperty, value); }

    /// <inheritdoc cref="MinLimitProperty"/>
    public double MinLimit { get => GetValue(MinLimitProperty); set => SetValue(MinLimitProperty, value); }

    /// <inheritdoc cref="TaperAtProperty"/>
    public double TaperAt { get => GetValue(TaperAtProperty); set => SetValue(TaperAtProperty, value); }

    /// <inheritdoc cref="RateProperty"/>
    public decimal Rate { get => GetValue(RateProperty); set => SetValue(RateProperty, value); }

    /// <inheritdoc cref="CurrencyProperty"/>
    public string Currency { get => GetValue(CurrencyProperty); set => SetValue(CurrencyProperty, value); }

    /// <inheritdoc cref="LocaleProperty"/>
    public string Locale { get => GetValue(LocaleProperty); set => SetValue(LocaleProperty, value); }

    /// <inheritdoc cref="SpeedProperty"/>
    public double Speed { get => GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }

    /// <inheritdoc cref="IsPausedProperty"/>
    public bool IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }

    /// <inheritdoc cref="AccentBrushProperty"/>
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    /// <inheritdoc cref="PowerCurveProperty"/>
    public Func<double, double>? PowerCurve { get => GetValue(PowerCurveProperty); set => SetValue(PowerCurveProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ChargeSessionLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="TimeProviderProperty"/>
    public TimeProvider TimeProvider { get => GetValue(TimeProviderProperty); set => SetValue(TimeProviderProperty, value); }

    /// <inheritdoc cref="StateProperty"/>
    public ChargeSessionState State { get => _state; private set => SetAndRaise(StateProperty, ref _state, value); }

    /// <inheritdoc cref="ChargeProperty"/>
    public double Charge { get => _charge; private set => SetAndRaise(ChargeProperty, ref _charge, value); }

    /// <inheritdoc cref="PowerProperty"/>
    public double Power { get => _power; private set => SetAndRaise(PowerProperty, ref _power, value); }

    /// <inheritdoc cref="EnergyAddedProperty"/>
    public double EnergyAdded { get => _energy; private set => SetAndRaise(EnergyAddedProperty, ref _energy, value); }

    /// <inheritdoc cref="CostProperty"/>
    public decimal Cost { get => _cost; private set => SetAndRaise(CostProperty, ref _cost, value); }

    /// <inheritdoc cref="MinutesToLimitProperty"/>
    public double? MinutesToLimit { get => _minutes; private set => SetAndRaise(MinutesToLimitProperty, ref _minutes, value); }

    /// <inheritdoc cref="StateChangedEvent"/>
    public event EventHandler<ChargeSessionStateChangedEventArgs>? StateChanged
    {
        add => AddHandler(StateChangedEvent, value);
        remove => RemoveHandler(StateChangedEvent, value);
    }

    /// <inheritdoc cref="LimitChangedEvent"/>
    public event EventHandler<ChargeSessionLimitChangedEventArgs>? LimitChanged
    {
        add => AddHandler(LimitChangedEvent, value);
        remove => RemoveHandler(LimitChangedEvent, value);
    }

    internal IBrush? AccentInk { get => GetValue(AccentInkProperty); set => SetValue(AccentInkProperty, value); }

    internal IBrush? MutedInk { get => GetValue(MutedInkProperty); set => SetValue(MutedInkProperty, value); }
}
