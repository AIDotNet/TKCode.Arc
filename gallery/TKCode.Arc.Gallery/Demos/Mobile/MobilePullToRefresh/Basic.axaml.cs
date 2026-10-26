using Avalonia.Controls;
using Avalonia.Threading;
using TKCode.Arc.Mobile;

namespace TKCode.Arc.Gallery.Demos;

public partial class MobilePullToRefreshBasicDemo : UserControl
{
    private int _refreshes;

    public MobilePullToRefreshBasicDemo()
    {
        InitializeComponent();
        for (var i = 1; i <= 6; i++)
        {
            Rows.Items.Add(new ListItem { Content = $"Message {i}", Subtitle = "Tap to open" });
        }
        Refresher.RefreshRequested += OnRefreshRequested;
    }

    private void OnRefreshRequested(object? sender, PullRefreshRequestedEventArgs e)
    {
        // A deferral keeps the spinner going until the (simulated) request ends, then the content springs back.
        var deferral = e.GetDeferral();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _refreshes++;
            Rows.Items.Insert(0, new ListItem { Content = $"New message {_refreshes}", Subtitle = "Just now" });
            Status.Text = $"Refreshed {_refreshes} time(s). Pull down again.";
            deferral.Dispose();
        };
        timer.Start();
    }
}
