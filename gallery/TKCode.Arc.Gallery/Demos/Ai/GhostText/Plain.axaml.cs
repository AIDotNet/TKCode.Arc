using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A reply box inside a card; the demo model completes the sentence.</summary>
public partial class GhostTextPlainDemo : UserControl
{
    public GhostTextPlainDemo()
    {
        InitializeComponent();
        Field.Suggest = GhostTextDemoModel.Create();
    }
}
