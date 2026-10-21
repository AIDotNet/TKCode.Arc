using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ToastLocalToastDemo : UserControl
{
    public ToastLocalToastDemo()
    {
        InitializeComponent();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        Saved.Title = "Changes saved";
        Saved.Description = "Synced to all your devices.";
        Saved.IsOpen = true;
    }

    // The text changes while the toast is open: the new text rises in and the height follows.
    private void OnChangeTextClick(object? sender, RoutedEventArgs e)
    {
        Saved.IsOpen = true;
        Saved.Title = Saved.Title == "Changes saved" ? "Draft published" : "Changes saved";
        Saved.Description = Saved.Description is null ? "Readers will see it in a few seconds, and subscribers get an email." : null;
    }

    private void OnClosed(object? sender, ToastClosedEventArgs e) => Log.Text = $"Closed: {e.Reason}";
}
