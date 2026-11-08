using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A dashboard tile of the tile panel demo, with the cells it asks for.</summary>
public sealed record DashboardTile(string Title, string Value, int RowSpan = 1, int ColumnSpan = 1);

public partial class TilePanelItemsControlDemo : UserControl
{
    private readonly ObservableCollection<DashboardTile> _tiles =
    [
        new("Revenue", "$48.2k", 2, 2),
        new("Orders", "1,284"),
        new("Refunds", "12"),
        new("Visitors", "38.9k", 1, 2),
        new("Conversion", "3.3%"),
        new("Churn", "1.9%"),
    ];

    private int _added;

    public TilePanelItemsControlDemo()
    {
        InitializeComponent();
        Tiles.ItemsSource = _tiles;
        Add.Click += (_, _) => _tiles.Add(new DashboardTile($"Metric {++_added}", $"{_added * 7}"));
        Grow.Click += (_, _) =>
        {
            if (_tiles.Count > 0)
            {
                var first = _tiles[0];
                // Replacing the item rebuilds its container, which reads the new spans (MaxRowSpan/MaxColumnSpan still apply).
                _tiles[0] = first with { RowSpan = first.RowSpan % 2 + 1, ColumnSpan = first.ColumnSpan % 2 + 1 };
            }
        };
        Remove.Click += (_, _) =>
        {
            if (_tiles.Count > 0)
            {
                _tiles.RemoveAt(_tiles.Count - 1);
            }
        };
    }
}
