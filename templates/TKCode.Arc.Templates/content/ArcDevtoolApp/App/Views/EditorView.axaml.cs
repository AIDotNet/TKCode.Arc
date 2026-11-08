using Avalonia.Controls;

namespace ArcStarterApp.Views;

public partial class EditorView : UserControl
{
    public EditorView()
    {
        InitializeComponent();
        // A Textarea's frame has its own height: keep it as tall as the pane.
        SizeChanged += (_, e) => Editor.FrameHeight = Math.Max(Editor.FrameMinHeight, e.NewSize.Height - Editor.Margin.Top - Editor.Margin.Bottom);
    }
}
