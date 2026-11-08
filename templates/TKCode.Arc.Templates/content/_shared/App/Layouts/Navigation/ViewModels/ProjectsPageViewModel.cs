using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArcStarterApp.Models;
#if (UsesDi)
using ArcStarterApp.Services;
#endif

namespace ArcStarterApp.ViewModels;

/// <summary>The Projects page: the list and the "New project" action.</summary>
public sealed partial class ProjectsPageViewModel : ObservableObject
{
#if (UsesDi)
    private readonly IProjectStore _store;

    /// <summary>The container passes the registered <see cref="IProjectStore"/> (Services/AppServices.cs).</summary>
    public ProjectsPageViewModel(IProjectStore store)
    {
        _store = store;
        Projects = new ObservableCollection<Project>(store.GetAll());
    }

    /// <summary>The rows; an ObservableCollection, so the list shows rows added later without being reset.</summary>
    public ObservableCollection<Project> Projects { get; }

    [RelayCommand]
    private void AddProject() => Projects.Add(_store.Create(Projects.Count + 1));
#else
    /// <summary>The rows; an ObservableCollection, so the list shows rows added later without being reset.</summary>
    public ObservableCollection<Project> Projects { get; } = new(SampleProjects.All);

    [RelayCommand]
    private void AddProject() => Projects.Add(SampleProjects.CreateNew(Projects.Count + 1));
#endif
}
