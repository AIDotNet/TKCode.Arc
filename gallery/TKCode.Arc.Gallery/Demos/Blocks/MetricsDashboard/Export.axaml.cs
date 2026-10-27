using System.Globalization;
using System.Text;
using System.Windows.Input;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MetricsDashboardExportDemo : UserControl
{
    public MetricsDashboardExportDemo()
    {
        InitializeComponent();
        Dashboard.EndDate = MetricsDashboardDemoData.EndDate;
        Dashboard.Metrics.AddRange(MetricsDashboardDemoData.SiteMetrics());
        Dashboard.PrimaryItems.AddRange(MetricsDashboardDemoData.TopPaths());
        Dashboard.SecondaryItems.AddRange(MetricsDashboardDemoData.Referrers());
        Dashboard.ExportCommand = new ExportCsv(csv => Log.Text = csv);
    }

    /// <summary>Writes the selected metric's daily values for the range as CSV and shows the first lines.</summary>
    private sealed class ExportCsv(Action<string> show) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => parameter is MetricsDashboardExportRequest { Metric.Daily: not null };

        public void Execute(object? parameter)
        {
            if (parameter is not MetricsDashboardExportRequest { Metric: { Daily: { } daily } metric } request)
            {
                return;
            }
            var csv = new StringBuilder("date,").Append(metric.Key).Append('\n');
            var first = daily.Count - request.RangeDays;
            for (var i = Math.Max(0, first); i < daily.Count; i++)
            {
                var date = request.EndDate.AddDays(i - (daily.Count - 1));
                csv.Append(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                   .Append(daily[i].ToString("0.##", CultureInfo.InvariantCulture)).Append('\n');
            }
            var lines = csv.ToString().Split('\n');
            show($"{metric.Label}, {request.StartDate:MMM d} – {request.EndDate:MMM d}: {lines.Length - 2} rows\n" +
                 string.Join("  ", lines.Take(4)) + " …");
        }
    }
}
