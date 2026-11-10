using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SplitBarLiveSharesDemo : UserControl
{
    private readonly ObservableCollection<SplitBarSegment> _segments =
    [
        new SplitBarSegment("Rent", 1450),
        new SplitBarSegment("Groceries", 520),
        new SplitBarSegment("Transport", 180),
        new SplitBarSegment("Savings", 600)
    ];

    private readonly SplitBarSegment _travel = new("Travel", 340);
    private readonly Random _random = new(7);

    public SplitBarLiveSharesDemo()
    {
        InitializeComponent();
        Bar.ItemsSource = _segments;
    }

    private void OnShuffle(object? sender, RoutedEventArgs e)
    {
        foreach (var segment in _segments)
        {
            // Values change in place: the bar springs every part to its new share.
            segment.Value = Math.Round(segment.Value * (0.6 + _random.NextDouble() * 0.8) / 10) * 10;
        }
    }

    private void OnToggle(object? sender, RoutedEventArgs e)
    {
        if (_segments.Contains(_travel))
        {
            _segments.Remove(_travel);
            ToggleButton.Content = "Add travel";
        }
        else
        {
            _segments.Insert(2, _travel);
            ToggleButton.Content = "Remove travel";
        }
    }

    private void OnSegmentInvoked(object? sender, SplitBarSegmentEventArgs e)
    {
        Log.Text = $"{e.Segment.Label}: {Bar.FormatValue(e.Segment.Value)}, {e.Share:P0} of the budget.";
    }
}
