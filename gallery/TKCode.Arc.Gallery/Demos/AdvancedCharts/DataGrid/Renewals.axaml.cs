using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DataGridRenewalsDemo : UserControl
{
    private static readonly string[] Regions = ["North America", "Europe", "Asia Pacific", "Latin America"];
    private static readonly string[] Stages = ["Discovery", "Proposal", "Negotiation", "Committed", "Closed won", "Closed lost"];
    private static readonly string[] Names =
    [
        "Alder", "Basalt", "Cobalt", "Delta", "Ember", "Fjord", "Garnet", "Harbor", "Indigo", "Juniper", "Kelp", "Lumen", "Maple",
        "Nimbus", "Onyx", "Pebble", "Quartz", "Ridge", "Saffron", "Tundra", "Umber", "Vale", "Willow", "Yarrow", "Zephyr"
    ];
    private static readonly string[] Kinds =
    [
        "Analytics", "Bakery", "Clinic", "Dental", "Energy", "Freight", "Garden", "Hotels", "Insurance", "Labs", "Media", "Motors",
        "Outfitters", "Pharmacy", "Robotics", "Studios", "Travel", "Utilities"
    ];
    private static readonly string[] Owners =
    [
        "Ada Brooks", "Bilal Khan", "Chen Wei", "Dana Ortiz", "Elif Demir", "Femi Adeyemi", "Greta Lind", "Hiro Tanaka", "Ines Costa",
        "Jonas Weber"
    ];

    private readonly ObservableCollection<DataGridRow> _rows;

    public DataGridRenewalsDemo()
    {
        InitializeComponent();
        RenewalsGrid.Columns[2].Options = Regions;
        RenewalsGrid.Columns[3].Options = Stages;
        _rows = new ObservableCollection<DataGridRow>(CreateRows(10_000));
        RenewalsGrid.ItemsSource = _rows;
        RenewalsGrid.CellsChanged += (_, _) => ResetButton.IsEnabled = true;
        RenewalsGrid.RowsDeleted += (_, _) => ResetButton.IsEnabled = true;
        ResetButton.Click += (_, _) =>
        {
            _rows.Clear();
            foreach (var row in CreateRows(10_000))
            {
                _rows.Add(row);
            }
            RenewalsGrid.ClearHistory();
            ResetButton.IsEnabled = false;
        };
    }

    /// <summary>Generated accounts: the same rows every time (a fixed seed).</summary>
    private static List<DataGridRow> CreateRows(int count)
    {
        var random = new Random(20261020);
        var rows = new List<DataGridRow>(count);
        for (var i = 0; i < count; i++)
        {
            var seats = 5 * (int)Math.Round((10 + Math.Pow(random.NextDouble(), 2.4) * 790) / 5);
            var price = new[] { 240, 300, 360, 420 }[random.Next(4)];
            var month = random.Next(12);
            rows.Add(new DataGridRow($"acct-{i + 1}")
            {
                { "account", $"{Names[i % Names.Length]} {Kinds[i / Names.Length % Kinds.Length]}{(i >= Names.Length * Kinds.Length ? $" {i / (Names.Length * Kinds.Length) + 1}" : "")}" },
                { "owner", Owners[random.Next(Owners.Length)] },
                { "region", Regions[random.Next(Regions.Length)] },
                { "stage", Stages[Math.Min(Stages.Length - 1, (int)(Math.Pow(random.NextDouble(), 1.3) * Stages.Length))] },
                { "seats", (decimal)seats },
                { "arr", (decimal)(seats * price) },
                { "health", (decimal)Math.Round(35 + 64 * random.NextDouble()) },
                { "renewal", new DateOnly(month < 8 ? 2027 : 2026, month + 1, 1 + random.Next(28)) }
            });
        }
        return rows;
    }
}
