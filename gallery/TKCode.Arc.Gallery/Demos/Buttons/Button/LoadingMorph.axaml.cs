using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ButtonLoadingMorphDemo : UserControl
{
    public ButtonLoadingMorphDemo()
    {
        InitializeComponent();
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        Save.IsLoading = true;
        await Task.Delay(900);
        Save.IsLoading = false;
        Save.Content = "Saved";
        await Task.Delay(1600);
        Save.Content = "Save";
    }
}
