using Avalonia.Controls;
using Avalonia.Input;

namespace TKCode.Arc.Gallery.Demos;

public partial class EmojiPickerInComposerDemo : UserControl
{
    public EmojiPickerInComposerDemo()
    {
        InitializeComponent();
    }

    private void OnEmojiSelected(object? sender, EmojiSelectedEventArgs e)
    {
        var text = Message.Text ?? string.Empty;
        var caret = Math.Clamp(Message.CaretIndex, 0, text.Length);
        Message.Text = text.Insert(caret, e.Emoji);
        EmojiPopover.Close();
        Message.Focus(NavigationMethod.Tab);
        Message.CaretIndex = caret + e.Emoji.Length;
    }
}
