using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class SkeletonPage
{
    public static Control Create()
    {
        var profile = new Skeleton { IsAvatarVisible = true };
        var comment = new Skeleton { Lines = 4 };

        var loaded = GalleryUi.Column(10,
            new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 12,
                Children = { new Avatar { DisplayName = "Maya Chen", Size = AvatarSize.Medium }, GalleryUi.Text("Maya Chen", ArcDesignTokenKind.Foreground, 16) }
            },
            GalleryUi.Text("Shipped the new billing page. The usage meter now explains what fills the plan.", ArcDesignTokenKind.TextSecondary));
        var reveal = new Skeleton { IsAvatarVisible = true, Lines = 2, Content = loaded };
        var toggle = new Button { Content = "Finish loading", Variant = ButtonVariant.Secondary };
        toggle.Click += (_, _) =>
        {
            reveal.IsLoading = !reveal.IsLoading;
            toggle.Content = reveal.IsLoading ? "Finish loading" : "Load again";
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Profile", "A round avatar and three lines; the blocks pulse top to bottom in a calm wave for about 20 seconds, then rest.", profile),
            GalleryUi.Card("Lines", "Up to six lines: the first is shorter and taller, the last ends at 72%.", comment),
            GalleryUi.Card("Into content", "When loading finishes the placeholder fades out, the content rises into place and the height springs to fit.",
                GalleryUi.Column(16, reveal, GalleryUi.Row(toggle))));
    }
}
