using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActionMorphPreviewDemo : UserControl
{
    public ActionMorphPreviewDemo()
    {
        InitializeComponent();
    }

    private void OnCreate(object? sender, RoutedEventArgs e)
    {
        if (ListName.Text is { Length: > 0 } name)
        {
            Status.Text += ", " + name;
        }
    }

    private void OnCollapsed(object? sender, RoutedEventArgs e) => ListName.Text = string.Empty;
}
