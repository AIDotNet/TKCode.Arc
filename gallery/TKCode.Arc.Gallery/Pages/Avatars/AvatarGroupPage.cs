using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class AvatarGroupPage
{
    private static readonly string[] Names =
    [
        "Maya Chen", "Noah Park", "Leah Fox", "Omar Haddad", "Ines Duarte", "Kenji Sato", "Ava Brooks", "Luca Romano", "Zara Ali"
    ];

    public static Control Create()
    {
        var team = new AvaloniaList<AvatarGroupMember>
        {
            new("Maya Chen", status: AvatarStatus.Online), new("Noah Park"), new("Leah Fox", status: AvatarStatus.Offline),
            new("Omar Haddad"), new("Ines Duarte"), new("Kenji Sato")
        };
        var group = new AvatarGroup { ItemsSource = team, Label = "Editors" };
        var next = 6;
        var add = new Button { Content = "Add person", Variant = ButtonVariant.Secondary };
        add.Click += (_, _) => team.Insert(1, new AvatarGroupMember(Names[next++ % Names.Length]));
        var remove = new Button { Content = "Remove person", Variant = ButtonVariant.Secondary };
        remove.Click += (_, _) =>
        {
            if (team.Count > 0)
            {
                team.RemoveAt(Math.Min(1, team.Count - 1));
            }
        };

        var sizes = GalleryUi.Column(16,
            new AvatarGroup { ItemsSource = team, Size = AvatarSize.Small, MaxVisibleCount = 3 },
            new AvatarGroup { ItemsSource = team, Size = AvatarSize.Medium },
            new AvatarGroup { ItemsSource = team, Size = AvatarSize.Large, MaxVisibleCount = 5 });

        var inline = new AvatarGroup
        {
            Items =
            {
                new AvatarGroupMember("Ada Lovelace"), new AvatarGroupMember("Grace Hopper"), new AvatarGroupMember("Alan Turing")
            }
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Team", "Point at the stack: it loosens from the center, the person under the pointer lifts with a crisp ring and their name. People who join or leave open or close their slot; the overflow count rolls the way it moved.",
                GalleryUi.Column(16, new Border { Padding = new Thickness(0, 28, 0, 0), Child = group }, GalleryUi.Row(add, remove))),
            GalleryUi.Card("Sizes and limits", "28, 36 and 48px stacks with 3, 4 and 5 people before the overflow chip.", sizes),
            GalleryUi.Card("Inline members", "Members can be written in place of a bound list.", inline));
    }
}
