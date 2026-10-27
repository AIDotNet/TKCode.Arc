using System.Globalization;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ChoroplethExplorerCustomLookDemo : UserControl
{
    public ChoroplethExplorerCustomLookDemo()
    {
        InitializeComponent();
        var italy = ChoroplethExplorerDemoData.Area("it");
        // Revenue in thousands of euros, written the way the finance team reports it.
        var revenue = italy.Metrics[0];
        revenue.Label = "Bookings";
        revenue.HeadlineLabel = "Signed this year";
        revenue.LegendCaption = "Shaded by bookings";
        revenue.Formatter = v => (v / 1000).ToString("N0", CultureInfo.GetCultureInfo("en-US")) + " k€";
        Explorer.Labels = new ChoroplethLabels
        {
            RankedByFormat = "Sorted by {0}",
            BackFormat = "All of Italy",
            NoDataText = "No contracts",
        };
        Explorer.Areas.Add(italy);
    }
}
