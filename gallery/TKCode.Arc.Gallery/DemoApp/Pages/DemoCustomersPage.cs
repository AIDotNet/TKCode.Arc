using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// uiarc.dev/demo/customers: the customers table across the page, then new-account growth beside the account activity
/// feed. The table's floating bar of bulk actions lies over the page's scroller so it can stay at the bottom of the
/// window while the table runs past it.
/// </summary>
public sealed class DemoCustomersPage : DemoPageView
{
    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");
    private static readonly int[] s_signups = [31, 36, 34, 41, 38, 44, 47, 43, 52, 49, 58, 61];
    private static readonly int[] s_upgrades = [6, 8, 7, 9, 11, 8, 12, 10, 13, 12, 15, 17];

    private readonly DemoCustomersTable _table;

    public DemoCustomersPage(IDemoHost host, DemoPage page)
        : base(host, page)
    {
        _table = new DemoCustomersTable(host);
        AddRow(DemoRowLayout.Stack, "accounts", _table);
        AddRow(DemoRowLayout.MainSide, "growth", BuildGrowth(), BuildActivity());

        // Lay the dock over the scroller (the base class made the scroller the content).
        Content = null;
        var layer = new Grid();
        layer.Children.Add(Scroll);
        layer.Children.Add(_table.Dock);
        Content = layer;
        void Place() => _table.PlaceDock(layer, layer.Bounds.Height);
        Scroll.ScrollChanged += (_, _) => Place();
        layer.LayoutUpdated += (_, _) => Place();
    }

    private static Control BuildGrowth()
    {
        var chart = new BarChart
        {
            PlotHeight = 220,
            Period = "Jul 7 – Sep 22, 2026",
            AverageLabel = "Weekly average",
            CategoryLabel = "Week",
            ValueFormat = "N0"
        };
        void Show(bool upgrades)
        {
            chart.Label = upgrades ? "Upgrades" : "New accounts";
            chart.ValueLabel = upgrades ? "Upgraded" : "Signed up";
            var values = upgrades ? s_upgrades : s_signups;
            var last = new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc);
            chart.ItemsSource = values.Select((value, i) =>
            {
                var week = last.AddDays(7 * (i - values.Length + 1));
                return new ChartPoint(week, value)
                {
                    Key = week.ToString("yyyy-MM-dd", s_culture),
                    Label = "Week of " + week.ToString("MMM d", s_culture),
                    AxisLabel = week.ToString("MMM d", s_culture)
                };
            }).ToArray();
        }
        Show(false);

        var measure = new SegmentedControl();
        Avalonia.Automation.AutomationProperties.SetName(measure, "Growth measure");
        measure.Items.Add(new SegmentedOption { Value = "signups", Label = "Signups" });
        measure.Items.Add(new SegmentedOption { Value = "upgrades", Label = "Upgrades" });
        measure.SelectedValue = "signups";
        measure.SelectionChanged += (_, _) => Show(measure.SelectedValue as string == "upgrades");

        var panel = DemoUi.Panel("Growth", "Weekly, the last 12 weeks", chart, measure);
        panel.VerticalAlignment = VerticalAlignment.Top;
        return panel;
    }

    private static Control BuildActivity()
    {
        var now = new DateTimeOffset(DemoData.Now);
        DateTimeOffset At(int day, int hour, int minute) => new(2026, 9, day, hour, minute, 0, TimeSpan.Zero);
        var feed = new Timeline
        {
            Label = "Recent account activity",
            Now = now,
            TimeZone = TimeZoneInfo.Utc,
            Culture = s_culture,
            HeadingLevel = 3,
            MaxHeight = 300,
            ItemsSource = DemoUi.WithInitials(new List<TimelineEvent>
            {
                new("a1", At(22, 14, 42), "moved Halcyon Freight to Scale", "Ava Mitchell") { Meta = "+$1,200 MRR", Tone = TimelineTone.Success },
                new("a2", At(22, 11, 5), "started a trial for Juniper Labs", "Marcus Johnson") { Meta = "14 days, 25 seats" },
                new("a3", At(21, 16, 30), "Orbital Payroll's renewal payment failed") { Meta = "Retrying on Sep 24", Tone = TimelineTone.Danger },
                new("a4", At(21, 9, 12), "added 12 seats to Brightline Health", "Emma Collins") { Meta = "Growth plan" },
                new("a5", At(19, 15, 48), "renewed Copperleaf Insurance for a year", "Jasmine Brooks") { Meta = "$18,600 a year", Tone = TimelineTone.Success }
            }).ToList()
        };
        var panel = DemoUi.Panel("Account activity", "Upgrades, trials, and renewals", feed);
        panel.VerticalAlignment = VerticalAlignment.Top;
        return panel;
    }
}
