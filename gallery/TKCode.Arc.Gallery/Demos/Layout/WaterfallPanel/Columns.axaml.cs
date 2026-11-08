using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class WaterfallPanelColumnsDemo : UserControl
{
    private static readonly int[] s_columns = [2, 3, 4, 0];

    public WaterfallPanelColumnsDemo()
    {
        InitializeComponent();
        Count.SelectionChanged += (_, _) =>
        {
            if (Count.SelectedIndex >= 0)
            {
                Wall.Columns = s_columns[Count.SelectedIndex];
            }
        };
    }
}
