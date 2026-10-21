using Avalonia.Controls;
using Avalonia.Input;

namespace TKCode.Arc.Gallery.Demos;

public partial class ShortcutRecorderConflictsDemo : UserControl
{
    public ShortcutRecorderConflictsDemo()
    {
        InitializeComponent();
        var platform = ShortcutFormatter.CurrentPlatform;
        string Caps(KeyGesture gesture) => ShortcutFormatter.Format(gesture, platform).Text;

        var quickSearch = ShortcutFormatter.Parse("mod+p", platform);
        var save = ShortcutFormatter.Parse("mod+s", platform);
        var find = ShortcutFormatter.Parse("mod+f", platform);

        Recorder.Value = quickSearch;
        Recorder.ResetValue = quickSearch;
        Recorder.Bindings.Add(new ShortcutBinding(save, "Save"));
        Recorder.Bindings.Add(new ShortcutBinding(find, "Find"));
        Recorder.Description = $"Try {Caps(save)} or {Caps(find)}: both are already bound.";
    }
}
