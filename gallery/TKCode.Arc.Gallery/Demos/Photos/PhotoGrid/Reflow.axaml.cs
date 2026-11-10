using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class PhotoGridReflowDemo : UserControl
{
    private readonly ObservableCollection<PhotoGridPhoto> _photos = PhotosDemoData.NewAlbum();
    private int _next = 10;

    public PhotoGridReflowDemo()
    {
        InitializeComponent();
        Grid.ItemsSource = _photos;
    }

    private void OnAdd(object? sender, RoutedEventArgs e) => _photos.Insert(Math.Min(2, _photos.Count), PhotosDemoData.GridPhoto(_next++));

    private void OnRemove(object? sender, RoutedEventArgs e)
    {
        if (_photos.Count > 0)
        {
            _photos.RemoveAt(0);
        }
    }

    private void OnShuffle(object? sender, RoutedEventArgs e)
    {
        // A fixed rotation rather than a random order, so the demo is the same every time.
        if (_photos.Count > 1)
        {
            _photos.Move(_photos.Count - 1, 0);
            _photos.Move(_photos.Count - 1, 3 % _photos.Count);
        }
    }

    private void OnLayout(object? sender, RoutedEventArgs e) =>
        Grid.Layout = Grid.Layout == PhotoGridLayout.Justified ? PhotoGridLayout.Masonry : PhotoGridLayout.Justified;
}
