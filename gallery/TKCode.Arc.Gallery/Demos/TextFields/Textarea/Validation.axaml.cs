using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TextareaValidationDemo : UserControl
{
    public TextareaValidationDemo()
    {
        InitializeComponent();
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        var feedback = (Textarea)sender!;
        feedback.ErrorMessage = (feedback.Text?.Trim().Length ?? 0) < 40
            ? "Tell us a little more: at least 40 characters."
            : null;
    }
}
