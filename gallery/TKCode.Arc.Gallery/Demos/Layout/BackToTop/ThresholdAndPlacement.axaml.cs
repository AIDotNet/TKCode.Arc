using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class BackToTopThresholdAndPlacementDemo : UserControl
{
    private static readonly BackToTopPlacement[] s_corners =
    [
        BackToTopPlacement.BottomRight, BackToTopPlacement.BottomLeft, BackToTopPlacement.TopRight, BackToTopPlacement.TopLeft
    ];

    private static readonly double[] s_thresholds = [0, 200, 600];

    public BackToTopThresholdAndPlacementDemo()
    {
        InitializeComponent();
        Posts.ItemsSource = Enumerable.Range(1, 40).Select(i => $"Post {i}: notes from the week, shared with the team.").ToList();
        Corner.SelectionChanged += (_, _) => BackToTop.SetPlacement(Feed, s_corners[Math.Max(0, Corner.SelectedIndex)]);
        Distance.SelectionChanged += (_, _) => BackToTop.SetThreshold(Feed, s_thresholds[Math.Max(0, Distance.SelectedIndex)]);
        Feed.ScrollChanged += (_, _) => Offset.Text = $"Scrolled {Feed.Offset.Y:0} px";
    }
}
