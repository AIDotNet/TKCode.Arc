using Avalonia.Controls;

namespace ArcStarterApp.Views;

/// <summary>The app's content: the main window shows it on the desktop, and the page itself in a browser.</summary>
public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
#if (UsesMvvm)
        // The page shown follows MainViewModel.SelectedKey, which the page switch's SelectedValue is bound to.
#else
        // The page switch's SelectedValue is the key of a page in Navigation/AppPages.cs.
        Nav.SelectionChanged += (_, _) => PageView.Navigate(Nav.SelectedValue as string);
        PageView.Navigate(Nav.SelectedValue as string);
#endif
    }
}
