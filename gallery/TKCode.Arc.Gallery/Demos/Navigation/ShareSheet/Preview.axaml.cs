using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ShareSheetPreviewDemo : UserControl
{
    public ShareSheetPreviewDemo()
    {
        InitializeComponent();
    }

    private void OnShared(object? sender, ShareSheetTargetEventArgs e) => Status.Text = $"Shared with {e.Target.Name}";
}
