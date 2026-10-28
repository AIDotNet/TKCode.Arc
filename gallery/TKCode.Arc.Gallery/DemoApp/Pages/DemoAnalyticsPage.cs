using Avalonia.Layout;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// uiarc.dev/demo/analytics: Arc's four analytics blocks with their default data. Recurring revenue on top, the MRR bridge
/// beside cohort retention (row "movement"), then the journey to a paid plan (row "journeys"). No toolbar.
/// </summary>
public sealed class DemoAnalyticsPage : DemoPageView
{
    public DemoAnalyticsPage(IDemoHost host, DemoPage page)
        : base(host, page)
    {
        AddRow(DemoRowLayout.Stack, "revenue", DemoUi.Frame(BuildRevenue()));
        AddRow(DemoRowLayout.Split, "movement", BuildWaterfall(), DemoUi.Frame(BuildCohorts()));
        AddRow(DemoRowLayout.Stack, "journeys", DemoUi.Frame(BuildJourneys()));
    }

    private static RevenueExplorer BuildRevenue()
    {
        var revenue = new RevenueExplorer
        {
            Title = "Recurring revenue",
            OpeningMrr = DemoAnalyticsData.OpeningMrr,
            DefaultRangeMonths = 12,
            TableCaptionFormat = "Monthly MRR movements, {0}",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        revenue.Presets.AddRange(
        [
            new RevenueRangePreset(3, "3M"),
            new RevenueRangePreset(6, "6M"),
            new RevenueRangePreset(12, "12M"),
            new RevenueRangePreset(0, "All", "All 24 months")
        ]);
        revenue.Months.AddRange(DemoAnalyticsData.RevenueMonths());
        return revenue;
    }

    private static MrrWaterfall BuildWaterfall()
    {
        var waterfall = new MrrWaterfall
        {
            Title = "MRR movement",
            Scope = "All plans",
            MaxNamedAccounts = 5,
            ValueMode = MrrValueMode.Currency,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Labels = new MrrWaterfallLabels
            {
                PeriodSwitch = "Period",
                ValueSwitch = "Show values as",
                CurrencyOption = "USD",
                PercentOption = "% of start",
                EndingFormat = "Ending MRR on {0}",
                SinceFormat = "since {0}",
                Hint = "Select a bar or use the arrow keys to see its accounts",
                HoverHint = "Click the bar to keep this list",
                RestLabel = "Other accounts",
                PeriodAnnouncementFormat = "{0}: MRR moved from {1} to {2}, {3} {4}"
            }
        };
        var periods = DemoAnalyticsData.MrrPeriods();
        waterfall.Periods.AddRange(periods);
        waterfall.SelectedPeriod = periods[^1];
        waterfall.SelectedIndex = 1;
        return waterfall;
    }

    private static CohortRetention BuildCohorts()
    {
        var cohorts = new CohortRetention
        {
            Title = "Cohort retention",
            Subtitle = "Workspaces still active each month after signup. Data through Aug 31, 2026.",
            Months = 12,
            Thresholds = [0.2, 0.35, 0.5, 0.65, 0.8],
            FirstMetricMonth = 1,
            SecondMetricMonth = 6,
            CompareCohortCount = 3,
            ShowSizeColumn = true,
            ShowAverageRow = true,
            View = CohortRetentionView.Heatmap,
            FirstMetricLabel = "Month 1 retention",
            SecondMetricLabel = "Month 6 retention",
            TotalLabel = "New workspaces",
            TotalContextFormat = "Signed up {0} to {1}",
            CompareFormat = "latest {0} vs first {0} cohorts",
            AxisTitle = "Months since signup",
            AxisTitleShort = "Month",
            LegendTitle = "% retained",
            AverageLegendText = "Average, size-weighted",
            CohortLegendText = "One line per cohort",
            HeatmapText = "Heatmap",
            CurvesText = "Curves",
            SegmentSwitchLabel = "Segment",
            ViewSwitchLabel = "Chart view",
            StillActiveText = "Still active",
            VsAverageText = "Vs average",
            WeightedOverText = "Weighted across",
            NotMeasuredText = "Not yet measured",
            ChartHint = "Use the arrow keys to move between cohorts and months, Home and End to jump within a cohort, and Escape to clear.",
            HeatmapAnnouncement = "Showing the retention heatmap.",
            CurvesAnnouncement = "Showing one retention curve per cohort with the weighted average.",
            SegmentAnnouncementFormat = "Showing {0}. Month 1 retention {2}, month 6 retention {4}.",
            CellAnnouncementFormat = "{0} cohort, month {1}: {2} retained, {3} of {4} workspaces.",
            AverageAnnouncementFormat = "Average of {0}, month {1}: {2} retained.",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        cohorts.Segments.AddRange(DemoAnalyticsData.RetentionSegments());
        return cohorts;
    }

    private static JourneyFlow BuildJourneys()
    {
        var journeys = new JourneyFlow
        {
            Title = "Journeys to a paid plan",
            SubtitleFormat = "{0} visits in August, followed from first session to plan",
            SegmentsLabel = "Acquisition channel",
            PanelLabel = "Step breakdown",
            GoalCaption = "of visitors started a paid plan",
            OverviewHint = "Select a step to see where its users came from and went next",
            DropOffsHeader = "Largest drop-offs",
            OutcomesHeader = "Plan after setup",
            InboundHeader = "Came from",
            OutboundHeader = "Went to",
            JourneyStartText = "Journey starts here",
            JourneyEndText = "Journey ends here",
            ClearText = "Clear",
            TableHeader = "Flow table",
            TableCaptionFormat = "Flows for {0}, August",
            TableColumnHeaders = ["From", "To", "Users", "Share of step"],
            AnnouncementFormat = "{0}: {1} visits and {2} paid plans.",
            MaxDropOffRows = 2,
            IsSidePanelVisible = true,
            IsTableVisible = true,
            IsTableExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        journeys.Stages.AddRange(DemoAnalyticsData.JourneyStages());
        journeys.Nodes.AddRange(DemoAnalyticsData.JourneyNodes());
        journeys.Segments.AddRange(DemoAnalyticsData.JourneySegments());
        return journeys;
    }
}
