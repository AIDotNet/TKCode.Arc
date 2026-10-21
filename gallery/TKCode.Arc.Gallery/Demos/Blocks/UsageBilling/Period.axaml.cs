using System;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class UsageBillingPeriodDemo : UserControl
{
    public UsageBillingPeriodDemo()
    {
        InitializeComponent();
        // The October period of Larkspur Studio, seen on Oct 20: twenty days of spend so far.
        Billing.Clock = new FixedClock(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
        Billing.PeriodStart = new DateTime(2026, 10, 1);
        Billing.PeriodEnd = new DateTime(2026, 10, 31);
        Billing.DailySpend =
        [
            4.97, 4.82, 2.79, 2.41, 5.74, 5.58, 5.18, 6.04, 5.35, 3.02,
            2.76, 5.75, 6.47, 7.34, 6.12, 6.41, 3.60, 3.97, 7.45, 7.19
        ];
    }

    /// <summary>A clock stopped at one moment, so the projection and the days left stay the same.</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
