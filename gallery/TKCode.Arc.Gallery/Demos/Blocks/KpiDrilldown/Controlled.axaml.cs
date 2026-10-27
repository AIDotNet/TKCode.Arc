using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class KpiDrilldownControlledDemo : UserControl
{
    private readonly List<string> _lines = new();

    public KpiDrilldownControlledDemo()
    {
        InitializeComponent();
        Block.EndDate = KpiDrilldownDemoData.End;
        Block.Dimensions.AddRange(KpiDrilldownDemoData.Dimensions());
        Block.Metrics.AddRange(KpiDrilldownDemoData.Metrics());
        Write("Choose a card, a range or Compare.");
    }

    private void OnOpenRefunds(object? sender, RoutedEventArgs e) => Block.Open("refunds");

    private void OnOpenRunRate(object? sender, RoutedEventArgs e) => Block.Open("arr");

    private void OnClose(object? sender, RoutedEventArgs e) => Block.Close();

    private void OnNinety(object? sender, RoutedEventArgs e) => Block.RangeDays = 90;

    private void OnSelectedChanged(object? sender, KpiSelectionChangedEventArgs e) =>
        Write(e.NewKey is null ? "SelectedChanged: back to the cards" : $"SelectedChanged: {e.OldKey ?? "none"} → {e.NewKey}");

    private void OnRangeChanged(object? sender, KpiRangeChangedEventArgs e) => Write($"RangeChanged: {e.OldDays} → {e.NewDays} days");

    private void OnCompareChanged(object? sender, KpiCompareChangedEventArgs e) => Write($"CompareChanged: {(e.IsEnabled ? "on" : "off")}");

    private void OnDimensionChanged(object? sender, KpiSelectionChangedEventArgs e) => Write($"DimensionChanged: {e.NewKey}");

    private void Write(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 4)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }
}
