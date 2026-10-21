using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class AlertMorphInPlaceDemo : UserControl
{
    public AlertMorphInPlaceDemo()
    {
        InitializeComponent();
    }

    // One alert instance changes tone, title and content, so the icon swaps and the height springs.
    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        Status.Tone = AlertTone.Info;
        Status.Title = "Saving changes";
        Status.Content = "Uploading 3 files to the workspace.";
        await Task.Delay(1400);
        Status.Tone = AlertTone.Success;
        Status.Title = "All changes saved";
        Status.Content = "Everyone in the workspace now sees the latest version, including the comments you left on the draft.";
    }

    private void OnFailClick(object? sender, RoutedEventArgs e)
    {
        Status.Tone = AlertTone.Danger;
        Status.Title = "Couldn't save";
        Status.Content = "Check your connection and try again.";
    }
}
