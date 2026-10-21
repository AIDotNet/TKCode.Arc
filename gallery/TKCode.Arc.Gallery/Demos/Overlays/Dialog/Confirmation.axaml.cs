using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class DialogConfirmationDemo : UserControl
{
    public DialogConfirmationDemo()
    {
        InitializeComponent();
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        var confirmed = await Dialog.ConfirmAsync(Delete, "Delete project?",
            "This permanently deletes Atlas redesign and its 14 files. This cannot be undone.", "Delete", isDanger: true);
        Status.Text = confirmed ? "Deleted" : "Kept";
    }
}
