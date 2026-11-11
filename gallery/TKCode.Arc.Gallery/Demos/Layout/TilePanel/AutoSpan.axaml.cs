using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>An app tile of the auto-span demo: no spans of its own, the panel sizes it.</summary>
public sealed record AppTile(string Name);

public partial class TilePanelAutoSpanDemo : UserControl
{
    private static readonly string[] s_names =
    [
        "Mail", "Calendar", "Photos", "Music", "Maps", "Weather", "Notes", "News",
        "Files", "Camera", "Clock", "Store", "Health", "Wallet", "Books", "Podcasts",
    ];

    private readonly ObservableCollection<AppTile> _tiles = [];
    private int _seed;

    public TilePanelAutoSpanDemo()
    {
        InitializeComponent();
        for (var i = 0; i < 12; i++)
        {
            _tiles.Add(new AppTile(s_names[i]));
        }
        Tiles.ItemsSource = _tiles;
        ShowSeed();
        // The panel is created from the ItemsPanelTemplate, so the buttons reach it through ItemsPanelRoot.
        Shuffle.Click += (_, _) =>
        {
            _seed++;
            if (Tiles.ItemsPanelRoot is TilePanel panel)
            {
                panel.Seed = _seed;
            }
            ShowSeed();
        };
        Fill.IsCheckedChanged += (_, _) =>
        {
            if (Tiles.ItemsPanelRoot is TilePanel panel)
            {
                panel.IsAutoFill = Fill.IsChecked == true;
            }
        };
        Add.Click += (_, _) =>
        {
            _tiles.Add(new AppTile(s_names[_tiles.Count % s_names.Length]));
            ShowSeed();
        };
        Remove.Click += (_, _) =>
        {
            if (_tiles.Count > 1)
            {
                _tiles.RemoveAt(_tiles.Count - 1);
            }
            ShowSeed();
        };
    }

    private void ShowSeed() => SeedLabel.Text = $"Seed {_seed} · {_tiles.Count} items";
}
