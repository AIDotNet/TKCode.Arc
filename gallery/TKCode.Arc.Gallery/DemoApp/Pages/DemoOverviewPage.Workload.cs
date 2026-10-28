using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.DemoApp;

public sealed partial class DemoOverviewPage
{
    private readonly TextBlock _workloadSubtitle = Subtitle();
    private BarChart _hoursChart = null!;
    private SegmentedControl _hoursShown = null!;
    private bool _rebalanced;

    /// <summary>
    /// Workload: hours logged over the range beside next week's capacity (side by side from 940), then the retainers.
    /// </summary>
    private Control BuildWorkload()
    {
        _hoursShown = Segmented("Hours shown", "all", true, ("all", "All"), ("billable", "Billable"));
        _hoursShown.SelectionChanged += (_, _) => ShowHours();
        _hoursChart = new BarChart { Unit = "h", PlotHeight = 176, ValueFormat = "N0" };
        var hours = DemoUi.Panel("Hours logged", null, _hoursChart, _hoursShown);

        var split = Split(hours, BuildCapacity(), "1.55*,*");
        var body = new StackPanel { Spacing = 16 };
        body.Children.Add(split);
        body.Children.Add(BuildRetainers());
        return Section(SectionHead("Workload", _workloadSubtitle), body);
    }

    /// <summary>The bars, the subtitle's total and the chart's labels for the range and All / Billable.</summary>
    private void ShowHours()
    {
        if (_hoursChart is null)
        {
            return;
        }
        var billable = _hoursShown.SelectedValue as string == "billable";
        var quarter = _range.Id == "quarter";
        var week = _range.Id == "week";
        var rows = quarter ? OverviewData.Weekly : week ? OverviewData.Daily.Skip(23).ToArray() : OverviewData.Daily;
        var points = rows.Select((row, i) =>
        {
            var value = billable ? row.Billable : row.All;
            if (quarter)
            {
                return new ChartPoint(row.Day, value)
                {
                    Key = "week-" + row.Day.ToString("yyyy-MM-dd"),
                    Label = "Week of " + row.Day.ToString("MMM d", OverviewData.En),
                    AxisLabel = i % 3 == 0 ? row.Day.ToString("MMM d", OverviewData.En) : string.Empty
                };
            }
            var today = row.Day == DemoData.Now.Date;
            return new ChartPoint(row.Day, value)
            {
                Key = row.Day.ToString("yyyy-MM-dd"),
                Label = row.Day.ToString("ddd, MMM d", OverviewData.En) + (today ? ", so far" : string.Empty),
                AxisLabel = week ? row.Day.ToString("ddd", OverviewData.En)
                    : row.Day.DayOfWeek == DayOfWeek.Monday ? row.Day.ToString("MMM d", OverviewData.En) : string.Empty
            };
        }).ToArray();

        _hoursChart.Label = billable ? "Billable hours" : "Hours logged";
        _hoursChart.Period = _range.Period;
        _hoursChart.AverageLabel = quarter ? "Weekly average" : "Daily average";
        _hoursChart.ValueLabel = billable ? "Billable" : "Logged";
        _hoursChart.CategoryLabel = quarter ? "Week" : "Day";
        _hoursChart.ItemsSource = points;
        var total = rows.Sum(row => billable ? row.Billable : row.All);
        _workloadSubtitle.Text = $"{total.ToString("N0", OverviewData.En)} {(billable ? "billable " : string.Empty)}hours logged, {_range.Span.ToLowerInvariant()}";
    }

    /// <summary>
    /// Next week's capacity: a gauge of booked hours and three facts. Between 561 and 939 the gauge and the facts sit
    /// side by side, with the facts as a list.
    /// </summary>
    private Control BuildCapacity()
    {
        var rebalance = DemoUi.Button("Rebalance");
        var head = DemoUi.Head("Next week", "Sep 28 to Oct 2", rebalance, 14);
        var gauge = new Gauge
        {
            Value = 86, Minimum = 0, Maximum = 100, Label = "Capacity booked", Detail = "344 of 400 hours",
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4)
        };
        gauge.Thresholds.Add(new GaugeThreshold(0, GaugeTone.Success, "Healthy"));
        gauge.Thresholds.Add(new GaugeThreshold(80, GaugeTone.Warning, "Stretched"));
        gauge.Thresholds.Add(new GaugeThreshold(95, GaugeTone.Danger, "Overbooked"));
        var booked = Counter(344, " h");
        var open = Counter(56, " h");
        OverviewFact[] facts = [Fact("Booked", booked), Fact("Open", open), Fact("Out", FactValue("Sofia, Daniel"))];
        var factsGrid = new Grid();
        var factsBox = new Border { Child = factsGrid };
        ArcTokens.Bind(factsBox, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);

