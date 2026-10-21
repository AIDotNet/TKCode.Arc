using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ShortcutRecorderDefaultDemo : UserControl
{
    public ShortcutRecorderDefaultDemo()
    {
        InitializeComponent();
        var palette = ShortcutFormatter.Parse("mod+k", ShortcutFormatter.CurrentPlatform);
        Recorder.Value = palette;
        Recorder.ResetValue = palette;
    }
}
