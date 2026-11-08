using CommunityToolkit.Mvvm.ComponentModel;
using ArcStarterApp.Navigation;

namespace ArcStarterApp.ViewModels;

/// <summary>The main view: which page is shown. The top bar's page switch and the page host are bound to it.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _selectedKey;

    [ObservableProperty]
    private AppPage? _currentPage;

    public MainViewModel()
    {
        SelectedKey = AppPages.Overview;
    }

    partial void OnSelectedKeyChanged(string? value)
    {
        if (AppPages.Find(value) is { } page)
        {
            CurrentPage = page;
        }
    }
}
