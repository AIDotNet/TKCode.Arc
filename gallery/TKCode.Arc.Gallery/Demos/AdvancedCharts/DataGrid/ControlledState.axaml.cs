using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DataGridControlledStateDemo : UserControl
{
    public DataGridControlledStateDemo()
    {
        InitializeComponent();
        string[] regions = ["Europe", "Asia Pacific", "North America"];
        string[] stages = ["Discovery", "Proposal", "Committed", "Closed won"];
        var random = new Random(3);
        AccountsGrid.ItemsSource = Enumerable.Range(1, 60).Select(i => new DataGridRow($"a{i}")
        {
            { "account", $"Account {i}" },
            { "region", regions[random.Next(regions.Length)] },
            { "stage", stages[random.Next(stages.Length)] },
            { "arr", (decimal)(random.Next(4, 400) * 250) }
        }).ToList();
        SortButton.Click += (_, _) => AccountsGrid.SortDescriptions = [DataGridSort.Descending("arr")];
        FilterButton.Click += (_, _) => AccountsGrid.Filters = new Dictionary<string, DataGridFilter> { ["region"] = DataGridFilter.OneOf(["Europe"]) };
        DensityButton.Click += (_, _) => AccountsGrid.Density = DataGridDensity.Compact;
        GroupButton.Click += (_, _) => AccountsGrid.GroupBy = ["stage"];
        ResetButton.Click += (_, _) =>
        {
            AccountsGrid.SortDescriptions = [];
            AccountsGrid.Filters = new Dictionary<string, DataGridFilter>();
            AccountsGrid.SearchText = string.Empty;
            AccountsGrid.Density = DataGridDensity.Standard;
            AccountsGrid.GroupBy = [];
            AccountsGrid.CheckedRowKeys = [];
        };
        AccountsGrid.PropertyChanged += (_, e) =>
        {
            if (e.Property == DataGrid.SortDescriptionsProperty || e.Property == DataGrid.FiltersProperty ||
                e.Property == DataGrid.SearchTextProperty || e.Property == DataGrid.DensityProperty ||
                e.Property == DataGrid.GroupByProperty || e.Property == DataGrid.CheckedRowKeysProperty)
            {
                ShowState();
            }
        };
        ShowState();
    }

    private void ShowState()
    {
        var grid = AccountsGrid;
        State.Text =
            $"SortDescriptions: [{string.Join(", ", grid.SortDescriptions.Select(s => $"{s.Key} {s.Direction}"))}]\n" +
            $"Filters: [{string.Join(", ", grid.Filters.Select(f => $"{f.Key}: {f.Value}"))}]  SearchText: \"{grid.SearchText}\"\n" +
            $"Density: {grid.Density}  GroupBy: [{string.Join(", ", grid.GroupBy)}]  CheckedRowKeys: {grid.CheckedRowKeys.Count}";
    }
}
