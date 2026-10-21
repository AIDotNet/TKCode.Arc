using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class BreadcrumbClientSidePathDemo : UserControl
{
    private static readonly string[] Folders = ["Arc", "src", "components", "breadcrumb"];

    // A file browser: the path lives in local state, crumbs truncate it and folders extend it.
    private readonly ObservableCollection<string> _path = new() { "Home", "Projects" };

    public BreadcrumbClientSidePathDemo()
    {
        InitializeComponent();
        Files.ItemsSource = _path;
    }

    private void OnItemClick(object? sender, BreadcrumbItemClickEventArgs e)
    {
        while (_path.Count > e.Index + 1)
        {
            _path.RemoveAt(_path.Count - 1);
        }
    }

    private void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        // Truncating to the first crumb leaves one segment, so the offset must stay non-negative.
        _path.Add(Folders[(_path.Count + Folders.Length - 2) % Folders.Length]);
    }

    private void OnUpClick(object? sender, RoutedEventArgs e)
    {
        if (_path.Count > 1)
        {
            _path.RemoveAt(_path.Count - 1);
        }
    }
}
