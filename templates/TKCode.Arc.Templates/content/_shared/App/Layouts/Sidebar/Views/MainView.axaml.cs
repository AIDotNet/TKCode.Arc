using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
#if (UsesSettings)
using ArcStarterApp.Settings;
#endif
using TKCode.Arc;
#if (UsesI18n && !UsesMvvm)
using TKCode.Arc.Localization;
#endif

namespace ArcStarterApp.Views;

/// <summary>The app's content: the main window shows it on the desktop, and the page itself in a browser.</summary>
public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
#if (UsesSettings)
        // The sidebar opens as it was left: folded to the icon rail or not.
        Nav.IsCollapsed = AppSettings.Current.IsSidebarCollapsed;
#endif
#if (UsesMvvm)
        // The page shown follows MainViewModel.SelectedKey, which the sidebar's SelectedKey is bound to.
#else
        PageView.Navigated += (_, _) => PageTitle.Text = PageView.Current?.Title;
#if (UsesI18n)
        // The title is read in the current language: read it again when the language changes.
        ArcLanguage.Changed += (_, _) => PageTitle.Text = PageView.Current?.Title;
#endif
        // ItemInvoked fires for every row chosen by click, keyboard or the rail's menus.
        Nav.ItemInvoked += (_, e) => PageView.Navigate(e.Item.Key);
        PageView.Navigate(Nav.SelectedKey);
#endif
#if (!UsesMvvm || UsesSettings)
        Nav.PropertyChanged += OnNavPropertyChanged;
#endif
    }
#if (!UsesMvvm || UsesSettings)

    private void OnNavPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
#if (!UsesMvvm)
        // SelectedKey can also change from code (a link on a page, a deep link); follow it.
        if (e.Property == Sidebar.SelectedKeyProperty)
        {
            PageView.Navigate(Nav.SelectedKey);
        }
#endif
#if (UsesSettings)
        if (e.Property == Sidebar.IsCollapsedProperty && AppSettings.Current.IsSidebarCollapsed != Nav.IsCollapsed)
        {
            AppSettings.Current.IsSidebarCollapsed = Nav.IsCollapsed;
            AppSettings.Current.Save();
        }
#endif
    }
#endif

    private void OnToggleSidebar(object? sender, RoutedEventArgs e) => Nav.ToggleCollapsed();
}
