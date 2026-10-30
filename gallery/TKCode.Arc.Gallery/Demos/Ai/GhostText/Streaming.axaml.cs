using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Streams continuations from the built-in demo model (nothing is sent anywhere).</summary>
public partial class GhostTextStreamingDemo : UserControl
{
    public GhostTextStreamingDemo()
    {
        InitializeComponent();
        Field.Suggest = GhostTextDemoModel.Create();
        Field.Model = GhostTextDemoData.Model;
    }
}
