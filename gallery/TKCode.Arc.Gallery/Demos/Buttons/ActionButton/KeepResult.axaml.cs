using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActionButtonKeepResultDemo : UserControl
{
    public ActionButtonKeepResultDemo()
    {
        InitializeComponent();
        Settings.AsyncAction = () => Task.Delay(700);
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        Settings.Reset();
    }
}
