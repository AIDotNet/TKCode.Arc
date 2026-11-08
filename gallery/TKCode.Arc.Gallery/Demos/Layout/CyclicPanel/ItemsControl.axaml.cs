using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CyclicPanelItemsControlDemo : UserControl
{
    private readonly ObservableCollection<string> _songs = ["Blue in Green", "So What", "Freddie Freeloader", "All Blues", "Flamenco Sketches", "Naima"];
    private int _added;

    public CyclicPanelItemsControlDemo()
    {
        InitializeComponent();
        Items.ItemsSource = _songs;
        Add.Click += (_, _) => _songs.Add($"Track {++_added}");
        Remove.Click += (_, _) =>
        {
            if (_songs.Count > 0)
            {
                _songs.RemoveAt(_songs.Count - 1);
            }
        };
        _songs.CollectionChanged += (_, _) => UpdateStatus();
        UpdateStatus();
    }

    private void UpdateStatus() =>
        Status.Text = _songs.Count > 4
            ? $"{_songs.Count} items: the loop is seamless."
            : $"{_songs.Count} items for 4 slots: each item shows once, the rest of the slots stay empty.";
}
