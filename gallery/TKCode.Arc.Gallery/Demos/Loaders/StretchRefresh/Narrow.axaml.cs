using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class StretchRefreshNarrowDemo : UserControl
{
    private readonly StretchRefreshSampleFeed _feed = new();

    public StretchRefreshNarrowDemo()
    {
        InitializeComponent();
        Feed.Title = StretchRefreshSamples.Title;
        Feed.Subtitle = _feed.Subtitle;
        Feed.ItemsSource = _feed.Items;
        Feed.RefreshHandler = async ct =>
        {
            var result = await _feed.RefreshAsync(ct);
            Feed.Subtitle = _feed.Subtitle;
            return result;
        };
    }
}
