using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class MobileSearchbarBasicDemo : UserControl
{
    public MobileSearchbarBasicDemo()
    {
        InitializeComponent();
    }

    private void OnSubmitted(object? sender, RoutedEventArgs e)
        => Result.Text = string.IsNullOrWhiteSpace(Query.Text) ? "Type something to search for." : $"Searching for “{Query.Text}”…";
}
