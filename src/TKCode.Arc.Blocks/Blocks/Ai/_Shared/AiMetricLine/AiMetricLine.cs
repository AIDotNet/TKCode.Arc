using System.Globalization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The footer line of an arena pane (Arc model arena): "First token 1.83s · 152 tok/s · $0.0041", 12 / 16.8 in the muted
/// text color with the values in Foreground, the items 8px apart (2px between wrapped rows), each later item led by a muted
/// "·" 8px before its value. The time is plain text the owner updates (every 100ms while <see cref="IsTiming"/>; the part
/// runs no clock) and turns TextSecondary while the timer runs, changing color over 240ms. The speed and the cost roll
/// digit by digit (Core <see cref="RollingDigits"/>, cut at the line box); the speed keeps a cell three digits wide so its
/// unit does not move, the cost has four decimals. A missing value shows <see cref="Placeholder"/>. The speed and the cost
/// have no visible label; screen readers hear the whole line with <see cref="SpeedName"/> and <see cref="CostName"/>
/// ("First token 1.83s, Speed 152 tok/s, Cost $0.0041").
/// </summary>
[TemplatePart(SpeedPart, typeof(RollingDigits))]
[TemplatePart(CostPart, typeof(RollingDigits))]
[PseudoClasses(TimingPseudoClass)]
internal sealed class AiMetricLine : TemplatedControl
{
    public const string SpeedPart = "PART_Speed";
    public const string CostPart = "PART_Cost";

    /// <summary>Set while the first-token timer runs.</summary>
    public const string TimingPseudoClass = ":timing";

    /// <summary>The cost's decimals.</summary>
    internal const int CostDecimals = 4;

    private static readonly CultureInfo s_format = CultureInfo.GetCultureInfo("en-US");

    /// <summary>From the start of the run to the first text; while timing, the time so far. Null shows the placeholder.</summary>
    public static readonly StyledProperty<TimeSpan?> FirstTokenProperty =
        AvaloniaProperty.Register<AiMetricLine, TimeSpan?>(nameof(FirstToken));

    /// <summary>Whether the first-token timer runs (the owner updates <see cref="FirstToken"/>): the time shows in TextSecondary.</summary>
    public static readonly StyledProperty<bool> IsTimingProperty =
        AvaloniaProperty.Register<AiMetricLine, bool>(nameof(IsTiming));

    /// <summary>Output tokens per second; null (or not a number) shows the placeholder.</summary>
    public static readonly StyledProperty<double?> TokensPerSecondProperty =
        AvaloniaProperty.Register<AiMetricLine, double?>(nameof(TokensPerSecond));

    /// <summary>What the answer cost so far in US dollars; null shows the placeholder.</summary>
    public static readonly StyledProperty<decimal?> CostUsdProperty =
        AvaloniaProperty.Register<AiMetricLine, decimal?>(nameof(CostUsd));

    /// <summary>The visible label before the time ("First token").</summary>
    public static readonly StyledProperty<string?> FirstTokenTextProperty =
        AvaloniaProperty.Register<AiMetricLine, string?>(nameof(FirstTokenText));

    /// <summary>The unit after the speed ("tok/s").</summary>
    public static readonly StyledProperty<string?> SpeedUnitTextProperty =
        AvaloniaProperty.Register<AiMetricLine, string?>(nameof(SpeedUnitText));

    /// <summary>The speed's name for screen readers ("Speed"); it has no visible label.</summary>
    public static readonly StyledProperty<string?> SpeedNameProperty =
        AvaloniaProperty.Register<AiMetricLine, string?>(nameof(SpeedName));

    /// <summary>The cost's name for screen readers ("Cost"); it has no visible label.</summary>
    public static readonly StyledProperty<string?> CostNameProperty =
        AvaloniaProperty.Register<AiMetricLine, string?>(nameof(CostName));

