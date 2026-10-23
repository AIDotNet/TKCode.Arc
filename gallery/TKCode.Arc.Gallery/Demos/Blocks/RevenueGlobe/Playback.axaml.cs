using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class RevenueGlobePlaybackDemo : UserControl
{
    private int _lastSecond = -1;

    public RevenueGlobePlaybackDemo()
    {
        InitializeComponent();
        RevenueGlobeSampleDay.Apply(Globe);
        ShowState(Globe.Time, Globe.PlaybackState);
    }

    private void OnFastReplay(object? sender, RoutedEventArgs e)
    {
        Globe.ReplaySpeed = 3000;
        Globe.Replay();
    }

    private void OnNine(object? sender, RoutedEventArgs e) => Globe.SeekTo(RevenueGlobeSampleDay.Day.AddHours(9));

    private void OnTimeChanged(object? sender, RevenueGlobeTimeChangedEventArgs e)
    {
        // TimeChanged fires every frame while replaying: update the text once per simulated second at most.
        var second = (int)e.Time.TimeOfDay.TotalSeconds;
        if (second != _lastSecond)
        {
            _lastSecond = second;
            ShowState(e.Time, e.PlaybackState);
        }
    }

    private void ShowState(DateTimeOffset time, RevenueGlobePlaybackState state) =>
        Status.Text = $"{state} · {time:HH:mm:ss} UTC";
}