        void Apply(bool on, bool fromUndo)
        {
            _rebalanced = on;
            rebalance.Content = on ? "Restore plan" : "Rebalance";
            gauge.Value = on ? 78 : 86;
            gauge.Detail = on ? "312 of 400 hours" : "344 of 400 hours";
            booked.Value = on ? 312 : 344;
            open.Value = on ? 88 : 56;
            if (on)
            {
                Host.Toast(new ToastOptions
                {
                    Id = "rebalance", Kind = ToastKind.Success, Title = "Moved 32 hours to the week of Oct 5",
                    Description = "Harbor audit work shifts one week.", Action = new ToastAction("Undo", () => Apply(false, true))
                });
                return;
            }
            var restored = new ToastUpdate
            {
                Kind = ToastKind.Info, Title = "Next week's plan restored", Description = "The Harbor audit is back on next week.", RemoveAction = true
            };
            if (fromUndo)
            {
                Host.UpdateToast("rebalance", restored);
                return;
            }
            Host.Toast(new ToastOptions
            {
                Id = "rebalance", Kind = ToastKind.Info, Title = restored.Title!, Description = restored.Description
            });
        }
        rebalance.Click += (_, _) => Apply(!_rebalanced, false);

        var grid = new Grid { ColumnSpacing = 32, RowSpacing = 16 };
        grid.Children.Add(head);
        grid.Children.Add(gauge);
        grid.Children.Add(factsBox);
        var card = DemoUi.Card(grid, new Thickness(20, 16, 20, 20));
        var mode = -1;
        OnWidth(width =>
        {
            // In the side column (940 and up) and at phone width the card is one column; in between, two.
            var next = width >= SplitFrom ? 0 : width > CompactBelow ? 1 : 2;
            card.Padding = width <= CompactBelow ? new Thickness(16) : new Thickness(20, 16, 20, 20);
            if (next == mode)
            {
                return;
            }
            mode = next;
            var sideBySide = mode == 1;
            grid.ColumnDefinitions = new ColumnDefinitions(sideBySide ? "*,1.3*" : "*");
            grid.RowDefinitions = new RowDefinitions(sideBySide ? "Auto,*" : "Auto,*,Auto");
            Grid.SetColumnSpan(head, sideBySide ? 2 : 1);
            Grid.SetRow(gauge, 1);
            Grid.SetRow(factsBox, sideBySide ? 1 : 2);
            Grid.SetColumn(factsBox, sideBySide ? 1 : 0);
            gauge.VerticalAlignment = VerticalAlignment.Center;
            factsBox.VerticalAlignment = sideBySide ? VerticalAlignment.Center : VerticalAlignment.Bottom;
            factsBox.BorderThickness = sideBySide ? default : new Thickness(0, 1, 0, 0);
            OverviewFact.Layout(factsGrid, facts, sideBySide ? OverviewFactsMode.List : OverviewFactsMode.Columns, mode == 2 ? 12 : 16);
        });
        return card;
    }

    /// <summary>Retainers: September's hours against each client's allowance; one meter at a time below 860.</summary>
    private Control BuildRetainers()
    {
        UsageMeter Meter(string label, double limit, double design, double prototyping, double research, double meetings)
        {
            var meter = new UsageMeter { Label = label, Limit = limit, Unit = "h", Decimals = 1, FreeLabel = "Remaining" };
            meter.Segments.Add(new UsageSegment { Id = "design", Label = "Design", Value = design });
            meter.Segments.Add(new UsageSegment { Id = "prototyping", Label = "Prototyping", Value = prototyping });
            meter.Segments.Add(new UsageSegment { Id = "research", Label = "Research", Value = research });
            meter.Segments.Add(new UsageSegment { Id = "meetings", Label = "Meetings", Value = meetings });
            return meter;
        }
        UsageMeter[] meters =
        [
            Meter("Northwind", 80, 36, 19, 12.5, 6.5),
            Meter("Harbor Health", 120, 41, 22.5, 8, 9),
            Meter("Orbit Labs", 60, 38, 16.5, 6, 5.5)
        ];
        var keys = new[] { "northwind", "harbor", "orbit" };
        var picker = Segmented("Retainer", "northwind", ("northwind", "Northwind"), ("harbor", "Harbor Health"), ("orbit", "Orbit Labs"));
        picker.HorizontalAlignment = HorizontalAlignment.Stretch;

        var grid = new Grid();
        var dividers = new List<Border>();
        for (var i = 0; i < meters.Length; i++)
        {
            grid.Children.Add(meters[i]);
            if (i > 0)
            {
                var divider = new Border { Width = 1, HorizontalAlignment = HorizontalAlignment.Center };
                ArcTokens.Bind(divider, Border.BackgroundProperty, ArcDesignTokenKind.BorderSubtle);
                Grid.SetColumn(divider, 2 * i - 1);
                dividers.Add(divider);
                grid.Children.Add(divider);
            }
        }
        var narrow = (bool?)null;
        void Apply()
        {
            var selected = Array.IndexOf(keys, picker.SelectedValue as string);
            for (var i = 0; i < meters.Length; i++)
            {
                meters[i].IsVisible = narrow != true || i == selected;
                Grid.SetColumn(meters[i], narrow == true ? 0 : 2 * i);
            }
        }
        picker.SelectionChanged += (_, _) => Apply();
        OnWidth(width =>
        {
            var next = width <= NarrowBelow;
            if (next == narrow)
            {
                return;
            }
            narrow = next;
            picker.IsVisible = next;
            grid.ColumnDefinitions = new ColumnDefinitions(next ? "*" : "*,40,*,40,*");
            foreach (var divider in dividers)
            {
                divider.IsVisible = !next;
            }
            Apply();
        });

        var body = new StackPanel { Spacing = 16 };
        body.Children.Add(picker);
        body.Children.Add(grid);
        return DemoUi.Panel("Retainers", "Hours used in September against each client's allowance", body);
    }
}
