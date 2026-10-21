using System;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class UsageBillingLiveDemo : UserControl
{
    private static readonly double[] s_days =
    [
        4.97, 4.82, 2.79, 2.41, 5.74, 5.58, 5.18, 6.04, 5.35, 3.02,
        2.76, 5.75, 6.47, 7.34, 6.12, 6.41, 3.60, 3.97, 7.45, 7.19
    ];

    // An observable list: changing today's amount redraws its bar.
    private readonly AvaloniaList<double> _days = new(s_days);

    public UsageBillingLiveDemo()
    {
        InitializeComponent();
        Billing.Clock = new FixedClock(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
        Billing.PeriodStart = new DateTime(2026, 10, 1);
        Billing.PeriodEnd = new DateTime(2026, 10, 31);
        Billing.DailySpend = _days;
    }

    private UsageBillingMeter ApiCalls => Billing.Meters.First(m => m.Name == "API calls");

    // 50K more calls past the included million cost 5 × $0.40 = $2.00. The meter notifies the block, which updates
    // the spend, the projection, the bar and the limit; today's bar takes the same $2.00.
    private void OnAddUsage(object? sender, RoutedEventArgs e)
    {
        ApiCalls.Used += 50_000;
        _days[^1] = Math.Round(_days[^1] + 2.00, 2);
    }

    private void OnReset(object? sender, RoutedEventArgs e)
    {
        ApiCalls.Used = 1_240_000;
        _days[^1] = s_days[^1];
    }

    /// <summary>A clock stopped at one moment, so the projection and the days left stay the same.</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
