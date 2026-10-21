using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Demos;

public partial class DataGridCustomCellsDemo : UserControl
{
    private readonly ObservableCollection<DataGridRow> _rows = new()
    {
        new("d1") { { "account", "Lumen Labs" }, { "stage", "Negotiation" }, { "health", 72m }, { "region", "Europe" }, { "active", true }, { "arr", 48000m } },
        new("d2") { { "account", "Basalt Freight" }, { "stage", "Won" }, { "health", 91m }, { "region", "Asia Pacific" }, { "active", true }, { "arr", 126000m } },
        new("d3") { { "account", "Cobalt Clinic" }, { "stage", "Proposal" }, { "health", 54m }, { "region", "Europe" }, { "active", false }, { "arr", 31500m } },
        new("d4") { { "account", "Delta Dental" }, { "stage", "Lost" }, { "health", 18m }, { "region", "North America" }, { "active", false }, { "arr", 22000m } },
        new("d5") { { "account", "Ember Foods" }, { "stage", "Discovery" }, { "health", 36m }, { "region", "North America" }, { "active", true }, { "arr", 15800m } },
        new("d6") { { "account", "Fjord Energy" }, { "stage", "Won" }, { "health", 83m }, { "region", "Europe" }, { "active", true }, { "arr", 210000m } }
    };

    public DataGridCustomCellsDemo()
    {
        InitializeComponent();
        // Select columns sort in the order of their options; the badge colors come from the ToneMap in the XAML.
        DealsGrid.Columns[1].Options = ["Discovery", "Proposal", "Negotiation", "Won", "Lost"];
        DealsGrid.Columns[3].Options = ["Europe", "Asia Pacific", "North America"];
        // An icon per value, drawn at 16 px with the theme's stroke color.
        var regions = new IconTextRenderer();
        regions.Icons["Europe"] = ArcIcons.Globe;
        regions.Icons["Asia Pacific"] = ArcIcons.Globe;
        regions.Icons["North America"] = ArcIcons.Globe;
        DealsGrid.Columns[3].Renderer = regions;
        // Only the cell being edited is a real control: Health edits with a slider (Enter commits, Escape cancels).
        DealsGrid.Columns[2].EditTemplate = new FuncDataTemplate<DataGridEditContext>((context, _) =>
        {
            var slider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Value = context.Value is decimal value ? (double)value : 0,
                Margin = new Thickness(12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            slider.ValueChanged += (_, e) => context.Value = Math.Round((decimal)e.NewValue);
            return slider;
        });
        // A row of icon buttons at the end of each row; a click raises CellInvoked with the action's label.
        var actions = new ActionsRenderer();
        actions.Actions.Add(new DataGridCellAction { Label = "edit", Icon = ArcIcons.Pencil });
        actions.Actions.Add(new DataGridCellAction { Label = "delete", Icon = ArcIcons.Trash2, IsDanger = true });
        DealsGrid.Columns.Add(new DataGridColumn("actions", "Actions")
        {
            Width = 100,
            Renderer = actions,
            IsEditable = false,
            IsSortable = false,
            IsFilterable = false
        });
        DealsGrid.ItemsSource = _rows;
        DealsGrid.CellInvoked += (_, e) =>
        {
            var row = (DataGridRow)e.Row;
            if (e.Part == "delete")
            {
                _rows.Remove(row);
                Log.Text = $"Deleted {row["account"]}.";
                return;
            }
            Log.Text = $"{(e.Part == "edit" ? "Edit" : "Opened")} {row["account"]}.";
        };
    }
}