    /// <summary>What a missing value shows ("–").</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<AiMetricLine, string?>(nameof(Placeholder));

    /// <summary>The time as shown (template use).</summary>
    internal static readonly DirectProperty<AiMetricLine, string?> FirstTokenDisplayProperty =
        AvaloniaProperty.RegisterDirect<AiMetricLine, string?>(nameof(FirstTokenDisplay), o => o.FirstTokenDisplay);

    /// <summary>Whether the speed has a value (template use).</summary>
    internal static readonly DirectProperty<AiMetricLine, bool> HasSpeedProperty =
        AvaloniaProperty.RegisterDirect<AiMetricLine, bool>(nameof(HasSpeed), o => o.HasSpeed);

    /// <summary>Whether the cost has a value (template use).</summary>
    internal static readonly DirectProperty<AiMetricLine, bool> HasCostProperty =
        AvaloniaProperty.RegisterDirect<AiMetricLine, bool>(nameof(HasCost), o => o.HasCost);

    private RollingDigits? _speed;
    private RollingDigits? _cost;
    private string? _firstTokenDisplay;
    private bool _hasSpeed;
    private bool _hasCost;

    static AiMetricLine()
    {
        FocusableProperty.OverrideDefaultValue<AiMetricLine>(false);
        IsTabStopProperty.OverrideDefaultValue<AiMetricLine>(false);
    }

    public AiMetricLine()
    {
        Refresh();
    }

    /// <inheritdoc cref="FirstTokenProperty"/>
    public TimeSpan? FirstToken { get => GetValue(FirstTokenProperty); set => SetValue(FirstTokenProperty, value); }

    /// <inheritdoc cref="IsTimingProperty"/>
    public bool IsTiming { get => GetValue(IsTimingProperty); set => SetValue(IsTimingProperty, value); }

    /// <inheritdoc cref="TokensPerSecondProperty"/>
    public double? TokensPerSecond { get => GetValue(TokensPerSecondProperty); set => SetValue(TokensPerSecondProperty, value); }

    /// <inheritdoc cref="CostUsdProperty"/>
    public decimal? CostUsd { get => GetValue(CostUsdProperty); set => SetValue(CostUsdProperty, value); }

    /// <inheritdoc cref="FirstTokenTextProperty"/>
    public string? FirstTokenText { get => GetValue(FirstTokenTextProperty); set => SetValue(FirstTokenTextProperty, value); }

    /// <inheritdoc cref="SpeedUnitTextProperty"/>
    public string? SpeedUnitText { get => GetValue(SpeedUnitTextProperty); set => SetValue(SpeedUnitTextProperty, value); }

    /// <inheritdoc cref="SpeedNameProperty"/>
    public string? SpeedName { get => GetValue(SpeedNameProperty); set => SetValue(SpeedNameProperty, value); }

    /// <inheritdoc cref="CostNameProperty"/>
    public string? CostName { get => GetValue(CostNameProperty); set => SetValue(CostNameProperty, value); }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    internal string? FirstTokenDisplay { get => _firstTokenDisplay; private set => SetAndRaise(FirstTokenDisplayProperty, ref _firstTokenDisplay, value); }

    internal bool HasSpeed { get => _hasSpeed; private set => SetAndRaise(HasSpeedProperty, ref _hasSpeed, value); }

    internal bool HasCost { get => _hasCost; private set => SetAndRaise(HasCostProperty, ref _hasCost, value); }

    /// <summary>What the line reads as.</summary>
    internal string SpokenText { get; private set; } = string.Empty;

    /// <summary>The rolling speed (template part).</summary>
    internal RollingDigits? SpeedDigits => _speed;

    /// <summary>The rolling cost (template part).</summary>
    internal RollingDigits? CostDigits => _cost;

