using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RevenueExplorerEuroDemo : UserControl
{
    public RevenueExplorerEuroDemo()
    {
        InitializeComponent();
        Revenue.OpeningMrr = RevenueExplorerDemoData.EuroOpening;
        Revenue.Months.AddRange(RevenueExplorerDemoData.Euro());
        // A range text with the month names only, no years: "Mär – Feb".
        Revenue.RangeFormatter = (from, to) => $"{from:MMM} – {to:MMM}";
    }
}
