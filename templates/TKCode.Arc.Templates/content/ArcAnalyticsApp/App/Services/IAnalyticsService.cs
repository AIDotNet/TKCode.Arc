using TKCode.Arc;

namespace ArcStarterApp.Services;

/// <summary>
/// The figures the pages show, in the types of Arc's analytics blocks. SampleData/SampleAnalyticsService.cs makes them up;
/// replace it with queries to your warehouse or metrics API and register yours in Services/AppServices.cs.
/// </summary>
public interface IAnalyticsService
{
    /// <summary>The last day of the KPI series.</summary>
    DateOnly KpiEndDate { get; }

    IReadOnlyList<KpiDimension> GetKpiDimensions();

    IReadOnlyList<KpiMetric> GetKpis();

    /// <summary>The monthly recurring revenue before the first month.</summary>
    double OpeningRevenue { get; }

    IReadOnlyList<RevenueMonth> GetRevenue();

    IReadOnlyList<RetentionSegment> GetRetention();

    /// <summary>The first day of the billing period the usage forecast covers.</summary>
    DateOnly UsagePeriodStart { get; }

    IReadOnlyList<UsageMetric> GetUsage();

    /// <summary>The moment the status page shows as now.</summary>
    DateTimeOffset StatusNow { get; }

    IReadOnlyList<UptimeService> GetServices();

    IReadOnlyList<UptimeIncident> GetIncidents();
}
