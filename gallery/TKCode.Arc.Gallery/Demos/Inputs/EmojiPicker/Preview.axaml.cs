using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class EmojiPickerPreviewDemo : UserControl
{
    public EmojiPickerPreviewDemo()
    {
        InitializeComponent();
        // A saved list of recent emoji, most recent first.
        foreach (var emoji in new[] { "👍", "🎉", "😂", "❤️", "🙏", "👀" })
        {
            Picker.Recent.Add(emoji);
        }
    }

    private void OnEmojiSelected(object? sender, EmojiSelectedEventArgs e) =>
        Picked.Text = $"Picked {e.Emoji}  {e.Name}";
}
