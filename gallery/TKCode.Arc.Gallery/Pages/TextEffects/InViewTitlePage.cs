using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class InViewTitlePage
{
    public static Control Create()
    {
        var titles = GalleryUi.Column(64,
            new InViewTitle { Text = "Blur sharpens word by word", Variant = InViewTitleVariant.Blur },
            new InViewTitle { Text = "Words rise out of a clip", Variant = InViewTitleVariant.Word },
            new InViewTitle { Text = "Line by line", Variant = InViewTitleVariant.Line, Lines = ["Lines rise one by one,", "without reflowing"] },
            new InViewTitle { Text = "Tracking opens up", Variant = InViewTitleVariant.Tracking },
            new InViewTitle { Text = "A soft wipe uncovers the title", Variant = InViewTitleVariant.Wipe },
            new InViewTitle { Text = "This one replays on every entry", Variant = InViewTitleVariant.Blur, IsReplayEnabled = true, Level = TextLevel.H3 });
        var scroller = new ScrollViewer
        {
            Height = 360,
            Content = new StackPanel
            {
                Children =
                {
                    GalleryUi.Text("Scroll down: each title reveals itself when 45% of it is visible.", ArcDesignTokenKind.TextSecondary),
                    new Border { Height = 380 },
                    titles,
                    new Border { Height = 320 }
                }
            }
        };
        return GalleryUi.Column(20,
            GalleryUi.Card("Five variants", "Opacity leads, the blur clears next and the movement settles last, so each title is readable before it fully arrives.", scroller));
    }
}
