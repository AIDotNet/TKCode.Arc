using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActionMorphCornerDemo : UserControl
{
    public ActionMorphCornerDemo()
    {
        InitializeComponent();
    }

    private void OnSubmitted(object? sender, ActionMorphSubmittedEventArgs e)
    {
        var submission = e.Submission;
        Log.Text = submission.Choice is { } choice
            ? $"Sent: {submission.Action.Label} ({choice})"
            : $"Sent: {submission.Action.Label}";
    }
}
