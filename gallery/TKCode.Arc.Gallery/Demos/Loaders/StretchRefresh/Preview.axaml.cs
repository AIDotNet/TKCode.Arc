using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class StretchRefreshPreviewDemo : UserControl
{
    private readonly StretchRefreshSampleFeed _feed = new();

    public StretchRefreshPreviewDemo()
    {
        InitializeComponent();
        Feed.Title = StretchRefreshSamples.Title;
        Feed.Subtitle = _feed.Subtitle;
        Feed.ItemsSource = _feed.Items;
        Feed.Labels = new StretchRefreshLabels { RefreshButton = StretchRefreshSamples.RefreshLabel };
        Feed.RefreshHandler = _feed.RefreshAsync;
        _feed.PropertyChanged += OnFeedChanged;
    }

    private void OnFeedChanged(object? sender, PropertyChangedEventArgs e)
    {
        Feed.Subtitle = _feed.Subtitle;
        ResetButton.IsEnabled = _feed.IsChanged;
    }

    private void OnReset(object? sender, RoutedEventArgs e) => _feed.Reset();
}
