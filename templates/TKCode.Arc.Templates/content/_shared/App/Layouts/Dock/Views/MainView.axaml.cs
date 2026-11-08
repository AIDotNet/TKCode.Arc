using Avalonia.Controls;
using Avalonia.Interactivity;
#if (UsesSettings)
using ArcStarterApp.Settings;
#endif

namespace ArcStarterApp.Views;

/// <summary>The app's content: the main window shows it on the desktop, and the page itself in a browser.</summary>
public partial class MainView : UserControl
{
    // The layout as MainView.axaml declares it, for "Reset layout".
    private readonly string _declaredLayout;

    public MainView()
    {
        InitializeComponent();
        _declaredLayout = Dock.SaveLayout();
#if (UsesSettings)
        // The panes come back where they were left. LoadLayout leaves the layout as it is when the saved one is not
        // usable (an older format, or text that is not a layout).
        if (AppSettings.Current.DockLayout is { } saved)
        {
            Dock.LoadLayout(saved);
        }
        // Every move, resize, close or float is saved.
        Dock.LayoutChanged += (_, _) =>
        {
            AppSettings.Current.DockLayout = Dock.SaveLayout();
            AppSettings.Current.Save();
        };
#endif
    }

    private void OnResetLayout(object? sender, RoutedEventArgs e) => Dock.LoadLayout(_declaredLayout);
}
