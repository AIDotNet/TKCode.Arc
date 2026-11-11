using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A photo card of the varied-sizes waterfall demo: every card uses the same template.</summary>
public sealed record WaterfallPhoto(string Title, string Place);

public partial class WaterfallPanelVariedSizesDemo : UserControl
{
    private static readonly (string Title, string Place)[] s_photos =
    [
        ("Harbor at dawn", "Lisbon"),
        ("Tea terraces", "Hangzhou"),
        ("Snow line", "Hokkaido"),
        ("Market lights", "Taipei"),
        ("Old town roofs", "Prague"),
        ("Salt flats", "Uyuni"),
        ("Night ferry", "Hong Kong"),
        ("Cliff path", "Cornwall"),
        ("Rice fields", "Bali"),
        ("Glass house", "Kew"),
    ];

    private readonly ObservableCollection<WaterfallPhoto> _photos = [];
    private int _seed;

    public WaterfallPanelVariedSizesDemo()
    {
        InitializeComponent();
        for (var i = 0; i < 10; i++)
        {
            AddPhoto();
        }
        Photos.ItemsSource = _photos;
        ShowSeed();
        Shuffle.Click += (_, _) =>
        {
            _seed++;
            // The panel is created from the ItemsPanelTemplate; a new seed gives a new arrangement of the same items.
            if (Photos.ItemsPanelRoot is WaterfallPanel panel)
            {
                panel.Seed = _seed;
            }
            ShowSeed();
        };
        Add.Click += (_, _) => AddPhoto();
    }

    private void AddPhoto()
    {
        var (title, place) = s_photos[_photos.Count % s_photos.Length];
        _photos.Add(new WaterfallPhoto(title, place));
    }

    private void ShowSeed() => SeedLabel.Text = $"Seed {_seed}";
}
