using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ShortcutRecorderHiddenLabelDemo : UserControl
{
    public ShortcutRecorderHiddenLabelDemo()
    {
        InitializeComponent();
        var toggleComments = ShortcutFormatter.Parse("mod+/", ShortcutFormatter.CurrentPlatform);
        Recorder.Value = toggleComments;
        Recorder.ResetValue = toggleComments;
    }
}
