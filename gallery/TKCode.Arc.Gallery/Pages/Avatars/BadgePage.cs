using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class BadgePage
{
    public static Control Create()
    {
        var tones = GalleryUi.Row(
            new Badge { Content = "Draft" },
            new Badge { Tone = BadgeTone.Success, Content = "Live" },
            new Badge { Tone = BadgeTone.Info, Content = "Beta" },
            new Badge { Tone = BadgeTone.Warning, Content = "Degraded" },
            new Badge { Tone = BadgeTone.Danger, Content = "Failed" });
        var small = GalleryUi.Row(
            new Badge { Size = BadgeSize.Small, Content = "Draft" },
            new Badge { Size = BadgeSize.Small, Tone = BadgeTone.Success, Content = "Live" },
            new Badge { Size = BadgeSize.Small, Tone = BadgeTone.Info, Content = "Beta" },
            new Badge { Size = BadgeSize.Small, Tone = BadgeTone.Warning, Content = "Degraded" },
            new Badge { Size = BadgeSize.Small, Tone = BadgeTone.Danger, Content = "Failed" });
        var icons = GalleryUi.Row(
            new Badge { Tone = BadgeTone.Success, Content = "Passing", Icon = new ArcGlyph { Data = ArcIcons.Check, Size = 12 } },
            new Badge { Tone = BadgeTone.Info, Content = "Synced", Icon = new ArcGlyph { Data = ArcIcons.RefreshCw, Size = 12 } },
            new Badge { Tone = BadgeTone.Danger, Content = "Blocked", Icon = new ArcGlyph { Data = ArcIcons.TriangleAlert, Size = 12 } });

        var deploy = new Badge { Tone = BadgeTone.Info, Content = "Queued", Icon = new ArcGlyph { Data = ArcIcons.Clock, Size = 12 } };
        var states = new (string Text, BadgeTone Tone, Avalonia.Media.Geometry Icon)[]
        {
            ("Queued", BadgeTone.Info, ArcIcons.Clock), ("Deploying", BadgeTone.Warning, ArcIcons.LoaderCircle),
            ("Live", BadgeTone.Success, ArcIcons.Check), ("Rolled back", BadgeTone.Danger, ArcIcons.RotateCcw)
        };
        var index = 0;
        var next = new Button { Content = "Next state", Variant = ButtonVariant.Secondary };
        next.Click += (_, _) =>
        {
            var (text, tone, icon) = states[index = (index + 1) % states.Length];
            deploy.Content = text;
            deploy.Tone = tone;
            deploy.Icon = new ArcGlyph { Data = icon, Size = 12 };
        };
        var count = new Badge { Content = 3 };
        var bump = new Button { Content = "Add comment", Variant = ButtonVariant.Secondary };
        bump.Click += (_, _) => count.Content = (int)count.Content! + 1;

        return GalleryUi.Column(20,
            GalleryUi.Card("Tones", "Neutral, success, info (accent), warning and danger. The label must say the state on its own: tone is not the only signal.", tones),
            GalleryUi.Card("Small", "22px tall with 11px text, for dense tables.", small),
            GalleryUi.Card("Icons", "A leading icon takes the tone color and is hidden from assistive technology.", icons),
            GalleryUi.Card("Changing in place", "Keep the badge mounted and change its content: the new label rises out of a soft blur, a new icon pops in, and the pill springs to the new width.",
                GalleryUi.Column(16, GalleryUi.Row(deploy, count), GalleryUi.Row(next, bump))));
    }
}
