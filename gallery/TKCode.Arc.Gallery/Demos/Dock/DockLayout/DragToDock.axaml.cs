using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Remembers the layout as it starts and loads it back when Reset layout is clicked.</summary>
public partial class DockLayoutDragToDockDemo : UserControl
{
    private readonly string _initial;

    public DockLayoutDragToDockDemo()
    {
        InitializeComponent();
        _initial = Layout.SaveLayout();
        ResetButton.Click += OnReset;
    }

    private void OnReset(object? sender, RoutedEventArgs e) => Layout.LoadLayout(_initial);
}
