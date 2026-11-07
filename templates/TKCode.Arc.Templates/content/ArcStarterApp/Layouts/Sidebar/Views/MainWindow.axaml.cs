using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TKCode.Arc;

namespace ArcStarterApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        PageView.Navigated += (_, _) => PageTitle.Text = PageView.Current?.Title;
        // ItemInvoked fires for every row chosen by click, keyboard or the rail's menus.
        Nav.ItemInvoked += (_, e) => PageView.Navigate(e.Item.Key);
        // SelectedKey can also change from code (a link on a page, a deep link); follow it.
        Nav.PropertyChanged += OnNavPropertyChanged;
        PageView.Navigate(Nav.SelectedKey);
    }

    private void OnNavPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Sidebar.SelectedKeyProperty)
        {
            PageView.Navigate(Nav.SelectedKey);
        }
    }

    private void OnToggleSidebar(object? sender, RoutedEventArgs e) => Nav.ToggleCollapsed();
}
