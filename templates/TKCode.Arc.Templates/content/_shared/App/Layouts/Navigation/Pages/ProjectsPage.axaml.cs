using Avalonia.Controls;
#if (!UsesMvvm)
using System.Collections.ObjectModel;
using Avalonia.Interactivity;
using ArcStarterApp.Models;
#endif

namespace ArcStarterApp.Pages;

public partial class ProjectsPage : UserControl
{
#if (!UsesMvvm)
    // An ObservableCollection: the list shows rows added later (the "New project" button) without being reset.
    private readonly ObservableCollection<Project> _projects = new(SampleProjects.All);

#endif
    public ProjectsPage()
    {
        InitializeComponent();
#if (!UsesMvvm)
        ProjectList.ItemsSource = _projects;
#endif
    }
#if (!UsesMvvm)

    private void OnNewProject(object? sender, RoutedEventArgs e) => _projects.Add(SampleProjects.CreateNew(_projects.Count + 1));
#endif
}
