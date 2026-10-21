using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TextareaLiveCountDemo : UserControl
{
    private const int Limit = 160;

    public TextareaLiveCountDemo()
    {
        InitializeComponent();
    }

    // The count rolls only the digits that change.
    private void OnTextChanged(object? sender, TextChangedEventArgs e)
        => Bio.Description = $"{Limit - (Bio.Text?.Length ?? 0)} characters left";
}
