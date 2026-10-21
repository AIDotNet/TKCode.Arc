using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ShortcutRecorderDisabledDemo : UserControl
{
    public ShortcutRecorderDisabledDemo()
    {
        InitializeComponent();
        var palette = ShortcutFormatter.Parse("mod+k", ShortcutFormatter.CurrentPlatform);
        Recorder.Value = palette;
        Recorder.ResetValue = palette;
    }
}
