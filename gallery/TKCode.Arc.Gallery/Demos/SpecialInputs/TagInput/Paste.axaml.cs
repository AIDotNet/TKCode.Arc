using Avalonia.Controls;
using Avalonia.Interactivity;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Gallery.Demos;

public partial class TagInputPasteDemo : UserControl
{
    public TagInputPasteDemo()
    {
        InitializeComponent();
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        await ArcClipboard.CopyTextAsync(CopySample, "red, green, blue, teal");
    }
}
