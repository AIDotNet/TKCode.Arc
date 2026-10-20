using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class CopyButtonPage
{
    public static Control Create()
    {
        var outline = GalleryUi.Row(
            new CopyButton { Value = "npx arc add copy-button" },
            new CopyButton { Value = "sk_live_51H...", Label = "Copy key" },
            new CopyButton { Value = "https://uiarc.dev", IsIconOnly = true });
        var plain = GalleryUi.Row(
            new CopyButton { Value = "#7C5CFF", Label = "Copy hex", Variant = CopyButtonVariant.Plain },
            new CopyButton { Value = "rgb(124 92 255)", Variant = CopyButtonVariant.Plain, IsIconOnly = true });
        var failing = new CopyButton { Value = null, Label = "Copy nothing" };
        return GalleryUi.Column(20,
            GalleryUi.Card("Outline", "Click to copy: the icon draws a check, the label morphs to Copied and back after 1.9s. The width never changes.", outline),
            GalleryUi.Card("Plain", "For dense rows and code blocks.", plain),
            GalleryUi.Card("Failure", "Without a value the copy fails and the button says so.", GalleryUi.Row(failing)));
    }
}
