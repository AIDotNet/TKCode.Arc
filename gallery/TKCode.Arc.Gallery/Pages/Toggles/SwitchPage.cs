using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class SwitchPage
{
    public static Control Create()
    {
        var settings = GalleryUi.Column(4,
            new Switch { Content = "Email notifications", IsChecked = true },
            new Switch { Content = "Weekly digest" },
            new Switch { Content = "Product updates" });
        var disabled = GalleryUi.Row(
            new Switch { Content = "Off and disabled", IsEnabled = false },
            new Switch { Content = "On and disabled", IsChecked = true, IsEnabled = false });
        return GalleryUi.Column(20,
            GalleryUi.Card("Settings list", "Press and hold to see the thumb stretch; release and it glides on a critically damped spring.", settings),
            GalleryUi.Card("Disabled", null, disabled));
    }
}
