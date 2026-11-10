using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RulerSliderFormattedDemo : UserControl
{
    public RulerSliderFormattedDemo()
    {
        InitializeComponent();
        Budget.ValueFormatter = value => $"${value:N0}";
    }
}
