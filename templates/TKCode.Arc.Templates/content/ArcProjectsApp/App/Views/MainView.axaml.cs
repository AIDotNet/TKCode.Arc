using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ArcStarterApp.Settings;
using TKCode.Arc;

namespace ArcStarterApp.Views;

/// <summary>The app's content: the sidebar and the pages. The main window shows it on the desktop.</summary>
public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        // The sidebar opens as it was left: folded to the icon rail or not.
        Nav.IsCollapsed = AppSettings.Current.IsSidebarCollapsed;
        Nav.PropertyChanged += OnNavPropertyChanged;
    }

    private void OnNavPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Sidebar.IsCollapsedProperty && AppSettings.Current.IsSidebarCollapsed != Nav.IsCollapsed)
        {
            AppSettings.Current.IsSidebarCollapsed = Nav.IsCollapsed;
            AppSettings.Current.Save();
        }
    }

    private void OnToggleSidebar(object? sender, RoutedEventArgs e) => Nav.ToggleCollapsed();
}
