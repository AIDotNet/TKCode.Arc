using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// One customer account's health on one card: the health score with its band and its trend over the last weeks, the
/// renewal date and the days left, open tickets, a usage chart with a metric switch, the risks, and recommended actions
/// you can tick off to see the score they are expected to win back.
/// <para>
/// With no health history the block shows a built-in invented account. Give it <see cref="HealthHistory"/> (weekly scores,
/// oldest first) and the other collections to show your own; <see cref="ActionToggled"/> reports every ticked action.
/// </para>
/// </summary>
public partial class AccountHealth : TemplatedControl
{
    /// <summary>The account's name; null shows the sample name while the samples are shown.</summary>
    public static readonly StyledProperty<string?> AccountNameProperty =
        AvaloniaProperty.Register<AccountHealth, string?>(nameof(AccountName));

    /// <summary>The line under the name (plan, yearly value, owner); null leaves it out.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<AccountHealth, string?>(nameof(Subtitle));

    /// <summary>When the contract renews; null shows "No date".</summary>
    public static readonly StyledProperty<DateTime?> RenewalDateProperty =
        AvaloniaProperty.Register<AccountHealth, DateTime?>(nameof(RenewalDate));

    /// <summary>The day the days left count from; null uses today.</summary>
    public static readonly StyledProperty<DateTime?> TodayProperty =
        AvaloniaProperty.Register<AccountHealth, DateTime?>(nameof(Today));

    /// <summary>Within this many days of the renewal the renewal tile turns to the warning color (60).</summary>
    public static readonly StyledProperty<int> RenewalWarningDaysProperty =
        AvaloniaProperty.Register<AccountHealth, int>(nameof(RenewalWarningDays), 60);

    /// <summary>The index of the usage metric the chart shows (two-way).</summary>
    public static readonly StyledProperty<int> SelectedMetricProperty =
        AvaloniaProperty.Register<AccountHealth, int>(nameof(SelectedMetric), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Draws a bordered card (true); false lays the block flat on the page.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<AccountHealth, bool>(nameof(IsFramed), true);

    /// <summary>The words the block shows and reads out.</summary>
    public static readonly StyledProperty<AccountHealthLabels> LabelsProperty =
        AvaloniaProperty.Register<AccountHealth, AccountHealthLabels>(nameof(Labels), new AccountHealthLabels());

    /// <summary>The health score now: the last value of the history, 0 to 100.</summary>
    public static readonly DirectProperty<AccountHealth, int> ScoreProperty =
        AvaloniaProperty.RegisterDirect<AccountHealth, int>(nameof(Score), o => o.Score);

    /// <summary>The score expected once every done action has had its effect (never above 100).</summary>
    public static readonly DirectProperty<AccountHealth, int> ProjectedScoreProperty =
        AvaloniaProperty.RegisterDirect<AccountHealth, int>(nameof(ProjectedScore), o => o.ProjectedScore);

    /// <summary>Days until the renewal (negative once it has passed); null without a renewal date.</summary>
    public static readonly DirectProperty<AccountHealth, int?> DaysToRenewalProperty =
        AvaloniaProperty.RegisterDirect<AccountHealth, int?>(nameof(DaysToRenewal), o => o.DaysToRenewal);

    /// <summary>Raised when a recommended action is ticked or cleared in the block.</summary>
    public static readonly RoutedEvent<AccountHealthActionToggledEventArgs> ActionToggledEvent =
        RoutedEvent.Register<AccountHealth, AccountHealthActionToggledEventArgs>(nameof(ActionToggled), RoutingStrategies.Bubble);

    private int _score;
    private int _projectedScore;
    private int? _daysToRenewal;

    /// <summary>The health score week by week, oldest first, each 0 to 100. Empty: the sample account is shown.</summary>
    public AvaloniaList<double> HealthHistory { get; } = new();

    /// <summary>The usage metrics the chart can switch between.</summary>
    public AvaloniaList<AccountHealthUsageMetric> Usage { get; } = new();

    /// <summary>The account's support tickets (solved ones are listed last).</summary>
    public AvaloniaList<AccountHealthTicket> Tickets { get; } = new();

    /// <summary>The risks to the renewal.</summary>
    public AvaloniaList<AccountHealthRisk> Risks { get; } = new();

    /// <summary>The recommended actions.</summary>
    public AvaloniaList<AccountHealthAction> Actions { get; } = new();

    /// <inheritdoc cref="AccountNameProperty"/>
    public string? AccountName { get => GetValue(AccountNameProperty); set => SetValue(AccountNameProperty, value); }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <inheritdoc cref="RenewalDateProperty"/>
    public DateTime? RenewalDate { get => GetValue(RenewalDateProperty); set => SetValue(RenewalDateProperty, value); }

    /// <inheritdoc cref="TodayProperty"/>
    public DateTime? Today { get => GetValue(TodayProperty); set => SetValue(TodayProperty, value); }

    /// <inheritdoc cref="RenewalWarningDaysProperty"/>
    public int RenewalWarningDays { get => GetValue(RenewalWarningDaysProperty); set => SetValue(RenewalWarningDaysProperty, value); }

    /// <inheritdoc cref="SelectedMetricProperty"/>
    public int SelectedMetric { get => GetValue(SelectedMetricProperty); set => SetValue(SelectedMetricProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AccountHealthLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="ScoreProperty"/>
    public int Score { get => _score; private set => SetAndRaise(ScoreProperty, ref _score, value); }

    /// <inheritdoc cref="ProjectedScoreProperty"/>
    public int ProjectedScore { get => _projectedScore; private set => SetAndRaise(ProjectedScoreProperty, ref _projectedScore, value); }

    /// <inheritdoc cref="DaysToRenewalProperty"/>
    public int? DaysToRenewal { get => _daysToRenewal; private set => SetAndRaise(DaysToRenewalProperty, ref _daysToRenewal, value); }

    /// <inheritdoc cref="ActionToggledEvent"/>
    public event EventHandler<AccountHealthActionToggledEventArgs>? ActionToggled
    {
        add => AddHandler(ActionToggledEvent, value);
        remove => RemoveHandler(ActionToggledEvent, value);
    }
}
