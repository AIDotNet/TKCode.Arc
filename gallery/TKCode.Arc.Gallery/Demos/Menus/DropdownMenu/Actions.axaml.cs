using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DropdownMenuActionsDemo : UserControl
{
    public DropdownMenuActionsDemo()
    {
        InitializeComponent();
    }

    private void OnItemClicked(object? sender, MenuEntryClickedEventArgs e) => Status.Text = $"Selected: {e.Header}";
}
