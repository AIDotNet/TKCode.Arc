using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MultiSelectChipsDemo : UserControl
{
    public MultiSelectChipsDemo()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Chosen.Text = Labels.SelectedValues.Count == 0
            ? "Values: none"
            : $"Values: {string.Join(", ", Labels.SelectedValues)}";
    }
}
