using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CyclicPanelPreviewDemo : UserControl
{
    public CyclicPanelPreviewDemo()
    {
        InitializeComponent();
        Previous.Click += (_, _) => Wheel.ScrollPrevious();
        Next.Click += (_, _) => Wheel.ScrollNext();
        Indices.Text = "VisibleItemIndices: [0, 1, 2, 3, 4]";
        Wheel.PropertyChanged += (_, e) =>
        {
            if (e.Property == CyclicPanel.VisibleItemIndicesProperty)
            {
                Indices.Text = $"VisibleItemIndices: [{string.Join(", ", Wheel.VisibleItemIndices)}]";
            }
        };
    }
}
