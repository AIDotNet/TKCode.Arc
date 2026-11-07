using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class StretchRefreshErrorsDemo : UserControl
{
    private int _attempt;

    public StretchRefreshErrorsDemo()
    {
        InitializeComponent();
        Feed.ItemsSource = new[]
        {
            "Weekly digest is ready",
            "Your export finished",
            "Two seats were added to the team",
            "Password changed on a new device"
        };
        Feed.Labels = new StretchRefreshLabels
        {
            PullHint = "Pull to check mail",
            ReleaseHint = "Let go to check",
            Refreshing = "Checking",
            Done = "No new mail",
            Error = "Mail server unreachable",
            RefreshButton = "Check mail"
        };
        Feed.RefreshHandler = async ct =>
        {
            await Task.Delay(800, ct);
            if (_attempt++ % 2 == 0)
            {
                throw new InvalidOperationException("The mail server did not answer.");
            }
            Feed.Subtitle = "Last checked just now";
            return null;
        };
    }
}
