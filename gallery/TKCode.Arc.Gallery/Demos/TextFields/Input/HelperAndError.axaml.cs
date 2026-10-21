using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class InputHelperAndErrorDemo : UserControl
{
    public InputHelperAndErrorDemo()
    {
        InitializeComponent();
    }

    // The helper copy is derived from state: changing the string rewrites only the words that changed,
    // and the count rolls.
    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = Username.Text ?? string.Empty;
        var left = Math.Max(0, 20 - text.Length);
        Username.Description = $"3 to 20 characters. {left} left.";
        Username.ErrorMessage = text.Length is > 0 and < 3
            ? "Use at least 3 characters."
            : text.Contains(' ') ? "Spaces are not allowed in a username." : null;
    }
}
