using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SegmentedControlAccessoryAndIntentDemo : UserControl
{
    public SegmentedControlAccessoryAndIntentDemo()
    {
        InitializeComponent();
    }

    // Raised when the pointer or focus reaches a segment, before it is chosen.
    private void OnItemIntent(object? sender, SegmentedItemIntentEventArgs e)
    {
        Intent.Text = $"Intent: {e.Value}";
    }
}
