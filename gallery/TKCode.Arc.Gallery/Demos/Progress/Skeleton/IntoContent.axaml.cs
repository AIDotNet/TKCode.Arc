using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SkeletonIntoContentDemo : UserControl
{
    public SkeletonIntoContentDemo()
    {
        InitializeComponent();
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e)
    {
        Post.IsLoading = !Post.IsLoading;
        Toggle.Content = Post.IsLoading ? "Finish loading" : "Load again";
    }
}
