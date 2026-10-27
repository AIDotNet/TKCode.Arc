using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ConnectedScatterEventsDemo : UserControl
{
    private readonly List<string> _lines = new();

    public ConnectedScatterEventsDemo()
    {
        InitializeComponent();
        Scatter.XAxis = ConnectedScatterDemoData.CostAxis();
        Scatter.YAxis = ConnectedScatterDemoData.ValueAxis();
        Scatter.Series.AddRange(ConnectedScatterDemoData.Channels());

        Play.Click += (_, _) => Scatter.Play();
        Pause.Click += (_, _) => Scatter.Pause();
        Replay.Click += (_, _) => Scatter.ReplayDrawIn();
        Middle.Click += (_, _) => Scatter.GoToMonth(8);

        Scatter.MonthChanged += (_, e) =>
        {
            var point = e.Point;
            Write(point is null
                ? $"Month {e.Index + 1}"
                : $"Month {e.Index + 1} of {e.Series?.Name}: {point.Label}, CAC ${point.X:N0}, LTV ${point.Y:N0}, {point.Y / point.X:0.0}x");
        };
        Scatter.SeriesChanged += (_, e) => Write($"Series: {e.Series?.Name}");
    }

    private void Write(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 5)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }
}
