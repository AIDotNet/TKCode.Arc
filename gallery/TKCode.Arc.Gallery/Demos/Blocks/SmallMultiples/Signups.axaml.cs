using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SmallMultiplesSignupsDemo : UserControl
{
    public SmallMultiplesSignupsDemo()
    {
        InitializeComponent();
        // Counts, not dollars: the readout and headings use whole numbers, the axis a compact form.
        Chart.ValueFormatter = SmallMultiplesDemoData.Signups;
        Chart.TickFormatter = SmallMultiplesDemoData.SignupTicks;
        Chart.Periods.AddRange(SmallMultiplesDemoData.Weeks());
        Chart.Series.AddRange(SmallMultiplesDemoData.Plans());
    }
}
