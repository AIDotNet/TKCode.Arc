using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DataGridBudgetDemo : UserControl
{
    public DataGridBudgetDemo()
    {
        InitializeComponent();
        // A computed, read-only column: no setter, so edits, pastes and fills skip it.
        BudgetGrid.Columns.Insert(3, DataGridColumn.Create<DataGridRow>("change", "Change",
            row => row["q1"] is decimal q1 && q1 != 0 && row["q2"] is decimal q2 ? Math.Round((q2 - q1) / q1 * 100, 1) : null,
            type: DataGridColumnType.Percent));
        BudgetGrid.Columns[3].Decimals = 1;
        BudgetGrid.ItemsSource = new ObservableCollection<DataGridRow>
        {
            new("salaries") { { "line", "Salaries" }, { "q1", 120000m }, { "q2", 126000m }, { "notes", "Two hires in May" } },
            new("rent") { { "line", "Office rent" }, { "q1", 18000m }, { "q2", 18000m } },
            new("cloud") { { "line", "Cloud hosting" }, { "q1", 9400m }, { "q2", 11200m }, { "notes", "New region" } },
            new("travel") { { "line", "Travel" }, { "q1", 6200m }, { "q2", 4800m } },
            new("events") { { "line", "Events" }, { "q1", 3500m }, { "q2", 9000m }, { "notes", "Conference booth" } }
        };
        BudgetGrid.BulkActionInvoked += (_, e) => Log.Text = $"Archived {e.Rows.Count} line items.";
    }
}
