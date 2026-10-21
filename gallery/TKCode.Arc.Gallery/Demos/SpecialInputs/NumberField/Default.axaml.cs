using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class NumberFieldDefaultDemo : UserControl
{
    public NumberFieldDefaultDemo()
    {
        InitializeComponent();
        // The unit follows the value: "1 seat", "3 seats".
        Seats.SuffixFormatter = n => n == 1 ? " seat" : " seats";
    }
}
