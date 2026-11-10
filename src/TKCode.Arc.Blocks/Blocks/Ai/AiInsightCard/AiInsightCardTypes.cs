using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>A stretch of the chart, from one point index to another (both included).</summary>
/// <param name="Start">The first point of the stretch.</param>
/// <param name="End">The last point of the stretch; equal to <paramref name="Start"/> marks a single point.</param>
public sealed record AiInsightRange(int Start, int End);

/// <summary>One likely cause of the change in <see cref="AiInsightCard.Causes"/>.</summary>
public sealed record AiInsightCause
{
    /// <summary>Your identifier of the cause; reported back in the events.</summary>
    public string? Id { get; init; }

    /// <summary>The cause in a few words ("Android 4.2 release").</summary>
    public required string Title { get; init; }

    /// <summary>One sentence of evidence under the title; null leaves it out.</summary>
    public string? Detail { get; init; }

    /// <summary>How much of the change this cause explains, from 0 to 1; drawn as a bar and a percentage.</summary>
    public double Share { get; init; }

    /// <summary>The stretch of the chart the cause belongs to; selecting the cause moves the chart's band there. Null keeps the insight's own stretch.</summary>
    public AiInsightRange? Range { get; init; }
}

/// <summary>What the person said about an insight.</summary>
public enum AiInsightFeedback
{
    /// <summary>No answer yet.</summary>
    None,

    /// <summary>The insight helped (thumbs up).</summary>
    Helpful,

    /// <summary>The insight did not help (thumbs down).</summary>
    NotHelpful
}

/// <summary>Arguments of <see cref="AiInsightCard.ActionInvoked"/>.</summary>
public sealed class AiInsightActionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiInsightActionEventArgs(RoutedEvent routedEvent, string? nextStep, AiInsightCause? selectedCause) : base(routedEvent)
    {
        NextStep = nextStep;
        SelectedCause = selectedCause;
    }

    /// <summary>The suggested step the button carries out.</summary>
    public string? NextStep { get; }

    /// <summary>The cause that was selected when the button was pressed, if any.</summary>
    public AiInsightCause? SelectedCause { get; }
}

/// <summary>Arguments of <see cref="AiInsightCard.FeedbackChanged"/>.</summary>
public sealed class AiInsightFeedbackEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiInsightFeedbackEventArgs(RoutedEvent routedEvent, AiInsightFeedback oldFeedback, AiInsightFeedback newFeedback) : base(routedEvent)
    {
        OldFeedback = oldFeedback;
        NewFeedback = newFeedback;
    }

    /// <summary>The answer before the change.</summary>
    public AiInsightFeedback OldFeedback { get; }

    /// <summary>The answer now (None when the person took it back).</summary>
    public AiInsightFeedback NewFeedback { get; }
}

/// <summary>Arguments of <see cref="AiInsightCard.SelectedCauseChanged"/>.</summary>
public sealed class AiInsightCauseEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiInsightCauseEventArgs(RoutedEvent routedEvent, AiInsightCause? cause) : base(routedEvent) => Cause = cause;

    /// <summary>The cause now selected; null when none is.</summary>
    public AiInsightCause? Cause { get; }
}

/// <summary>
/// Every word an <see cref="AiInsightCard"/> shows or reads out, with English defaults. Words built from values are
/// functions, so an app can apply its own grammar. Assign a new instance to <see cref="AiInsightCard.Labels"/>.
/// </summary>
public sealed record AiInsightCardLabels
{
    /// <summary>The small label at the top of the card ("AI insight").</summary>
    public string Eyebrow { get; init; } = "AI insight";

    /// <summary>The heading over the summary ("What moved").</summary>
    public string SummaryHeading { get; init; } = "What moved";

    /// <summary>The heading over the causes ("Likely causes").</summary>
    public string CausesHeading { get; init; } = "Likely causes";

    /// <summary>The heading of the suggestion ("Suggested next step").</summary>
    public string NextStepHeading { get; init; } = "Suggested next step";

    /// <summary>The suggestion's button ("Take this step").</summary>
    public string Action { get; init; } = "Take this step";

    /// <summary>The line shown while the insight is written ("Looking into the change…").</summary>
    public string Analyzing { get; init; } = "Looking into the change…";

    /// <summary>Accessible name of the thumbs-up button ("Helpful").</summary>
    public string Helpful { get; init; } = "Helpful";

    /// <summary>Accessible name of the thumbs-down button ("Not helpful").</summary>
    public string NotHelpful { get; init; } = "Not helpful";

    /// <summary>Announced after an answer ("Thanks for the feedback").</summary>
    public string FeedbackThanks { get; init; } = "Thanks for the feedback";

