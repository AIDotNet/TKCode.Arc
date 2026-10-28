using System.Globalization;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// The Billing page's data, uiarc.dev's samples: the usage forecast of Northwind Labs for Sep 1 to 30 (read on Sep 22;
/// actual readings to day 22, then uiarc.dev's precomputed median and likely range) and the cancellation flow of Halcyon
/// Studio's Team plan ($96 a month, 8 seats, renews Oct 14) with one offer per reason.
/// </summary>
internal static class DemoBillingData
{
    public static readonly DateOnly PeriodStart = new(2026, 9, 1);

    #region Usage forecast

    public static UsageForecastLabels ForecastLabels() => new()
    {
        PassedHeadlineFormat = "You passed the budget on {0}",
        PassedDetailFormat = "Usage is {0}% over today and heading for {1} by {2}.",
        CrossHeadlineFormat = "At this pace you reach the budget on {0}",
        CrossDetailFormat = "{0} before the period closes, likely between {1} and {2}.",
        CrossDetailPlainFormat = "{0} before the period closes.",
        LastDayDetailFormat = "On the last day of the period, as early as {0}.",
        LastDayPlainText = "On the last day of the period.",
        UnderHeadline = "At this pace you stay under budget",
        UnderDetailFormat = "Heading for {0} by {1}, {2}% of budget.",
        UnderRiskFormat = " A busy week could cross it on {0}.",
        UsedLabel = "Used so far",
        ForecastLabelFormat = "Projected by {0}",
        OverageLabel = "Cost above budget",
        RangeSmallFormat = "Likely {0}–{1}",
        UnderBudgetText = "Within budget",
        NotifyAtText = "Alert at",
        ReachedFormat = "Reached {0}",
        ExpectedFormat = "Expected {0}",
        NotExpectedText = "Not expected",
        LegendMeasuredText = "Actual",
        LegendProjectedText = "Forecast",
        LegendRangeText = "Likely range",
        MeasuredTag = "Actual",
        ProjectedTag = "Forecast",
        PanelRangeFormat = "Likely {0}–{1}",
        DragHintText = "Drag the budget line or use the arrow keys.",
        InspectorName = "Inspect a day"
    };

    /// <summary>API calls, storage and seats. Each series lists day 0 to 22; the forecasts list day 22 to 30.</summary>
    public static UsageMetric[] Metrics() =>
    [
        Metric("calls", "API calls", 0, 50, 10, 1, 38, 1, "M", "", 200, "At $0.20 per 1k calls",
            [0, 1.69, 3.32, 5.04, 6.65, 7.38, 8.12, 9.82, 11.45, 13.11, 14.76, 16.49, 17.25, 18.05, 19.85, 21.58, 23.44, 25.13, 27, 27.74, 28.53, 30.4, 32.3],
            [32.3, 34.148, 36.007, 37.875, 38.696, 39.522, 41.419, 43.326, 45.243],
            [32.3, 33.689, 35.288, 36.929, 37.54, 38.167, 39.872, 41.594, 43.329],
            [32.3, 34.796, 37.022, 39.21, 40.328, 41.435, 43.603, 45.772, 47.945]),
        Metric("storage", "Storage", 2, 4.5, 2.5, 0.1, 4, 2, "TB", " ", 120, "At $0.12 per GB",
            [3.12, 3.151, 3.18, 3.215, 3.247, 3.259, 3.27, 3.308, 3.34, 3.371, 3.254, 3.282, 3.296, 3.307, 3.343, 3.373, 3.398, 3.438, 3.477, 3.488, 3.503, 3.539, 3.574],
            [3.574, 3.608, 3.643, 3.677, 3.69, 3.704, 3.739, 3.774, 3.809],
            [3.574, 3.569, 3.581, 3.597, 3.592, 3.589, 3.607, 3.626, 3.646],
            [3.574, 3.663, 3.729, 3.79, 3.829, 3.866, 3.924, 3.982, 4.038]),
        Metric("seats", "Seats", 140, 240, 160, 5, 200, 0, "seats", " ", 24, "At $24 per seat",
            [186, 186, 187, 189, 190, 190, 190, 191, 193, 194, 195, 195, 195, 196, 197, 198, 199, 201, 202, 202, 202, 203, 205],
            [205, 206.149, 207.298, 208.447, 208.574, 208.702, 209.851, 211, 212.149],
            [205, 205.116, 205.68, 206.319, 205.973, 205.653, 206.371, 207.102, 207.843],
            [205, 207.607, 209.581, 211.451, 212.246, 213.007, 214.765, 216.503, 218.228])
    ];

