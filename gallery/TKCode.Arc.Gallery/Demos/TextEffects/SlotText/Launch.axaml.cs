using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SlotTextLaunchDemo : UserControl
{
    private static readonly string[] Words = ["Ready", "Steady", "Launch", "Go live", "Ready"];

    private int _index;

    public SlotTextLaunchDemo()
    {
        InitializeComponent();
    }

    private void OnNext(object? sender, RoutedEventArgs e)
    {
        Word.Value = Words[_index = (_index + 1) % Words.Length];
    }
}
