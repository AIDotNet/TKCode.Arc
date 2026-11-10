using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A team's goals and key results for a period: each key result shows its value against the target, a progress bar with
/// a mark where it should be by today, the owner's confidence and the last check-in, and opens an inline check-in (a new
/// value, a confidence and a note). A timeline view draws every key result's check-ins over the period against the
/// straight line to its target. A team filter and confidence counts sit on top.
/// <para>
/// With no goals the block shows built-in invented goals for a quarter. Give it <see cref="Goals"/>,
/// <see cref="PeriodStart"/> and <see cref="PeriodEnd"/> to show your own; <see cref="CheckedIn"/> reports each check-in.
/// </para>
/// </summary>
public partial class GoalTracker : TemplatedControl
{
    /// <summary>The heading; null shows the sample heading while the samples are shown.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<GoalTracker, string?>(nameof(Title));

    /// <summary>The first day of the period; null uses the start of the current quarter.</summary>
    public static readonly StyledProperty<DateTime?> PeriodStartProperty =
        AvaloniaProperty.Register<GoalTracker, DateTime?>(nameof(PeriodStart));

    /// <summary>The last day of the period; null uses the end of the current quarter.</summary>
    public static readonly StyledProperty<DateTime?> PeriodEndProperty =
        AvaloniaProperty.Register<GoalTracker, DateTime?>(nameof(PeriodEnd));

    /// <summary>The day progress is expected at and check-ins are dated; null uses today.</summary>
    public static readonly StyledProperty<DateTime?> TodayProperty =
        AvaloniaProperty.Register<GoalTracker, DateTime?>(nameof(Today));

    /// <summary>The team the goals are narrowed to (two-way); null shows every team.</summary>
    public static readonly StyledProperty<string?> TeamFilterProperty =
        AvaloniaProperty.Register<GoalTracker, string?>(nameof(TeamFilter), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Which view is shown (two-way).</summary>
    public static readonly StyledProperty<GoalTrackerView> ViewProperty =
        AvaloniaProperty.Register<GoalTracker, GoalTrackerView>(nameof(View), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The name check-ins made in the block are signed with; null leaves the author out.</summary>
    public static readonly StyledProperty<string?> CurrentUserProperty =
        AvaloniaProperty.Register<GoalTracker, string?>(nameof(CurrentUser));

    /// <summary>Draws a bordered card (true); false lays the block flat on the page.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<GoalTracker, bool>(nameof(IsFramed), true);

    /// <summary>The words the block shows and reads out.</summary>
    public static readonly StyledProperty<GoalTrackerLabels> LabelsProperty =
        AvaloniaProperty.Register<GoalTracker, GoalTrackerLabels>(nameof(Labels), new GoalTrackerLabels());

    /// <summary>Raised after a check-in was saved in the block (or with <see cref="CheckIn"/>).</summary>
    public static readonly RoutedEvent<GoalTrackerCheckedInEventArgs> CheckedInEvent =
        RoutedEvent.Register<GoalTracker, GoalTrackerCheckedInEventArgs>(nameof(CheckedIn), RoutingStrategies.Bubble);

    /// <summary>The goals. Empty: the built-in sample goals are shown.</summary>
    public AvaloniaList<GoalTrackerGoal> Goals { get; } = new();

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="PeriodStartProperty"/>
    public DateTime? PeriodStart { get => GetValue(PeriodStartProperty); set => SetValue(PeriodStartProperty, value); }

    /// <inheritdoc cref="PeriodEndProperty"/>
    public DateTime? PeriodEnd { get => GetValue(PeriodEndProperty); set => SetValue(PeriodEndProperty, value); }

    /// <inheritdoc cref="TodayProperty"/>
    public DateTime? Today { get => GetValue(TodayProperty); set => SetValue(TodayProperty, value); }

    /// <inheritdoc cref="TeamFilterProperty"/>
    public string? TeamFilter { get => GetValue(TeamFilterProperty); set => SetValue(TeamFilterProperty, value); }

    /// <inheritdoc cref="ViewProperty"/>
    public GoalTrackerView View { get => GetValue(ViewProperty); set => SetValue(ViewProperty, value); }

    /// <inheritdoc cref="CurrentUserProperty"/>
    public string? CurrentUser { get => GetValue(CurrentUserProperty); set => SetValue(CurrentUserProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public GoalTrackerLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="CheckedInEvent"/>
    public event EventHandler<GoalTrackerCheckedInEventArgs>? CheckedIn
    {
        add => AddHandler(CheckedInEvent, value);
        remove => RemoveHandler(CheckedInEvent, value);
    }
}
