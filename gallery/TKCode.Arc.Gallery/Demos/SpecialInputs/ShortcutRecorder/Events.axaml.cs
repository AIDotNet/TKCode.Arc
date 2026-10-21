using Avalonia.Controls;
using Avalonia.Input;

namespace TKCode.Arc.Gallery.Demos;

public partial class ShortcutRecorderEventsDemo : UserControl
{
    public ShortcutRecorderEventsDemo()
    {
        InitializeComponent();
        var platform = ShortcutFormatter.CurrentPlatform;
        var palette = ShortcutFormatter.Parse("mod+k", platform);
        Recorder.Value = palette;
        Recorder.ResetValue = palette;
        Recorder.Bindings.Add(new ShortcutBinding(ShortcutFormatter.Parse("mod+s", platform), "Save"));
        Recorder.ValueChanged += OnValueChanged;
    }

    private void OnValueChanged(object? sender, ShortcutRecorderValueChangedEventArgs e)
    {
        var platform = Recorder.EffectivePlatform;
        string Show(KeyGesture? gesture) => gesture is null ? "none" : ShortcutFormatter.Format(gesture, platform).Text;

        var line = $"ValueChanged {Show(e.OldValue)} → {Show(e.NewValue)}";
        if (e.Replaced is { } replaced)
        {
            line += $" (replaced {replaced.Label})";
        }
        Log.Children.Remove(Hint);
        Log.Children.Insert(0, new TextBlock { Text = line, Classes = { "log" } });
        while (Log.Children.Count > 6)
        {
            Log.Children.RemoveAt(Log.Children.Count - 1);
        }
    }
}
