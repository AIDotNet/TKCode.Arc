using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SplitButtonResultInPlaceDemo : UserControl
{
    private int _copies;

    public SplitButtonResultInPlaceDemo()
    {
        InitializeComponent();
    }

    // Copy page answers Copied in place: the shared "Cop" stays put while the rest of the label morphs.
    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        var current = ++_copies;
        Copy.Label = "Copied";
        Copy.Icon = ArcIcons.Check;
        await Task.Delay(1600);
        if (current == _copies)
        {
            Copy.Label = "Copy page";
            Copy.Icon = ArcIcons.Copy;
        }
    }
}
