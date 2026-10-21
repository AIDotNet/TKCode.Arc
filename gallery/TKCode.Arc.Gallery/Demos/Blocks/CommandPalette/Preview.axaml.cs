using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CommandPalettePreviewDemo : UserControl
{
    public CommandPalettePreviewDemo()
    {
        InitializeComponent();
        Hint.Text = OperatingSystem.IsMacOS() ? "⌘ K to open, Esc to close" : "Ctrl K to open, Esc to close";
    }

    private void OnItemSelected(object? sender, CommandPaletteItemSelectedEventArgs e)
        => LastCommand.Text = $"Last command: {e.Item.Label}";
}
