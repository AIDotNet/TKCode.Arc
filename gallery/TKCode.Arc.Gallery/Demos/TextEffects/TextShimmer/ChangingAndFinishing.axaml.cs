using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class TextShimmerChangingAndFinishingDemo : UserControl
{
    private static readonly string[] Steps = ["Reading 14 files", "Drafting a plan", "Writing the summary"];

    private int _index;

    public TextShimmerChangingAndFinishingDemo()
    {
        InitializeComponent();
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        Summary.IsActive = true;
        Summary.Text = Steps[_index = (_index + 1) % Steps.Length];
    }

    private void OnFinishClick(object? sender, RoutedEventArgs e)
    {
        Summary.Text = "Summary ready";
        Summary.IsActive = false;
    }
}
