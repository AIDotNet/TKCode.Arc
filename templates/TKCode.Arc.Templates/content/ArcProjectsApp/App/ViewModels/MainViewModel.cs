using CommunityToolkit.Mvvm.ComponentModel;
using ArcStarterApp.Navigation;
using ArcStarterApp.Services;

namespace ArcStarterApp.ViewModels;

/// <summary>The main view: which page is shown. The sidebar's selected key and the page host are bound to it.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _selectedKey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private AppPage? _currentPage;

    public MainViewModel(IProjectService projects)
    {
        ProjectName = projects.ProjectName;
        SelectedKey = AppPages.Board;
    }

    public string ProjectName { get; }

    /// <summary>The shown page's title, for the top bar.</summary>
    public string? Title => CurrentPage?.Title;

    partial void OnSelectedKeyChanged(string? value)
    {
        if (AppPages.Find(value) is { } page)
        {
            CurrentPage = page;
        }
    }
}
