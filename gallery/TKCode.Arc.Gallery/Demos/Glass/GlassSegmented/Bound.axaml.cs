using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class GlassSegmentedBoundDemo : UserControl
{
    public GlassSegmentedBoundDemo()
    {
        InitializeComponent();
        Speed.ItemsSource = new[] { "0.5×", "1×", "1.5×", "2×" };
        Speed.SelectedIndex = 1;
        Speed.SelectionChanged += (_, _) => Show();
        Show();
    }

    private void Show() => Result.Text = $"Playback speed: {Speed.SelectedItem}";
}
