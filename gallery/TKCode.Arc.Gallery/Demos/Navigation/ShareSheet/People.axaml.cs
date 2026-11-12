using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ShareSheetPeopleDemo : UserControl
{
    public ShareSheetPeopleDemo()
    {
        InitializeComponent();
    }

    private void OnSent(object? sender, ShareSheetSentEventArgs e) =>
        Log.Text = "Sent to " + string.Join(", ", e.Submission.People.Select(p => p.ShortName));

    private void OnUndone(object? sender, ShareSheetSentEventArgs e) => Log.Text = "Taken back";
}
