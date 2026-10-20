using TKCode.Arc.Gallery.Shell;
using AtomUI.Controls;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class AvatarPage
{
    public static Control Create()
    {
        var sizes = GalleryUi.Row(
            new Avatar { DisplayName = "Ada Lovelace", Size = AvatarSize.Small },
            new Avatar { DisplayName = "Grace Hopper", Size = AvatarSize.Medium },
            new Avatar { DisplayName = "Alan Turing", Size = AvatarSize.Large },
            new Avatar { DisplayName = "Katherine Johnson", Size = AvatarSize.XLarge });
        var photos = GalleryUi.Row(
            new Avatar { DisplayName = "Mia Chen", Size = AvatarSize.Large, Source = ImageSource.Parse("https://i.pravatar.cc/160?img=47") },
            new Avatar { DisplayName = "Noah Park", Size = AvatarSize.Large, Source = ImageSource.Parse("https://i.pravatar.cc/160?img=12") },
            new Avatar { DisplayName = "Broken Link", Size = AvatarSize.Large, Source = ImageSource.Parse("https://invalid.example/avatar.png") });
        var presence = new Avatar { DisplayName = "Leah Fox", Size = AvatarSize.Large, Status = AvatarStatus.Online };
        var toggle = new Button { Content = "Toggle status", Variant = ButtonVariant.Secondary };
        toggle.Click += (_, _) => presence.Status = presence.Status switch
        {
            AvatarStatus.Online => AvatarStatus.Offline,
            AvatarStatus.Offline => AvatarStatus.None,
            _ => AvatarStatus.Online
        };
        return GalleryUi.Column(20,
            GalleryUi.Card("Initials", "Sizes 28, 36, 48 and 88.", sizes),
            GalleryUi.Card("Photos", "A photo that is still downloading fades in from a soft blur; a broken one falls back to initials.", photos),
            GalleryUi.Card("Status", "The dot pops in on a snappy spring.", GalleryUi.Row(presence, toggle)));
    }
}