    private static UsageMetric Metric(string key, string label, double low, double high, double minimum, double step, double budget,
                                      int decimals, string unit, string separator, double price, string note,
                                      double[] actual, double[] median, double[] lower, double[] upper)
    {
        var format = decimals == 0 ? "0" : "0." + new string('0', decimals);
        return new UsageMetric
        {
            Key = key,
            Label = label,
            // Day 0 is where the period starts; the block takes days 1 to 22 as readings and the forecast after today.
            StartValue = actual[0],
            Readings = actual[1..],
            Forecast = median[1..],
            ForecastLow = lower[1..],
            ForecastHigh = upper[1..],
            AxisMinimum = low,
            AxisMaximum = high,
            BudgetMinimum = minimum,
            BudgetMaximum = high,
            BudgetStep = step,
            Budget = budget,
            UnitPrice = price,
            PriceNote = note,
            Unit = unit,
            UnitSeparator = separator,
            ValueFormatter = value => value.ToString(format, CultureInfo.InvariantCulture)
        };
    }

    #endregion

    #region Cancel flow

    public const string CancelEmail = "sofia@halcyon.studio";

    public static CancelFlowLabels CancelLabels { get; } = new()
    {
        ReasonTitle = "Why are you cancelling?",
        ReasonDescription = _ => "Your answer shapes what we suggest next. Nothing changes until you confirm on the last step.",
        FeedbackLabel = "Add a note (optional)",
        ContinueCanceling = "Continue to cancel",
        ConfirmTitle = _ => "Before you cancel",
        AccessUntil = _ => "Your team keeps full access until the end of this billing period. Here is what changes after that.",
        AfterThat = string.Empty,
        HoldToCancel = "Hold to cancel plan",
        HoldConfirmed = "Plan cancelled",
        CanceledTitle = "Your plan ends on Oct 14",
        CanceledDescription = (_, _) => $"Thanks for building with us. A confirmation is on its way to {CancelEmail}.",
        Reactivate = "Reactivate plan"
    };

    /// <summary>uiarc.dev's four reasons, each with the offer it leads to.</summary>
    public static CancelFlowReason[] Reasons() =>
    [
        new("Too expensive", "The cost is hard to justify right now")
        {
            AsksForDetails = true,
            DetailsPrompt = "What would feel like a fair price for your team?",
            Offer = new CancelFlowOffer
            {
                Title = "Stay for 30% less",
                Description = "Keep every project, seat and integration for $67.20 a month through the end of the year. "
                              + "Applies to your Oct, Nov and Dec invoices, saves $86.40 across the three months, and returns to $96 a month on Jan 14, 2027.",
                AcceptLabel = "Apply 30% off",
                AcceptedTitle = "30% off is applied",
                AcceptedDescription = "Your next three invoices drop to $67.20. Nothing else about your workspace changes. The discount ends on Jan 13, 2027."
            }
        },
        new("Not using it enough", "Our work is seasonal or has slowed down")
        {
            AsksForDetails = true,
            DetailsPrompt = "When do you expect to need it again?",
            Offer = new CancelFlowOffer
            {
                Title = "Pause instead of cancelling",
                Description = "Stop billing while things are quiet. Projects, members and integrations stay exactly where they are. "
                              + "A two-month pause skips 2 invoices, saving $192; billing resumes on Dec 14, 2026, and you can resume early any time from Billing.",
                AcceptLabel = "Pause for 2 months",
                AcceptedTitle = "Paused until Dec 14",
                AcceptedDescription = $"No charges until Dec 14, 2026. We'll remind {CancelEmail} a week before billing resumes."
            }
        },
        new("Missing a feature", "Something we need isn't there yet")
        {
            AsksForDetails = true,
            DetailsPrompt = "Which feature would keep you here? Emma from product reads these.",
            Offer = new CancelFlowOffer
            {
                Title = "Tell our product lead what's missing",
                Description = "Emma reads every note and replies within one business day. October is on us while you wait: "
                              + "your Oct 14 invoice is waived, Emma replies by Wednesday, Sep 23, and you get early access if it's already on the roadmap.",
                AcceptLabel = "Claim free month",
                AcceptedTitle = "October is on us",
                AcceptedDescription = "Emma has your note and will reply by Wednesday. Your Oct 14 invoice is waived."
            }
        },
        new("Moving to another tool", "We're consolidating on one product")
        {
            AsksForDetails = true,
            DetailsPrompt = "Which tool are you moving to?",
            Offer = new CancelFlowOffer
            {
                Title = "Keep your history on Starter",
                Description = "Moving tools takes a while. Starter keeps every project searchable for less than a third of the price: "
                              + "$29 a month instead of $96. All 42 projects and their history stay, and it saves $67 a month from Oct 14.",
                AcceptLabel = "Switch to Starter",
                AcceptedTitle = "Starter starts on Oct 14",
                AcceptedDescription = "Your team keeps working on Team until then. From Oct 14 the invoice is $29."
            }
        }
    ];

    /// <summary>What changes after the plan ends (uiarc.dev's three losses, title and detail in one line).</summary>
    public static string[] Losses() =>
    [
        "Full access until Oct 14, 2026. The $96 renewal won't be charged.",
        "Read-only from Oct 14. All 8 members can still open and export the 42 projects.",
        "Deleted on Nov 13, 2026. Projects, files and history are removed 30 days after the plan ends."
    ];

    #endregion
}
