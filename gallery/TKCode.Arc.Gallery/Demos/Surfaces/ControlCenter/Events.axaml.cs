using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ControlCenterEventsDemo : UserControl
{
    private readonly List<string> _lines = [];

    public ControlCenterEventsDemo()
    {
        InitializeComponent();
        ControlCenterSamples.Apply(Center);
    }

    private void Add(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 8)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join("\n", _lines);
    }

    private void OnStateChanged(object? sender, ControlCenterStateChangedEventArgs e)
    {
        var s = e.State;
        var focus = s.Focus is { } f ? $"{f.ModeId} {f.Minutes} min" : "no session";
        var on = s.Channels.Count(c => c.Value);
        Add($"{e.Reason}: {focus}, alerts {(s.NotificationsOn ? "on" : "off")} ({on}/{s.Channels.Count}), " +
            $"presenting {(s.Presenting ? "on" : "off")}, volume {s.Volume}, text {s.TextSize}%");
    }

    private void OnDetailOpened(object? sender, ControlCenterDetailEventArgs e) => Add($"Opened {e.Detail}");

    private void OnDetailClosed(object? sender, ControlCenterDetailEventArgs e) => Add($"Closed {e.Detail}");
}
