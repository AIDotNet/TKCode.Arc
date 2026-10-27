using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class IntegrationsEventLogDemo : UserControl
{
    private readonly List<string> _lines = new();

    public IntegrationsEventLogDemo()
    {
        InitializeComponent();
        IntegrationsDemoData.Fill(Directory);
        Directory.SimulatedConnectDuration = TimeSpan.FromMilliseconds(900);

        Directory.FilterChanged += (_, e) => Write($"Filter {e.OldFilter} → {e.NewFilter}, {e.VisibleCount} shown");
        Directory.SearchChanged += (_, e) => Write($"Search “{e.Text}”, {e.VisibleCount} shown");
        Directory.ConnectRequested += (_, e) => Write($"Connect {e.Item.Name} (attempt {e.Attempt})");
        Directory.StatusChanged += (_, e) => Write($"{e.Item.Name}: {e.OldStatus} → {e.NewStatus}");
        Directory.SyncFrequencyChanged += (_, e) => Write($"{e.Item.Name} syncs {e.NewKey} (was {e.OldKey ?? "unset"})");
        Directory.ConfigurationOpened += (_, e) => Write($"Opened {e.Item.Name}");
        Directory.ConfigurationClosed += (_, e) => Write($"Closed {e.Item.Name}");
    }

    private void Write(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 4)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }

    private void OnShowConnected(object? sender, RoutedEventArgs e) => Directory.SelectedFilter = Integrations.ConnectedFilter;

    private void OnConnectAll(object? sender, RoutedEventArgs e)
    {
        foreach (var item in Directory.Items)
        {
            _ = Directory.ConnectAsync(item);
        }
    }
}
