using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ReorderableListAutoScrollDemo : UserControl
{
    public ReorderableListAutoScrollDemo()
    {
        InitializeComponent();
        var tracks = new ObservableCollection<ReorderableListEntry>();
        for (var i = 1; i <= 24; i++)
        {
            tracks.Add(new ReorderableListEntry($"t{i}", $"Track {i:00}", $"{2 + i % 4}:{(i * 7) % 60:00}"));
        }
        Tracks.ItemsSource = tracks;
    }
}