    /// <summary>Accessible name of the chart ("Chart of the metric").</summary>
    public string Chart { get; init; } = "Chart of the metric";

    /// <summary>Accessible name of the list of causes.</summary>
    public string CausesList { get; init; } = "Likely causes";

    /// <summary>The change pill from the percent change ("−12.4%", "+3.1%").</summary>
    public Func<double, string> Change { get; init; } =
        static percent => (percent < 0 ? "−" : "+") + Math.Abs(percent).ToString("0.#", System.Globalization.CultureInfo.CurrentCulture) + "%";

    /// <summary>The share of a cause ("62%").</summary>
    public Func<double, string> Share { get; init; } =
        static share => Math.Round(share * 100).ToString("0", System.Globalization.CultureInfo.CurrentCulture) + "%";

    /// <summary>A cause's accessible name from its title and share ("Android release, 62% of the change").</summary>
    public Func<string, string, string> CauseName { get; init; } = static (title, share) => $"{title}, {share} of the change";

    /// <summary>The chart's readout of a point from its label and value ("Nov 4: 17,980").</summary>
    public Func<string, string, string> Point { get; init; } = static (label, value) => $"{label}: {value}";

    /// <summary>The readout with the previous period's value ("Nov 4: 17,980, before 20,110").</summary>
    public Func<string, string, string, string> PointWithComparison { get; init; } =
        static (label, value, before) => $"{label}: {value}, before {before}";

    /// <summary>The chart's description from the first and last labels and values.</summary>
    public Func<string, string, string, string, string> ChartSummary { get; init; } =
        static (fromLabel, fromValue, toLabel, toValue) => $"From {fromValue} on {fromLabel} to {toValue} on {toLabel}";
}

/// <summary>Invented sample data for <see cref="AiInsightCard"/>: its defaults.</summary>
public static class AiInsightCardSamples
{
    /// <summary>The sample metric's name.</summary>
    public const string Title = "Weekly active users";

    /// <summary>The sample metric's current value.</summary>
    public const string Value = "18,420";

    /// <summary>The sample change in percent.</summary>
    public const double Change = -12.4;

    /// <summary>The sample comparison period.</summary>
    public const string Period = "vs. the 14 days before";

    /// <summary>The sample summary.</summary>
    public const string Summary =
        "Active users fell 12.4% over the last five days, almost all of it on Android. The drop starts the day version 4.2 went out and is steepest among people who sign in with a magic link.";

    /// <summary>The sample suggestion.</summary>
    public const string NextStep =
        "Roll Android back to 4.1.3 for magic-link sign-ins and ask the auth team to check the new deep-link handler.";

    /// <summary>The sample footnote.</summary>
    public const string Footnote = "From product events and release notes · updated 4 min ago";

    private static readonly double[] s_points =
        [20940, 21210, 21080, 20870, 21330, 21460, 21190, 21020, 20880, 19960, 19120, 18710, 18530, 18420];

    private static readonly double[] s_before =
        [20480, 20610, 20720, 20590, 20830, 20990, 20870, 20760, 20920, 21040, 20980, 21110, 21060, 21020];

    /// <summary>Fourteen days of the metric, Oct 27 to Nov 9; it falls from day ten.</summary>
    public static IReadOnlyList<double> Points() => s_points;

    /// <summary>The fourteen days before, for the dashed comparison line.</summary>
    public static IReadOnlyList<double> ComparisonPoints() => s_before;

    /// <summary>The days' labels, "Oct 27" to "Nov 9".</summary>
    public static IReadOnlyList<string> PointLabels()
    {
        var labels = new string[s_points.Length];
        var day = new DateOnly(2026, 10, 27);
        for (var i = 0; i < labels.Length; i++)
        {
            labels[i] = day.AddDays(i).ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture);
        }
        return labels;
    }

    /// <summary>The stretch the insight is about: the last five days.</summary>
    public static AiInsightRange Focus() => new(9, 13);

    /// <summary>Three causes that add up to the drop.</summary>
    public static IReadOnlyList<AiInsightCause> Causes() =>
    [
        new()
        {
            Id = "android-release", Title = "Android 4.2 release",
            Detail = "Sign-ins on Android fell 31% from the day it shipped; iOS and web held steady.",
            Share = 0.64, Range = new AiInsightRange(9, 13),
        },
        new()
        {
            Id = "magic-link", Title = "Magic-link sign-in errors",
            Detail = "Link opens that end on the home screen rose from 2% to 19%.",
            Share = 0.27, Range = new AiInsightRange(10, 13),
        },
        new()
        {
            Id = "holiday", Title = "Regional holiday",
            Detail = "Two markets were on a public holiday on Nov 5 and 6.",
            Share = 0.09, Range = new AiInsightRange(9, 10),
        },
    ];
}
