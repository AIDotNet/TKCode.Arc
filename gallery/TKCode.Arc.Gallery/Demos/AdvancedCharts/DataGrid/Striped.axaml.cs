using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DataGridStripedDemo : UserControl
{
    public DataGridStripedDemo()
    {
        InitializeComponent();
        Grid.ItemsSource = new ObservableCollection<DataGridRow>
        {
            new("inv-1001") { { "invoice", "INV-1001" }, { "customer", "Lumen Labs" }, { "amount", 1200m } },
            new("inv-1002") { { "invoice", "INV-1002" }, { "customer", "Basalt Freight" }, { "amount", 800m } },
            new("inv-1003") { { "invoice", "INV-1003" }, { "customer", "Cobalt Clinic" }, { "amount", 3000m } },
            new("inv-1004") { { "invoice", "INV-1004" }, { "customer", "Delta Dental" }, { "amount", 450m } },
            new("inv-1005") { { "invoice", "INV-1005" }, { "customer", "Ember Foods" }, { "amount", 1980m } },
            new("inv-1006") { { "invoice", "INV-1006" }, { "customer", "Fjord Energy" }, { "amount", 2640m } }
        };
    }
}
