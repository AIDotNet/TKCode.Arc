using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class HoverCardPage
{
    public static Control Create()
    {
        HoverCard Mention(string handle, string name, string role, string bio, int projects, int reviews, string meta) => new()
        {
            Trigger = new Button { Content = handle, Variant = ButtonVariant.Ghost },
            Content = new HoverCardProfile
            {
                DisplayName = name,
                Role = role,
                Bio = bio,
                Meta = meta,
                Stats = { new HoverCardStat("Projects", projects.ToString()), new HoverCardStat("Reviews", reviews.ToString()) }
            }
        };

        var maya = Mention("@maya", "Maya Chen", "Product designer, Payments", "Designs checkout and billing flows. Previously at Linear.", 12, 48, "Lisbon · 14:20 local time");
        var ari = Mention("@ari", "Ari Okafor", "Staff engineer, Platform", "Owns the build pipeline and the design token compiler.", 31, 210, "Lagos · 13:20 local time");
        var lena = Mention("@lena", "Lena Novak", "Research lead", "Runs the quarterly usability studies.", 7, 19, "Prague · 14:20 local time");

        var link = new TextBlock { Text = "uiarc.dev/components/hover-card", Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) };
        ArcTokens.Bind(link, TextBlock.ForegroundProperty, ArcDesignTokenKind.Accent);
        HoverCard.SetAttach(link, new HoverCard { Content = "Hover card: preview a person or link on hover or focus without leaving the page.", Placement = PopoverPlacement.Top });

        return GalleryUi.Column(20,
            GalleryUi.Card("Mention", "Hover for half a second, or Tab to it. The card stays open while the pointer travels into it; Escape closes it.", GalleryUi.Row(maya)),
            GalleryUi.Card("Dense list", "Once a card is open, moving to the next trigger opens it almost at once with a fade only.", GalleryUi.Row(ari, lena)),
            GalleryUi.Card("Link preview", "Attached to a plain text link with arc:HoverCard.Attach.", link));
    }
}
