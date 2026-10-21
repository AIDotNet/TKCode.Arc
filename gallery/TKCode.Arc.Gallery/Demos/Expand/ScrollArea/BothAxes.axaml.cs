using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ScrollAreaBothAxesDemo : UserControl
{
    public ScrollAreaBothAxesDemo()
    {
        InitializeComponent();
        // A 12 by 18 sheet of cells: A1, B1, ... R1, A2, ...
        Cells.ItemsSource = Enumerable.Range(0, 12 * 18).Select(i => $"{(char)('A' + i % 18)}{i / 18 + 1}").ToArray();
    }
}
