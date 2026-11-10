using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class EmojiPickerSkinToneDemo : UserControl
{
    public EmojiPickerSkinToneDemo()
    {
        InitializeComponent();
    }

    private void OnDefault(object? sender, RoutedEventArgs e) => Picker.SkinTone = EmojiSkinTone.Default;

    private void OnLight(object? sender, RoutedEventArgs e) => Picker.SkinTone = EmojiSkinTone.Light;

    private void OnMedium(object? sender, RoutedEventArgs e) => Picker.SkinTone = EmojiSkinTone.Medium;

    private void OnDark(object? sender, RoutedEventArgs e) => Picker.SkinTone = EmojiSkinTone.Dark;
}
