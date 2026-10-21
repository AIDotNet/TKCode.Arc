using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SliderStepsAndMarksDemo : UserControl
{
    public SliderStepsAndMarksDemo()
    {
        InitializeComponent();
        Speed.ValueFormatter = v => $"{v:0.##}×";
    }
}
