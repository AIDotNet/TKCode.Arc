using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ExplodedViewSliderProgressDemo : UserControl
{
    public ExplodedViewSliderProgressDemo()
    {
        InitializeComponent();
        ExplodedViewDemoData.Fill(View);
        View.Title = null;
        View.Description = null;
        // External mode: nothing scrolls; the slider drives Progress and the scene follows on its spring.
        Scrub.ValueChanged += (_, _) => View.Progress = Scrub.Value / 100;
        View.StopChanged += (_, e) => Show(e.Stop);
        Show(View.CurrentStop);
    }

    private void Show(int stop) =>
        Status.Text = $"Stop {stop} of {View.StopCount - 1}: {View.GetStopName(stop)}";
}
