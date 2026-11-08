using ArcStarterApp.Services;
using TKCode.Arc;

namespace ArcStarterApp.ViewModels;

/// <summary>The KPI overview: the metrics, and the dimensions their breakdowns are cut by.</summary>
public sealed class OverviewPageViewModel(IAnalyticsService analytics)
{
    public DateOnly EndDate { get; } = analytics.KpiEndDate;

    public IReadOnlyList<KpiDimension> Dimensions { get; } = analytics.GetKpiDimensions();

    public IReadOnlyList<KpiMetric> Metrics { get; } = analytics.GetKpis();
}

/// <summary>Revenue by month: new, expansion and churn, from an opening MRR.</summary>
public sealed class RevenuePageViewModel(IAnalyticsService analytics)
{
    public double OpeningMrr { get; } = analytics.OpeningRevenue;

    public IReadOnlyList<RevenueMonth> Months { get; } = analytics.GetRevenue();
}

/// <summary>Cohort retention, by segment.</summary>
public sealed class RetentionPageViewModel(IAnalyticsService analytics)
{
    public IReadOnlyList<RetentionSegment> Segments { get; } = analytics.GetRetention();
}

/// <summary>Usage this billing period and where it is heading.</summary>
public sealed class UsagePageViewModel(IAnalyticsService analytics)
{
    public DateOnly PeriodStart { get; } = analytics.UsagePeriodStart;

    public IReadOnlyList<UsageMetric> Metrics { get; } = analytics.GetUsage();
}

/// <summary>The services' uptime and latency, and the incidents.</summary>
public sealed class StatusPageViewModel(IAnalyticsService analytics)
{
    public DateTimeOffset Now { get; } = analytics.StatusNow;

    public IReadOnlyList<UptimeService> Services { get; } = analytics.GetServices();

    public IReadOnlyList<UptimeIncident> Incidents { get; } = analytics.GetIncidents();
}
