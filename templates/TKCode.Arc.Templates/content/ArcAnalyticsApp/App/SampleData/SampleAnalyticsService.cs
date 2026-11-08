using ArcStarterApp.Services;
using TKCode.Arc;

namespace ArcStarterApp.SampleData;

/// <summary>Invented figures (SampleKpis, SampleRevenue, SampleRetention, SampleUsage, SampleUptime), the same on every run.</summary>
public sealed class SampleAnalyticsService : IAnalyticsService
{
    public DateOnly KpiEndDate => SampleKpis.End;

    public IReadOnlyList<KpiDimension> GetKpiDimensions() => SampleKpis.Dimensions();

    public IReadOnlyList<KpiMetric> GetKpis() => SampleKpis.Metrics();

    public double OpeningRevenue => SampleRevenue.Opening;

    public IReadOnlyList<RevenueMonth> GetRevenue() => SampleRevenue.Standard();

    public IReadOnlyList<RetentionSegment> GetRetention() => SampleRetention.SpaceSegments();

    public DateOnly UsagePeriodStart => SampleUsage.Start;

    public IReadOnlyList<UsageMetric> GetUsage() => SampleUsage.Metrics();

    public DateTimeOffset StatusNow => SampleUptime.Now;

    public IReadOnlyList<UptimeService> GetServices() => SampleUptime.Services();

    public IReadOnlyList<UptimeIncident> GetIncidents() => SampleUptime.Incidents();
}
