using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class HoloCardControlledDemo : UserControl
{
    private readonly List<string> _lines = new();

    public HoloCardControlledDemo()
    {
        InitializeComponent();
    }

    private void OnFlippedChanged(object? sender, HoloCardFlippedEventArgs e)
    {
        var side = e.Side switch
        {
            HoloCardFlipSide.Left => "left half",
            HoloCardFlipSide.Right => "right half",
            HoloCardFlipSide.Keyboard => "keyboard",
            _ => "code",
        };
        _lines.Insert(0, $"{(e.IsFlipped ? "Back" : "Front")} shown ({side})");
        if (_lines.Count > 4)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join("\n", _lines);
    }
}
