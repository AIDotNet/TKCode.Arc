using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DropdownMenuLabelAndTogglesDemo : UserControl
{
    public DropdownMenuLabelAndTogglesDemo()
    {
        InitializeComponent();
    }

    // A new label rises in while the trigger width springs.
    private void OnSortClicked(object? sender, MenuEntryClickedEventArgs e) => Sort.Label = $"Sort: {e.Header}";
}
