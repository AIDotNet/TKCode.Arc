using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class StatsBandPreviewDemo : UserControl
{
    public StatsBandPreviewDemo()
    {
        InitializeComponent();
        Layouts.SelectionChanged += (_, _) =>
        {
            Band.Layout = Layouts.SelectedValue is "plain" ? StatsBandLayout.Plain : StatsBandLayout.Divided;
            Band.Replay();
        };
    }
}
