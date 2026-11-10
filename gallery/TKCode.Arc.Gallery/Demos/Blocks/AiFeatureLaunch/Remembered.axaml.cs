using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiFeatureLaunchRememberedDemo : UserControl
{
    public AiFeatureLaunchRememberedDemo()
    {
        InitializeComponent();
        Launch.Highlights = ["Matches how you usually write", "Never sends without you", "Turn it off any time in Settings"];
        Launch.Dismissed += (_, e) => Log.Text = $"Closed: {e.Reason}";
        ShowAgain.Click += (_, _) =>
        {
            Log.Text = null;
            Launch.Show();
        };
    }
}
