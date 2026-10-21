using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ChipGroupFilterChipsDemo : UserControl
{
    public ChipGroupFilterChipsDemo()
    {
        InitializeComponent();
        Topics.Selection.Select(1);
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Chosen.Text = Topics.SelectedValues.Count == 0
            ? "Values: none"
            : $"Values: {string.Join(", ", Topics.SelectedValues)}";
    }
}
