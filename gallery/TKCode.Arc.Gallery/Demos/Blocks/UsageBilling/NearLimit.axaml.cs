using System;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class UsageBillingNearLimitDemo : UserControl
{
    public UsageBillingNearLimitDemo()
    {
        InitializeComponent();
        Billing.Clock = new FixedClock(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
        Billing.PeriodStart = new DateTime(2026, 10, 1);
        Billing.PeriodEnd = new DateTime(2026, 10, 31);
    }

    // $102.96 spent: 86% of a $120 limit is past the 80% alert; a $100 limit has been passed.
    private void OnScenarioChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Billing is not null)
        {
            Billing.SpendLimit = Scenario.SelectedValue as string == "over" ? 100 : 120;
        }
    }

    /// <summary>A clock stopped at one moment, so the projection and the days left stay the same.</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
