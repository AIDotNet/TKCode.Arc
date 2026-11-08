using CommunityToolkit.Mvvm.ComponentModel;
using ArcStarterApp.Navigation;
#if (UsesI18n)
using TKCode.Arc.Localization;
#endif

namespace ArcStarterApp.ViewModels;

/// <summary>
/// The main view: which page is shown. The navigation's selected key and the page host's Page are bound to it, so
/// navigating from code is setting <see cref="SelectedKey"/>.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _selectedKey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private AppPage? _currentPage;

    public MainViewModel()
    {
        SelectedKey = AppPages.Home;
#if (UsesI18n)
        // The title is read in the current language: read it again when the language changes. The main view model lives
        // as long as the app, so the subscription does too.
        ArcLanguage.Changed += (_, _) => OnPropertyChanged(nameof(Title));
#endif
    }

    /// <summary>The shown page's title, for the top bar.</summary>
    public string? Title => CurrentPage?.Title;

    partial void OnSelectedKeyChanged(string? value)
    {
        // A key that names no page (a row without one) keeps the current page.
        if (AppPages.Find(value) is { } page)
        {
            CurrentPage = page;
        }
    }
}
