using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class TooltipPage
{
    public static Control Create()
    {
        Button Tip(string label, string tip, TooltipSide side = TooltipSide.Top)
        {
            var button = new Button { Content = label, Variant = ButtonVariant.Secondary };
            Tooltip.SetTip(button, tip);
            Tooltip.SetSide(button, side);
            return button;
        }

        var row = GalleryUi.Row(Tip("Top", "Copy link"), Tip("Bottom", "Share with your team", TooltipSide.Bottom),
            Tip("Left", "Archive", TooltipSide.Left), Tip("Right", "Delete forever", TooltipSide.Right));

        var live = new Button { Content = "Save", Variant = ButtonVariant.Primary };
        Tooltip.SetTip(live, "Save draft");
        var saves = 0;
        live.Click += (_, _) => Tooltip.SetTip(live, ++saves % 2 == 1 ? "Saved just now" : "Save draft");

        return GalleryUi.Column(20,
            GalleryUi.Card("Sides", "The first tooltip waits 250ms and rises from its trigger; move to a neighbour within 300ms and it opens at once with a fade.", row),
            GalleryUi.Card("Live text", "Click while hovering: the text crossfades and the bubble springs to the new size.", GalleryUi.Row(live)));
    }
}
