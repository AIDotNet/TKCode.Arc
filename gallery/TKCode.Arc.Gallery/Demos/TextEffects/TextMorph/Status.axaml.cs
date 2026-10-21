using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class TextMorphStatusDemo : UserControl
{
    private static readonly string[] Words = ["Deploy", "Deploying", "Deployed", "Rolled back"];

    private int _index;

    public TextMorphStatusDemo()
    {
        InitializeComponent();
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        Status.Text = Words[_index = (_index + 1) % Words.Length];
    }
}
