using Avalonia.Controls;
using Avalonia.Input;

namespace TKCode.Arc.Gallery.Demos;

public partial class CommandPaletteOnDemandDemo : UserControl
{
    public CommandPaletteOnDemandDemo()
    {
        InitializeComponent();
        // Cmd+K belongs to the preview above on this page, so this one answers to Cmd/Ctrl+Shift+P.
        var modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        Palette.OpenGesture = new KeyGesture(Key.P, modifier | KeyModifiers.Shift);
        Hint.Text = OperatingSystem.IsMacOS() ? "or press ⌘ ⇧ P" : "or press Ctrl+Shift+P";
    }

    private void OnItemSelected(object? sender, CommandPaletteItemSelectedEventArgs e)
        => Result.Text = $"Ran “{e.Item.Label}”";
}
