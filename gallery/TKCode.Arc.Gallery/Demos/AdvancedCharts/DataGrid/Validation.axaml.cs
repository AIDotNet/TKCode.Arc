using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DataGridValidationDemo : UserControl
{
    public DataGridValidationDemo()
    {
        InitializeComponent();
        OrdersGrid.Columns[3].Options = ["Open", "Late", "Shipped"];
        // Locks whole rows: edits, pastes, fills and clears skip the cells this refuses.
        OrdersGrid.CanEditCell = context => context.Row is not DataGridRow row || row["status"] as string != "Shipped";
        OrdersGrid.ItemsSource = new ObservableCollection<DataGridRow>
        {
            new("o1") { { "item", "Standing desk" }, { "qty", 4m }, { "price", 649m }, { "status", "Open" }, { "due", new DateOnly(2026, 7, 14) } },
            new("o2") { { "item", "Monitor arm" }, { "qty", 12m }, { "price", 89.5m }, { "status", "Shipped" }, { "due", new DateOnly(2026, 5, 2) } },
            new("o3") { { "item", "Desk lamp" }, { "qty", 120m }, { "price", 24m }, { "status", "Late" }, { "due", new DateOnly(2026, 4, 20) } },
            new("o4") { { "item", "Cable tray" }, { "qty", 30m }, { "price", 18.25m }, { "status", "Open" }, { "due", new DateOnly(2026, 8, 3) } }
        };
        FlagBox.IsCheckedChanged += (_, _) =>
            OrdersGrid.ValidationMode = FlagBox.IsChecked == true ? DataGridValidationMode.Flag : DataGridValidationMode.Block;
        CheckAll.Click += (_, _) => Log.Text = $"{OrdersGrid.ValidateAll()} cell(s) need attention. Press F8 to jump to the next one.";
    }
}
