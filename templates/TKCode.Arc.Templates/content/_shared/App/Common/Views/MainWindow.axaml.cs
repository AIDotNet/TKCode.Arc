using Avalonia.Controls;
#if (UsesArcWindow)
using TKCode.Arc;
#endif
#if (UsesSettings)
using ArcStarterApp.Settings;
#endif

namespace ArcStarterApp.Views;

/// <summary>The desktop window around <see cref="MainView"/>.</summary>
#if (UsesArcWindow)
public partial class MainWindow : ArcWindow
#else
public partial class MainWindow : Window
#endif
{
    public MainWindow()
    {
        InitializeComponent();
#if (UsesSettings)
        // Opens where it was last closed, at the same size, and remembers the placement when it closes.
        AppSettings.Current.TrackWindow(this);
#endif
    }
}
