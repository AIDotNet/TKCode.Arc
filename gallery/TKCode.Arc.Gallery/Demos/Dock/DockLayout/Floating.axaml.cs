using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Floats the Properties pane, docks every floating pane back, and switches between windows and cards.</summary>
public partial class DockLayoutFloatingDemo : UserControl
{
    public DockLayoutFloatingDemo()
    {
        InitializeComponent();
        FloatButton.Click += OnFloat;
        DockButton.Click += OnDockAll;
        CardsSwitch.IsCheckedChanged += OnCardsChanged;
    }

    private void OnFloat(object? sender, RoutedEventArgs e)
    {
        if (Layout.Workspace.FindPane("properties") is { } pane)
        {
            Layout.Float(pane);
        }
    }

    private void OnDockAll(object? sender, RoutedEventArgs e)
    {
        foreach (var pane in Layout.Workspace.AllPanes().Where(p => p.State == DockPaneState.Floating).ToList())
        {
            Layout.Dock(pane);
        }
    }

    private void OnCardsChanged(object? sender, RoutedEventArgs e)
        => Layout.FloatingMode = CardsSwitch.IsChecked == true ? DockFloatingMode.Overlay : DockFloatingMode.Auto;
}
