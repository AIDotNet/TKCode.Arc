using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class EmptyStateFirstUseDemo : UserControl
{
    public EmptyStateFirstUseDemo()
    {
        InitializeComponent();
    }

    private void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        Empty.Icon = ArcIcons.Check;
        Empty.Title = "Project created";
        Empty.Description = "Invite your team or start adding files whenever you're ready.";
        Empty.Action = null;
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        Empty.Icon = null;
        Empty.Title = "No projects yet";
        Empty.Description = "Projects keep your files, people and conversations in one place.";
        Empty.Action = Create;
    }
}
