using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Your own entity for a sidebar page: any class works, with fields of your own such as <see cref="CanAdd"/>.</summary>
public sealed class SidebarMenuNode
{
    public string Key { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public bool CanAdd { get; init; }

    public ObservableCollection<SidebarMenuNode> Children { get; } = new();
}

public partial class SidebarItemsSourceDemo : UserControl
{
    private readonly SidebarMenuNode _projects = new() { Key = "projects", Title = "Projects", CanAdd = true };

    public SidebarItemsSourceDemo()
    {
        InitializeComponent();
        _projects.Children.Add(new SidebarMenuNode { Key = "projects/atlas", Title = "Atlas" });
        Nav.ItemsSource = new ObservableCollection<SidebarMenuNode>
        {
            new() { Key = "home", Title = "Home" },
            new() { Key = "inbox", Title = "Inbox" },
            _projects,
            new() { Key = "settings", Title = "Settings" }
        };
    }

    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: SidebarMenuNode node })
        {
            var index = node.Children.Count + 1;
            node.Children.Add(new SidebarMenuNode { Key = $"{node.Key}/new-{index}", Title = $"New {node.Title} {index}" });
            Log.Text = $"Added a child to {node.Title}.";
        }
    }
}
