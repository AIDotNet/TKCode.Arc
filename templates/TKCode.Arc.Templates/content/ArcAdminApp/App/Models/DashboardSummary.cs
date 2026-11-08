namespace ArcStarterApp.Models;

/// <summary>One figure of the dashboard: its value, how it changed and a trend for the sparkline.</summary>
public sealed record Kpi(string Label, double Value, string? Prefix, string? Suffix, int Decimals, string Change, IList<double> Trend);

/// <summary>The dashboard: the headline figures and the daily revenue of the last 30 days (oldest first).</summary>
public sealed record DashboardSummary(IReadOnlyList<Kpi> Kpis, IReadOnlyList<DateOnly> Days, IReadOnlyList<double> Revenue, IReadOnlyList<double> PreviousRevenue);