    /// <summary>
    /// A time as the line shows it: seconds with two decimals under 10s ("1.83s"), one decimal under 100s ("12.4s"), minutes
    /// and seconds above ("1:42"). Rounding never prints a value of the shorter form's limit ("10.00s" is "10.0s").
    /// </summary>
    internal static string FormatTime(TimeSpan time)
    {
        var seconds = Math.Max(0, time.TotalSeconds);
        var hundredths = Math.Round(seconds, 2, MidpointRounding.AwayFromZero);
        if (hundredths < 10)
        {
            return hundredths.ToString("0.00", CultureInfo.InvariantCulture) + "s";
        }
        var tenths = Math.Round(seconds, 1, MidpointRounding.AwayFromZero);
        if (tenths < 100)
        {
            return tenths.ToString("0.0", CultureInfo.InvariantCulture) + "s";
        }
        var whole = (long)Math.Floor(seconds);
        return (whole / 60).ToString(CultureInfo.InvariantCulture) + ":" + (whole % 60).ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>The speed as a whole number, or null when there is none to show.</summary>
    internal static double? SpeedValue(double? tokensPerSecond) =>
        tokensPerSecond is { } speed && double.IsFinite(speed) && speed >= 0 ? Math.Round(speed, MidpointRounding.AwayFromZero) : null;

    /// <summary>The cost as the line shows it ("$0.0041"), or null.</summary>
    internal static string? FormatCost(decimal? cost) =>
        cost is { } value ? "$" + value.ToString("N" + CostDecimals, s_format) : null;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _speed = e.NameScope.Find<RollingDigits>(SpeedPart);
        _cost = e.NameScope.Find<RollingDigits>(CostPart);
        // A new template shows the values as they are, without rolling.
        HasSpeed = false;
        HasCost = false;
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsTimingProperty)
        {
            PseudoClasses.Set(TimingPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == FirstTokenProperty || change.Property == TokensPerSecondProperty || change.Property == CostUsdProperty
                 || change.Property == FirstTokenTextProperty || change.Property == SpeedUnitTextProperty || change.Property == SpeedNameProperty
                 || change.Property == CostNameProperty || change.Property == PlaceholderProperty)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var placeholder = Placeholder ?? string.Empty;
        var time = FirstToken is { } first ? FormatTime(first) : null;
        FirstTokenDisplay = time ?? placeholder;

        var speed = SpeedValue(TokensPerSecond);
        HasSpeed = Push(_speed, speed, HasSpeed);
        var cost = CostUsd;
        HasCost = Push(_cost, cost is { } c ? (double)c : null, HasCost);

        var spokenSpeed = speed is { } s ? s.ToString("N0", s_format) : placeholder;
        var spoken = Join(FirstTokenText, time ?? placeholder);
        spoken = Join(spoken, Join(SpeedName, Join(spokenSpeed, SpeedUnitText)), ", ");
        spoken = Join(spoken, Join(CostName, FormatCost(cost) ?? placeholder), ", ");
        SpokenText = spoken;
        AutomationProperties.SetName(this, SpokenText);
    }

    /// <summary>
    /// Shows <paramref name="value"/> on <paramref name="digits"/>: a value that follows another one rolls there, the first
    /// value after a placeholder appears as it is. Returns whether there is a value.
    /// </summary>
    private static bool Push(RollingDigits? digits, double? value, bool hadValue)
    {
        if (value is not { } number)
        {
            return false;
        }
        if (digits is not null)
        {
            if (hadValue)
            {
                digits.Value = number;
            }
            else
            {
                digits.IsInstant = true;
                digits.Value = number;
                digits.IsInstant = false;
            }
        }
        return true;
    }

    private static string Join(string? first, string? second, string separator = " ")
    {
        if (string.IsNullOrEmpty(first))
        {
            return second ?? string.Empty;
        }
        return string.IsNullOrEmpty(second) ? first : first + separator + second;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LinePeer(this);

    /// <summary>One line of text named by what it shows; the parts are not read one by one.</summary>
    private sealed class LinePeer(AiMetricLine owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
